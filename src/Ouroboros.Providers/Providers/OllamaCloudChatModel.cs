using System.Reactive.Linq;
using OllamaSharp;
using OllamaSharp.Models;
using Polly;
using Polly.CircuitBreaker;
using Polly.Wrap;

namespace Ouroboros.Providers;

/// <summary>
/// HTTP client specifically designed for Ollama Cloud API endpoints.
/// Uses OllamaSharp for typed API access with /api/generate endpoint.
/// Includes Polly exponential backoff retry policy to handle rate limiting.
/// Supports thinking mode for models like DeepSeek R1 that emit &lt;think&gt; tags.
/// </summary>
public sealed class OllamaCloudChatModel : IStreamingThinkingChatModel, ICostAwareChatModel
{
    // Global concurrency limiter: prevents thundering herd when multiple model slots
    // (coder, reasoner, summarizer) all target the same cloud endpoint simultaneously.
    private static readonly SemaphoreSlim s_cloudConcurrency = new(2, 2);

    private readonly HttpClient _client;
    private readonly OllamaApiClient _ollamaClient;
    private readonly string _model;
    private readonly ChatRuntimeSettings _settings;
    private readonly AsyncPolicyWrap _resiliencePolicy;
    private readonly LlmCostTracker? _costTracker;

    /// <summary>
    /// Gets the cost tracker for this model instance.
    /// </summary>
    public LlmCostTracker? CostTracker => _costTracker;

    public OllamaCloudChatModel(string endpoint, string apiKey, string model, ChatRuntimeSettings? settings = null, LlmCostTracker? costTracker = null)
    {
        if (string.IsNullOrWhiteSpace(endpoint)) throw new ArgumentException("Endpoint is required", nameof(endpoint));
        if (string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentException("API key is required", nameof(apiKey));

        // Guard: embedding models (nomic-embed-text, mxbai-embed-large, etc.) cannot generate text.
        // Replace with the default chat model to prevent fallback noise in output.
        if (model.Contains("embed", StringComparison.OrdinalIgnoreCase)
            || model.Contains("nomic", StringComparison.OrdinalIgnoreCase))
        {
            System.Diagnostics.Trace.TraceWarning(
                "[OllamaCloudChatModel] Embedding model '{0}' used as chat model — reset to glm-5:cloud", model);
            model = "glm-5:cloud";
        }

        _client = new HttpClient
        {
            BaseAddress = new Uri(endpoint.TrimEnd('/'), UriKind.Absolute),
            Timeout = TimeSpan.FromMinutes(10) // Large models like DeepSeek 671B need longer timeouts
        };
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        _ollamaClient = new OllamaApiClient(_client);
        _ollamaClient.SelectedModel = model;

        _model = model;
        _settings = settings ?? new ChatRuntimeSettings();
        _costTracker = costTracker ?? new LlmCostTracker(model);

        // === ENHANCED POLLY RESILIENCE POLICY ===
        // OllamaSharp throws exceptions on failure (no HttpResponseMessage to inspect),
        // so the policy is exception-based rather than result-based.

        // 1. Retry policy with exponential backoff + jitter for rate limiting and server errors
        var retryPolicy = Policy
            .Handle<HttpRequestException>()        // Network failures and HTTP error status codes
            .Or<TaskCanceledException>(ex =>        // Only genuine HttpClient timeouts, NOT external cancellation.
                // HttpClient.Timeout fires as TaskCanceledException with InnerException=TimeoutException.
                // Deliberate cancellation (Racing mode, user Ctrl+C) produces a bare TCE without a TimeoutException.
                ex.InnerException is TimeoutException)
            .WaitAndRetryAsync(
                retryCount: 5,  // Increased from 3 for cloud instability
                sleepDurationProvider: retryAttempt =>
                {
                    // Exponential backoff with jitter to avoid thundering herd
                    var baseDelay = TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));
                    var jitter = TimeSpan.FromMilliseconds(System.Random.Shared.Next(0, 1000));
                    return baseDelay + jitter;
                },
                onRetry: (exception, timespan, retryCount, context) =>
                {
                    var reason = exception.Message;
                    System.Diagnostics.Trace.TraceInformation("[OllamaCloudChatModel] Retry {0}/5 after {1:F1}s due to {2}", retryCount, timespan.TotalSeconds, reason);
                });

