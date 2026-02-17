using System.Net.Http.Json;
using Polly;
using Polly.Retry;

namespace Ouroboros.Providers;

/// <summary>
/// Embedding adapter specifically for Ollama Cloud API endpoints.
/// Uses Ollama's native /api/embeddings endpoint and JSON format.
/// Includes Polly exponential backoff retry policy to handle rate limiting.
/// </summary>
public sealed class OllamaCloudEmbeddingModel : IEmbeddingModel
{
    private readonly HttpClient _client;
    private readonly string _model;
    private readonly DeterministicEmbeddingModel _fallback = new();
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

    public OllamaCloudEmbeddingModel(string endpoint, string apiKey, string model)
    {
        if (string.IsNullOrWhiteSpace(endpoint)) throw new ArgumentException("Endpoint is required", nameof(endpoint));
        if (string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentException("API key is required", nameof(apiKey));

        _client = new HttpClient
        {
            BaseAddress = new Uri(endpoint.TrimEnd('/'), UriKind.Absolute)
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
                    Console.WriteLine($"[OllamaCloudEmbeddingModel] Retry {retryCount} after {timespan.TotalSeconds}s due to {outcome.Result?.StatusCode}");
                });
    }

    /// <inheritdoc/>
    public async Task<float[]> CreateEmbeddingsAsync(string input, CancellationToken ct = default)
    {
        try
        {
            // Use Ollama's native /api/embeddings endpoint and JSON format
            using JsonContent payload = JsonContent.Create(new
            {
                model = _model,
                prompt = input
            });

            HttpResponseMessage response = await _retryPolicy.ExecuteAsync(async () =>
            {
                return await _client.PostAsync("/api/embeddings", payload, ct).ConfigureAwait(false);
            }).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            Dictionary<string, object?>? json = await response.Content.ReadFromJsonAsync<Dictionary<string, object?>>(cancellationToken: ct).ConfigureAwait(false);
            if (json is not null && json.TryGetValue("embedding", out object? embeddingValue))
            {
                if (embeddingValue is System.Text.Json.JsonElement jsonElement && jsonElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    List<float> floats = new List<float>();
                    foreach (System.Text.Json.JsonElement element in jsonElement.EnumerateArray())
                    {
                        if (element.TryGetSingle(out float value))
                        {
                            floats.Add(value);
                        }
                    }
                    if (floats.Count > 0)
                    {
                        return floats.ToArray();
                    }
                }
            }
        }
        catch
        {
            // Remote Ollama Cloud not reachable → fall back to deterministic embedding
        }
        return await _fallback.CreateEmbeddingsAsync(input, ct).ConfigureAwait(false);
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}