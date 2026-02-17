using System.Net.Http.Json;
using Polly;
using Polly.Retry;

namespace Ouroboros.Providers;

/// <summary>
/// Embedding provider for LiteLLM proxy using OpenAI-compatible /v1/embeddings endpoint.
/// Supports various embedding models proxied through LiteLLM.
/// Includes Polly exponential backoff retry policy to handle rate limiting.
/// </summary>
public sealed class LiteLLMEmbeddingModel : IEmbeddingModel
{
    private readonly HttpClient _client;
    private readonly string _model;
    private readonly DeterministicEmbeddingModel _fallback = new();
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

    /// <summary>
    /// Initializes a new instance of the <see cref="LiteLLMEmbeddingModel"/> class.
    /// </summary>
    /// <param name="endpoint">LiteLLM proxy endpoint URL.</param>
    /// <param name="apiKey">API key for authentication.</param>
    /// <param name="model">Model name (e.g., text-embedding-ada-002, nomic-embed-text).</param>
    public LiteLLMEmbeddingModel(string endpoint, string apiKey, string model)
    {
        if (string.IsNullOrWhiteSpace(endpoint)) throw new ArgumentException("Endpoint is required", nameof(endpoint));
        if (string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentException("API key is required", nameof(apiKey));

        _client = new HttpClient
        {
            BaseAddress = new Uri(endpoint.TrimEnd('/'), UriKind.Absolute),
        };
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        _model = model;

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
                    Console.WriteLine($"[LiteLLMEmbeddingModel] Retry {retryCount} after {timespan.TotalSeconds}s due to {outcome.Result?.StatusCode}");
                });
    }

    /// <inheritdoc/>
    public async Task<float[]> CreateEmbeddingsAsync(string input, CancellationToken ct = default)
    {
        try
        {
            // LiteLLM uses OpenAI-compatible /v1/embeddings endpoint
            using JsonContent payload = JsonContent.Create(new
            {
                model = _model,
                input = input,
            });

            HttpResponseMessage response = await _retryPolicy.ExecuteAsync(async () =>
            {
                return await _client.PostAsync("/v1/embeddings", payload, ct).ConfigureAwait(false);
            }).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            using System.Text.Json.JsonDocument doc = await System.Text.Json.JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false), cancellationToken: ct).ConfigureAwait(false);

            // OpenAI format: { "data": [{ "embedding": [...] }] }
            if (doc.RootElement.TryGetProperty("data", out System.Text.Json.JsonElement dataElement) &&
                dataElement.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (System.Text.Json.JsonElement item in dataElement.EnumerateArray())
                {
                    if (item.TryGetProperty("embedding", out System.Text.Json.JsonElement embeddingElement) &&
                        embeddingElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        List<float> floats = new List<float>();
                        foreach (System.Text.Json.JsonElement element in embeddingElement.EnumerateArray())
                        {
                            if (element.TryGetSingle(out float value))
                            {
                                floats.Add(value);
                            }
                            else if (element.TryGetDouble(out double dblValue))
                            {
                                floats.Add((float)dblValue);
                            }
                        }

                        if (floats.Count > 0)
                        {
                            return floats.ToArray();
                        }
                    }
                }
            }
        }
        catch
        {
            // LiteLLM proxy not reachable or error occurred → fall back to deterministic embedding
        }

        return await _fallback.CreateEmbeddingsAsync(input, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Disposes the HTTP client.
    /// </summary>
    public void Dispose()
    {
        _client?.Dispose();
    }
}