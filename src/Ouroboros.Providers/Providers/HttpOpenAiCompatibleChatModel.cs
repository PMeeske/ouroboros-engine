using System.Net.Http.Json;
using Polly;
using Polly.Retry;

namespace Ouroboros.Providers;

/// <summary>
/// Shallow HTTP client that mimics an OpenAI-compatible JSON API. We intentionally
/// keep it permissive – if the call fails we simply echo the prompt with context.
/// Uses Polly for exponential backoff retry policy to handle rate limiting.
/// </summary>
public sealed class HttpOpenAiCompatibleChatModel : Ouroboros.Abstractions.Core.IChatCompletionModel, ICostAwareChatModel
{
    private readonly HttpClient _client;
    private readonly string _model;
    private readonly ChatRuntimeSettings _settings;
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
    private readonly LlmCostTracker? _costTracker;

    /// <summary>Gets the cost tracker for this model instance.</summary>
    public LlmCostTracker? CostTracker => _costTracker;

    public HttpOpenAiCompatibleChatModel(string endpoint, string apiKey, string model, ChatRuntimeSettings? settings = null, LlmCostTracker? costTracker = null)
    {
        if (string.IsNullOrWhiteSpace(endpoint)) throw new ArgumentException("Endpoint is required", nameof(endpoint));
        if (string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentException("API key is required", nameof(apiKey));

        _client = new HttpClient
        {
            BaseAddress = new Uri(endpoint, UriKind.Absolute)
        };
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        _model = model;
        _settings = settings ?? new ChatRuntimeSettings();
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
                    Console.WriteLine($"[HttpOpenAiCompatibleChatModel] Retry {retryCount} after {timespan.TotalSeconds}s due to {outcome.Result?.StatusCode}");
                });
    }

    /// <inheritdoc/>
    public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        _costTracker?.StartRequest();
        try
        {
            using JsonContent payload = JsonContent.Create(new
            {
                model = _model,
                temperature = _settings.Temperature,
                max_output_tokens = _settings.MaxTokens,
                input = _settings.Culture is { Length: > 0 } c ? $"Please answer in {c}. {prompt}" : prompt
            });

            HttpResponseMessage response = await _retryPolicy.ExecuteAsync(async () =>
            {
                return await _client.PostAsync("/v1/responses", payload, ct).ConfigureAwait(false);
            }).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();
            Dictionary<string, object?>? json = await response.Content.ReadFromJsonAsync<Dictionary<string, object?>>(cancellationToken: ct).ConfigureAwait(false);
            if (json is not null && json.TryGetValue("output_text", out object? text) && text is string s)
            {
                // Try to extract token counts if available
                int inputTokens = 0, outputTokens = 0;
                if (json.TryGetValue("usage", out var usage) && usage is System.Text.Json.JsonElement usageEl)
                {
                    if (usageEl.TryGetProperty("prompt_tokens", out var pt))
                        inputTokens = pt.GetInt32();
                    if (usageEl.TryGetProperty("completion_tokens", out var ct2))
                        outputTokens = ct2.GetInt32();
                }
                _costTracker?.EndRequest(inputTokens, outputTokens);
                return s;
            }
        }
        catch
        {
            // Remote backend not reachable → fall back to indicating failure.
        }
        _costTracker?.EndRequest(0, 0);
        return $"[remote-fallback:{_model}] {prompt}";
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}