        // 2. Circuit breaker - fail fast if service is consistently down
        var circuitBreakerPolicy = Policy
            .Handle<HttpRequestException>()
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: 5,  // Open circuit after 5 consecutive failures
                durationOfBreak: TimeSpan.FromSeconds(30),  // Stay open for 30s before trying again
                onBreak: (exception, breakDelay) =>
                {
                    System.Diagnostics.Trace.TraceWarning("[OllamaCloudChatModel] Circuit OPEN for {0}s - service appears down", breakDelay.TotalSeconds);
                },
                onReset: () =>
                {
                    System.Diagnostics.Trace.TraceInformation("[OllamaCloudChatModel] Circuit CLOSED - service recovered");
                },
                onHalfOpen: () =>
                {
                    System.Diagnostics.Trace.TraceInformation("[OllamaCloudChatModel] Circuit HALF-OPEN - testing service...");
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
            string finalPrompt = _settings.Culture is { Length: > 0 } c ? $"Please answer in {c}. {prompt}" : prompt;

            var result = await _resiliencePolicy.ExecuteAsync(async (innerCt) =>
            {
                string response = "";
                int promptTokens = 0, outputTokens = 0;

                await foreach (var chunk in _ollamaClient.GenerateAsync(new GenerateRequest
                {
                    Model = _model,
                    Prompt = finalPrompt,
                    Stream = false,
                    Options = new RequestOptions
                    {
                        Temperature = (float)_settings.Temperature,
                        NumPredict = _settings.MaxTokens > 0 ? _settings.MaxTokens : null
                    }
                }, innerCt).ConfigureAwait(false))
                {
                    response += chunk.Response;

                    // Extract token counts from the final (done=true) chunk.
                    // OllamaSharp yields GenerateDoneResponseStream for the final item.
                    if (chunk.Done && chunk is GenerateDoneResponseStream doneChunk)
                    {
                        promptTokens = doneChunk.PromptEvalCount;
                        outputTokens = doneChunk.EvalCount;
                    }
                }

                _costTracker?.EndRequest(promptTokens, outputTokens);
                return response;
            }, ct).ConfigureAwait(false);

            return !string.IsNullOrEmpty(result) ? result : $"[ollama-cloud-fallback:{_model}]";
        }
        catch (BrokenCircuitException)
        {
            // Circuit is open - service is down, fail fast without spamming logs
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Deliberate cancellation (e.g. Racing mode found a winner) — not an error, don't log
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            System.Diagnostics.Trace.TraceWarning("[OllamaCloudChatModel] Error: {0}: {1}", ex.GetType().Name, ex.Message);
        }
        finally
        {
            s_cloudConcurrency.Release();
        }
        return $"[ollama-cloud-fallback:{_model}]";
    }

    /// <inheritdoc/>
    public async Task<ThinkingResponse> GenerateWithThinkingAsync(string prompt, CancellationToken ct = default)
    {
        string rawText = await GenerateTextAsync(prompt, ct).ConfigureAwait(false);
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
                string finalPrompt = _settings.Culture is { Length: > 0 } c ? $"Please answer in {c}. {prompt}" : prompt;

                await _resiliencePolicy.ExecuteAsync(async (innerToken) =>
                {
                    bool inThinking = false;
                    StringBuilder buffer = new();

                    await foreach (var chunk in _ollamaClient.GenerateAsync(new GenerateRequest
                    {
                        Model = _model,
                        Prompt = finalPrompt,
                        Stream = true,
                        Options = new RequestOptions
                        {
                            Temperature = (float)_settings.Temperature,
                            NumPredict = _settings.MaxTokens > 0 ? _settings.MaxTokens : null
                        }
                    }, innerToken).ConfigureAwait(false))
                    {
                        string? content = chunk.Response;
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

                        if (chunk.Done)
                        {
                            // Flush any remaining buffer
                            if (buffer.Length > 0)
                                observer.OnNext((inThinking, buffer.ToString()));
                            break;
                        }
                    }
                }, token).ConfigureAwait(false);

                observer.OnCompleted();
            }
            catch (BrokenCircuitException)
            {
                // Circuit is open - complete gracefully without error
                observer.OnCompleted();
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (ex is not OperationCanceledException)
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
                string finalPrompt = _settings.Culture is { Length: > 0 } c ? $"Please answer in {c}. {prompt}" : prompt;

                await _resiliencePolicy.ExecuteAsync(async (innerToken) =>
                {
                    await foreach (var chunk in _ollamaClient.GenerateAsync(new GenerateRequest
                    {
                        Model = _model,
                        Prompt = finalPrompt,
                        Stream = true,
                        Options = new RequestOptions
                        {
                            Temperature = (float)_settings.Temperature,
                            NumPredict = _settings.MaxTokens > 0 ? _settings.MaxTokens : null
                        }
                    }, innerToken).ConfigureAwait(false))
                    {
                        string? content = chunk.Response;
                        if (!string.IsNullOrEmpty(content))
                        {
                            observer.OnNext(content);
                        }

                        if (chunk.Done)
                        {
                            break;
                        }
                    }
                }, token).ConfigureAwait(false);

                observer.OnCompleted();
            }
            catch (BrokenCircuitException)
            {
                // Circuit is open - complete gracefully without error
                observer.OnCompleted();
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (ex is not OperationCanceledException)
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
        _ollamaClient?.Dispose();
        _client?.Dispose();
    }
}
