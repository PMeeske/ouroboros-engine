using System.Net.Http.Json;
using System.Reactive.Linq;
using Polly;
using Polly.CircuitBreaker;
using Polly.Wrap;

namespace Ouroboros.Providers;

/// <summary>
/// HTTP client specifically designed for Ollama Cloud API endpoints.
/// Uses Ollama's native JSON API format with /api/generate endpoint.
/// Includes Polly exponential backoff retry policy to handle rate limiting.
/// Supports thinking mode for models like DeepSeek R1 that emit &lt;think&gt; tags.
/// </summary>
public sealed class OllamaCloudChatModel : IStreamingThinkingChatModel, ICostAwareChatModel
{
    // Global concurrency limiter: prevents thundering herd when multiple model slots
    // (coder, reasoner, summarizer) all target the same cloud endpoint simultaneously.
    private static readonly SemaphoreSlim s_cloudConcurrency = new(2, 2);

    private readonly HttpClient _client;
    private readonly string _model;
    private readonly ChatRuntimeSettings _settings;
    private readonly AsyncPolicyWrap<HttpResponseMessage> _resiliencePolicy;
    private readonly LlmCostTracker? _costTracker;

    /// <summary>
    /// Gets the cost tracker for this model instance.
    /// </summary>
    public LlmCostTracker? CostTracker => _costTracker;

