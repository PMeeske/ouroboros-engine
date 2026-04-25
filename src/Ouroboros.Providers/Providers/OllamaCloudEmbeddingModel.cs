using OllamaSharp;
using OllamaSharp.Models;
using Polly;
using Polly.Retry;

namespace Ouroboros.Providers;

/// <summary>
/// Embedding adapter specifically for Ollama Cloud API endpoints.
/// Uses OllamaSharp to call the Ollama embeddings API.
/// Includes Polly exponential backoff retry policy to handle transient failures.
/// </summary>
public sealed class OllamaCloudEmbeddingModel : IEmbeddingModel
{
    private readonly HttpClient _client;
    private readonly OllamaApiClient _ollamaClient;
    private readonly string _model;
    private readonly DeterministicEmbeddingModel _fallback = new();
    private readonly AsyncRetryPolicy _retryPolicy;

    public OllamaCloudEmbeddingModel(string endpoint, string apiKey, string model)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new ArgumentException("Endpoint is required", nameof(endpoint));
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("API key is required", nameof(apiKey));
        }

        _client = new HttpClient
        {
            BaseAddress = new Uri(endpoint.TrimEnd('/'), UriKind.Absolute),
        };
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        _ollamaClient = new OllamaApiClient(_client);
        _ollamaClient.SelectedModel = model;
        _model = model;

        // Create Polly retry policy with exponential backoff for transient failures
        _retryPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (exception, timespan, retryCount, context) =>
                {
                    System.Diagnostics.Trace.TraceInformation("[OllamaCloudEmbeddingModel] Retry {0} after {1}s due to {2}", retryCount, timespan.TotalSeconds, exception.GetType().Name);
                });
    }

    /// <inheritdoc/>
    public async Task<float[]> CreateEmbeddingsAsync(string input, CancellationToken ct = default)
    {
        try
        {
            EmbedResponse result = await _retryPolicy.ExecuteAsync(async () =>
            {
                EmbedResponse response = await _ollamaClient.EmbedAsync(
                    new EmbedRequest
                {
                    Model = _model,
                    Input = [input],
                }, ct).ConfigureAwait(false);
                return response;
            }).ConfigureAwait(false);

            if (result?.Embeddings is { Count: > 0 } embeddings && embeddings[0]?.Length > 0)
            {
                return [.. embeddings[0]];
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
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
