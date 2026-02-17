using System.Net.Http.Json;
using System.Reactive.Linq;
using Polly;
using Polly.Retry;

namespace Ouroboros.Providers;

/// <summary>
/// Base class for OpenAI-compatible chat models that use /v1/chat/completions endpoint.
/// Provides shared implementation for request/response handling and streaming.
/// Supports thinking mode via reasoning_content field (DeepSeek, o1, etc.) and thinking tags.
/// </summary>
public abstract class OpenAiCompatibleChatModelBase : IStreamingThinkingChatModel, ICostAwareChatModel
{
    private readonly HttpClient _client;
    private readonly string _model;
    private readonly ChatRuntimeSettings _settings;
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
    private readonly string _providerName;
    private readonly LlmCostTracker? _costTracker;

    /// <summary>Gets the model name.</summary>
    protected string ModelName => _model;

    /// <summary>Gets the settings.</summary>
    protected ChatRuntimeSettings Settings => _settings;

    /// <summary>Gets the cost tracker for this model instance.</summary>
    public LlmCostTracker? CostTracker => _costTracker;

    protected OpenAiCompatibleChatModelBase(string endpoint, string apiKey, string model, string providerName, ChatRuntimeSettings? settings = null, LlmCostTracker? costTracker = null)
    {
        if (string.IsNullOrWhiteSpace(endpoint)) throw new ArgumentException("Endpoint is required", nameof(endpoint));
        if (string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentException("API key is required", nameof(apiKey));

        _client = new HttpClient
        {
            BaseAddress = new Uri(endpoint.TrimEnd('/'), UriKind.Absolute)
        };
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        _model = model;
        _settings = settings ?? new ChatRuntimeSettings();
        _providerName = providerName;
        _costTracker = costTracker ?? new LlmCostTracker(model);

        // Create Polly retry policy with exponential backoff for rate limiting (429) and server errors (5xx)
        _retryPolicy = Policy
            .HandleResult<HttpResponseMessage>(r =>
                (int)r.StatusCode == 429 || // Too Many Requests
                (int)r.StatusCode >= 500)   // Server errors
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    Console.WriteLine($"[{_providerName}] Retry {retryCount} after {timespan.TotalSeconds}s due to {outcome.Result?.StatusCode}");
                });
    }

    /// <inheritdoc/>
    public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        var response = await GenerateWithThinkingAsync(prompt, ct);
        // Return combined thinking + content for backward compatibility if thinking is present
        return response.HasThinking ? response.ToFormattedString() : response.Content;
    }

    /// <inheritdoc/>
    public async Task<ThinkingResponse> GenerateWithThinkingAsync(string prompt, CancellationToken ct = default)
    {
        _costTracker?.StartRequest();
        try
        {
            // Use OpenAI-compatible chat completions format
            using JsonContent payload = JsonContent.Create(new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "user", content = _settings.Culture is { Length: > 0 } c ? $"Please answer in {c}. {prompt}" : prompt }
                },
                temperature = _settings.Temperature,
                max_tokens = _settings.MaxTokens > 0 ? _settings.MaxTokens : (int?)null
            });

            HttpResponseMessage response = await _retryPolicy.ExecuteAsync(async () =>
            {
                return await _client.PostAsync("/v1/chat/completions", payload, ct).ConfigureAwait(false);
            }).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            string jsonString = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(jsonString);

            string? reasoningContent = null;
            string? content = null;
            int? reasoningTokens = null;
            int? contentTokens = null;
            int promptTokens = 0;
            int completionTokens = 0;

            // Try to extract usage info
            if (doc.RootElement.TryGetProperty("usage", out var usageElement))
            {
                if (usageElement.TryGetProperty("reasoning_tokens", out var rtElement))
                    reasoningTokens = rtElement.GetInt32();
                if (usageElement.TryGetProperty("completion_tokens", out var ctElement))
                {
                    contentTokens = ctElement.GetInt32();
                    completionTokens = contentTokens.Value;
                }
                if (usageElement.TryGetProperty("prompt_tokens", out var ptElement))
                    promptTokens = ptElement.GetInt32();
            }

            // Record cost tracking
            _costTracker?.EndRequest(promptTokens, completionTokens);

            // Extract content from OpenAI response format: choices[0].message
            if (doc.RootElement.TryGetProperty("choices", out System.Text.Json.JsonElement choicesElement) &&
                choicesElement.ValueKind == System.Text.Json.JsonValueKind.Array &&
                choicesElement.GetArrayLength() > 0)
            {
                System.Text.Json.JsonElement firstChoice = choicesElement[0];
                if (firstChoice.TryGetProperty("message", out System.Text.Json.JsonElement messageElement))
                {
                    // Extract reasoning_content if present (DeepSeek, o1, etc.)
                    if (messageElement.TryGetProperty("reasoning_content", out System.Text.Json.JsonElement reasoningElement) &&
                        reasoningElement.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        reasoningContent = reasoningElement.GetString();
                    }

                    // Extract standard content
                    if (messageElement.TryGetProperty("content", out System.Text.Json.JsonElement contentElement) &&
                        contentElement.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        content = contentElement.GetString();
                    }
                }
            }

            // If we have explicit reasoning_content, return structured response
            if (!string.IsNullOrEmpty(reasoningContent))
            {
                return new ThinkingResponse(reasoningContent, content ?? string.Empty, reasoningTokens, contentTokens);
            }

            // Otherwise, try to parse thinking tags from content
            if (!string.IsNullOrEmpty(content))
            {
                var parsed = ThinkingResponse.FromRawText(content);
                return parsed with { ThinkingTokens = reasoningTokens, ContentTokens = contentTokens };
            }

            return new ThinkingResponse(null, string.Empty);
        }
        catch
        {
            // Remote endpoint not reachable → fall back to indicating failure.
        }

        return new ThinkingResponse(null, GetFallbackMessage(prompt));
    }

    /// <inheritdoc/>
    public IObservable<(bool IsThinking, string Chunk)> StreamWithThinkingAsync(string prompt, CancellationToken ct = default)
    {
        return Observable.Create<(bool IsThinking, string Chunk)>(async (observer, token) =>
        {
            try
            {
                using JsonContent payload = JsonContent.Create(new
                {
                    model = _model,
                    messages = new[]
                    {
                        new { role = "user", content = _settings.Culture is { Length: > 0 } c ? $"Please answer in {c}. {prompt}" : prompt }
                    },
                    temperature = _settings.Temperature,
                    max_tokens = _settings.MaxTokens > 0 ? _settings.MaxTokens : (int?)null,
                    stream = true
                });

                HttpResponseMessage response = await _retryPolicy.ExecuteAsync(async () =>
                {
                    return await _client.PostAsync("/v1/chat/completions", payload, token).ConfigureAwait(false);
                }).ConfigureAwait(false);

                response.EnsureSuccessStatusCode();

                using Stream responseStream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
                using StreamReader reader = new StreamReader(responseStream);

                bool inThinkingFromTag = false;
                StringBuilder tagBuffer = new();

                while (!reader.EndOfStream && !token.IsCancellationRequested)
                {
                    string? line = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ")) continue;

                    string jsonData = line.Substring(6).Trim();
                    if (jsonData == "[DONE]")
                    {
                        // Flush any remaining buffer
                        if (tagBuffer.Length > 0)
                            observer.OnNext((inThinkingFromTag, tagBuffer.ToString()));
                        observer.OnCompleted();
                        return;
                    }

                    try
                    {
                        using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(jsonData);
                        if (doc.RootElement.TryGetProperty("choices", out System.Text.Json.JsonElement choicesElement) &&
                            choicesElement.ValueKind == System.Text.Json.JsonValueKind.Array &&
                            choicesElement.GetArrayLength() > 0)
                        {
                            System.Text.Json.JsonElement delta = choicesElement[0];
                            if (delta.TryGetProperty("delta", out System.Text.Json.JsonElement deltaElement))
                            {
                                // Check for reasoning_content (structured thinking from API)
                                if (deltaElement.TryGetProperty("reasoning_content", out System.Text.Json.JsonElement reasoningElement))
                                {
                                    string? reasoning = reasoningElement.GetString();
                                    if (!string.IsNullOrEmpty(reasoning))
                                    {
                                        observer.OnNext((true, reasoning));
                                    }
                                    continue;
                                }

                                // Check for content
                                if (deltaElement.TryGetProperty("content", out System.Text.Json.JsonElement contentElement))
                                {
                                    string? content = contentElement.GetString();
                                    if (!string.IsNullOrEmpty(content))
                                    {
                                        // Check for thinking tags in content
                                        tagBuffer.Append(content);
                                        string bufferStr = tagBuffer.ToString();

                                        if (!inThinkingFromTag && bufferStr.Contains("<think>", StringComparison.OrdinalIgnoreCase))
                                        {
                                            int idx = bufferStr.IndexOf("<think>", StringComparison.OrdinalIgnoreCase);
                                            string beforeTag = bufferStr[..idx];
                                            if (!string.IsNullOrEmpty(beforeTag))
                                                observer.OnNext((false, beforeTag));

                                            tagBuffer.Clear();
                                            tagBuffer.Append(bufferStr[(idx + 7)..]);
                                            inThinkingFromTag = true;
                                            continue;
                                        }

                                        if (inThinkingFromTag && bufferStr.Contains("</think>", StringComparison.OrdinalIgnoreCase))
                                        {
                                            int idx = bufferStr.IndexOf("</think>", StringComparison.OrdinalIgnoreCase);
                                            string thinkingContent = bufferStr[..idx];
                                            if (!string.IsNullOrEmpty(thinkingContent))
                                                observer.OnNext((true, thinkingContent));

                                            tagBuffer.Clear();
                                            tagBuffer.Append(bufferStr[(idx + 8)..]);
                                            inThinkingFromTag = false;
                                            continue;
                                        }

                                        // Emit with current state
                                        observer.OnNext((inThinkingFromTag, content));
                                        tagBuffer.Clear();
                                    }
                                }
                            }
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
            catch (Exception ex)
            {
                observer.OnError(ex);
            }
        });
    }

    /// <inheritdoc/>
    public System.IObservable<string> StreamReasoningContent(string prompt, CancellationToken ct = default)
    {
        // Flatten the thinking stream to just emit all chunks (both thinking and content)
        return StreamWithThinkingAsync(prompt, ct).Select(tuple => tuple.Chunk);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _client?.Dispose();
    }

    /// <summary>
    /// Gets the fallback message to return when the API call fails.
    /// </summary>
    protected virtual string GetFallbackMessage(string prompt)
    {
        return $"[{_providerName.ToLowerInvariant()}-fallback:{_model}] {prompt}";
    }
}