    public OllamaCloudChatModel(string endpoint, string apiKey, string model, ChatRuntimeSettings? settings = null, LlmCostTracker? costTracker = null)
    {
        if (string.IsNullOrWhiteSpace(endpoint)) throw new ArgumentException("Endpoint is required", nameof(endpoint));
        if (string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentException("API key is required", nameof(apiKey));

        _client = new HttpClient
        {
            BaseAddress = new Uri(endpoint.TrimEnd('/'), UriKind.Absolute),
            Timeout = TimeSpan.FromMinutes(10) // Large models like DeepSeek 671B need longer timeouts
        };
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        _model = model;
        _settings = settings ?? new ChatRuntimeSettings();
        _costTracker = costTracker ?? new LlmCostTracker(model);

        // === ENHANCED POLLY RESILIENCE POLICY ===

        // 1. Retry policy with exponential backoff + jitter for rate limiting and server errors
        var retryPolicy = Policy
            .HandleResult<HttpResponseMessage>(r =>
                (int)r.StatusCode == 429 ||    // Too Many Requests
                (int)r.StatusCode == 503 ||    // Service Unavailable (explicitly)
                (int)r.StatusCode >= 500)      // All server errors
            .Or<HttpRequestException>()        // Network failures
            .Or<TaskCanceledException>(ex =>   // Only genuine HttpClient timeouts, NOT external cancellation.
                // HttpClient.Timeout fires as TaskCanceledException with InnerException=TimeoutException.
                // Deliberate cancellation (Racing mode, user Ctrl+C) produces a bare TCE without a TimeoutException.
                ex.InnerException is TimeoutException)
            .WaitAndRetryAsync(
                retryCount: 5,  // Increased from 3 for cloud instability
                sleepDurationProvider: retryAttempt =>
                {
                    // Exponential backoff with jitter to avoid thundering herd
                    var baseDelay = TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));
                    var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000));
                    return baseDelay + jitter;
                },
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    var reason = outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString() ?? "Unknown";
                    Console.WriteLine($"[OllamaCloudChatModel] Retry {retryCount}/5 after {timespan.TotalSeconds:F1}s due to {reason}");
                });

        // 2. Circuit breaker - fail fast if service is consistently down
        var circuitBreakerPolicy = Policy
            .HandleResult<HttpResponseMessage>(r => (int)r.StatusCode >= 500)
            .Or<HttpRequestException>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,  // Open circuit after 5 consecutive failures
                durationOfBreak: TimeSpan.FromSeconds(30),  // Stay open for 30s before trying again
                onBreak: (outcome, breakDelay) =>
                {
                    Console.WriteLine($"[OllamaCloudChatModel] ⚡ Circuit OPEN for {breakDelay.TotalSeconds}s - service appears down");
                },
                onReset: () =>
                {
                    Console.WriteLine($"[OllamaCloudChatModel] ✓ Circuit CLOSED - service recovered");
                },
                onHalfOpen: () =>
                {
                    Console.WriteLine($"[OllamaCloudChatModel] ○ Circuit HALF-OPEN - testing service...");
                });

        // Combine: Retry wraps Circuit Breaker (retry outside, circuit breaker inside)
        _resiliencePolicy = Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);
    }

    /// <inheritdoc/>
    public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        _costTracker?.StartRequest();
        await s_cloudConcurrency.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Use Ollama's native /api/generate endpoint and JSON format
            var payloadObject = new
            {
                model = _model,
                prompt = _settings.Culture is { Length: > 0 } c ? $"Please answer in {c}. {prompt}" : prompt,
                stream = false,
                options = new
                {
                    temperature = _settings.Temperature,
                    num_predict = _settings.MaxTokens > 0 ? _settings.MaxTokens : (int?)null
                }
            };

            HttpResponseMessage response = await _resiliencePolicy.ExecuteAsync(async (innerCt) =>
            {
                // Create fresh JsonContent for each retry attempt (content can only be used once).
                // innerCt is checked by Polly before each retry — so a Racing-mode cancellation
                // causes immediate abort instead of 5x exponential-backoff spam.
                using JsonContent payload = JsonContent.Create(payloadObject);
                return await _client.PostAsync("/api/generate", payload, innerCt).ConfigureAwait(false);
            }, ct).ConfigureAwait(false);

            string rawContent = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[OllamaCloudChatModel] HTTP {(int)response.StatusCode}: {rawContent}");
                return $"[ollama-cloud-fallback:{_model}] {prompt}";
            }

            using var doc = System.Text.Json.JsonDocument.Parse(rawContent);
            if (doc.RootElement.TryGetProperty("response", out var responseElement))
            {
                // Extract token counts from Ollama response if available
                int promptTokens = 0, outputTokens = 0;
                if (doc.RootElement.TryGetProperty("prompt_eval_count", out var promptEval))
                    promptTokens = promptEval.GetInt32();
                if (doc.RootElement.TryGetProperty("eval_count", out var evalCount))
                    outputTokens = evalCount.GetInt32();

                _costTracker?.EndRequest(promptTokens, outputTokens);
                return responseElement.GetString() ?? "";
            }

            Console.WriteLine($"[OllamaCloudChatModel] No 'response' field in: {rawContent}");
        }
        catch (BrokenCircuitException)
        {
            // Circuit is open - service is down, fail fast without spamming logs
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Deliberate cancellation (e.g. Racing mode found a winner) — not an error, don't log
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OllamaCloudChatModel] Error: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            s_cloudConcurrency.Release();
        }
        return $"[ollama-cloud-fallback:{_model}] {prompt}";
    }

    /// <inheritdoc/>
    public async Task<ThinkingResponse> GenerateWithThinkingAsync(string prompt, CancellationToken ct = default)
    {
        string rawText = await GenerateTextAsync(prompt, ct);
        return ThinkingResponse.FromRawText(rawText);
    }

    /// <inheritdoc/>
    public IObservable<(bool IsThinking, string Chunk)> StreamWithThinkingAsync(string prompt, CancellationToken ct = default)
    {
        return Observable.Create<(bool IsThinking, string Chunk)>(async (observer, token) =>
        {
            await s_cloudConcurrency.WaitAsync(token).ConfigureAwait(false);
            try
            {
                using JsonContent payload = JsonContent.Create(new
                {
                    model = _model,
                    prompt = _settings.Culture is { Length: > 0 } c ? $"Please answer in {c}. {prompt}" : prompt,
                    stream = true,
                    options = new
                    {
                        temperature = _settings.Temperature,
                        num_predict = _settings.MaxTokens > 0 ? _settings.MaxTokens : (int?)null
                    }
                });

                HttpResponseMessage response = await _resiliencePolicy.ExecuteAsync(
                    (innerToken) => _client.PostAsync("/api/generate", payload, innerToken),
                    token).ConfigureAwait(false);

                response.EnsureSuccessStatusCode();

                using Stream responseStream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
                using StreamReader reader = new StreamReader(responseStream);

                bool inThinking = false;
                StringBuilder buffer = new();

                while (!reader.EndOfStream && !token.IsCancellationRequested)
                {
                    string? line = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    try
                    {
                        using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(line);
                        if (doc.RootElement.TryGetProperty("response", out System.Text.Json.JsonElement responseElement))
                        {
                            string? content = responseElement.GetString();
                            if (!string.IsNullOrEmpty(content))
                            {
                                buffer.Append(content);
                                string bufferStr = buffer.ToString();

                                // Check for thinking tag transitions
                                if (!inThinking && bufferStr.Contains("<think>", StringComparison.OrdinalIgnoreCase))
                                {
                                    int idx = bufferStr.IndexOf("<think>", StringComparison.OrdinalIgnoreCase);
                                    string beforeTag = bufferStr[..idx];
                                    if (!string.IsNullOrEmpty(beforeTag))
                                        observer.OnNext((false, beforeTag));

                                    buffer.Clear();
                                    buffer.Append(bufferStr[(idx + 7)..]);
                                    inThinking = true;
                                    continue;
                                }

                                if (inThinking && bufferStr.Contains("</think>", StringComparison.OrdinalIgnoreCase))
                                {
                                    int idx = bufferStr.IndexOf("</think>", StringComparison.OrdinalIgnoreCase);
                                    string thinkingContent = bufferStr[..idx];
                                    if (!string.IsNullOrEmpty(thinkingContent))
                                        observer.OnNext((true, thinkingContent));

                                    buffer.Clear();
                                    buffer.Append(bufferStr[(idx + 8)..]);
                                    inThinking = false;
                                    continue;
                                }

                                // Emit chunk with current state
                                observer.OnNext((inThinking, content));
                                buffer.Clear();
                            }
                        }

                        if (doc.RootElement.TryGetProperty("done", out System.Text.Json.JsonElement doneElement) && doneElement.GetBoolean())
                        {
                            // Flush any remaining buffer
                            if (buffer.Length > 0)
                                observer.OnNext((inThinking, buffer.ToString()));
                            observer.OnCompleted();
                            return;
                        }
                    }
                    catch (System.Text.Json.JsonException)
                    {
                        // Skip malformed JSON chunks
                        continue;
                    }
                }

                observer.OnCompleted();
            }
            catch (BrokenCircuitException)
            {
                // Circuit is open - complete gracefully without error
                observer.OnCompleted();
            }
            catch (Exception ex)
            {
                observer.OnError(ex);
            }
            finally
            {
                s_cloudConcurrency.Release();
            }
        });
    }

    /// <inheritdoc/>
    public IObservable<string> StreamReasoningContent(string prompt, CancellationToken ct = default)
    {
        return Observable.Create<string>(async (observer, token) =>
        {
            await s_cloudConcurrency.WaitAsync(token).ConfigureAwait(false);
            try
            {
                using JsonContent payload = JsonContent.Create(new
                {
                    model = _model,
                    prompt = _settings.Culture is { Length: > 0 } c ? $"Please answer in {c}. {prompt}" : prompt,
                    stream = true,
                    options = new
                    {
                        temperature = _settings.Temperature,
                        num_predict = _settings.MaxTokens > 0 ? _settings.MaxTokens : (int?)null
                    }
                });

                HttpResponseMessage response = await _resiliencePolicy.ExecuteAsync(
                    (innerToken) => _client.PostAsync("/api/generate", payload, innerToken),
                    token).ConfigureAwait(false);

                response.EnsureSuccessStatusCode();

                using Stream responseStream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
                using StreamReader reader = new StreamReader(responseStream);

                while (!reader.EndOfStream && !token.IsCancellationRequested)
                {
                    string? line = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    try
                    {
                        using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(line);
                        if (doc.RootElement.TryGetProperty("response", out System.Text.Json.JsonElement responseElement))
                        {
                            string? content = responseElement.GetString();
                            if (!string.IsNullOrEmpty(content))
                            {
                                observer.OnNext(content);
                            }
                        }

                        if (doc.RootElement.TryGetProperty("done", out System.Text.Json.JsonElement doneElement) && doneElement.GetBoolean())
                        {
                            observer.OnCompleted();
                            return;
                        }
                    }
                    catch (System.Text.Json.JsonException)
                    {
                        // Skip malformed JSON chunks
                        continue;
                    }
                }

                observer.OnCompleted();
            }
            catch (BrokenCircuitException)
            {
                // Circuit is open - complete gracefully without error
                observer.OnCompleted();
            }
            catch (Exception ex)
            {
                observer.OnError(ex);
            }
            finally
            {
                s_cloudConcurrency.Release();
            }
        });
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}