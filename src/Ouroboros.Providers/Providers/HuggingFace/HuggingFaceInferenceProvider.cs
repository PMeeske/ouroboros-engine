// <copyright file="HuggingFaceInferenceProvider.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Providers.HuggingFace;

using System.Net.Http.Json;
using Ouroboros.Abstractions.Monads;

/// <summary>
/// Provider for the HuggingFace Inference API.
/// Supports text classification, zero-shot classification, embeddings,
/// and text generation via HuggingFace-hosted models. Returns
/// <see cref="Result{TValue, TError}"/> to avoid exceptions on API failures.
/// </summary>
public sealed class HuggingFaceInferenceProvider : IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttpClient;
    private const string BaseUrl = "https://api-inference.huggingface.co/models/";

    /// <summary>
    /// Initializes a new instance of the <see cref="HuggingFaceInferenceProvider"/> class.
    /// </summary>
    /// <param name="apiToken">HuggingFace API bearer token.</param>
    /// <param name="httpClient">
    /// Optional pre-configured HttpClient. If null, a new client is created and owned.
    /// </param>
    public HuggingFaceInferenceProvider(string apiToken, HttpClient? httpClient = null)
    {
        ArgumentNullException.ThrowIfNull(apiToken);

        _ownsHttpClient = httpClient is null;
        _http = httpClient ?? new HttpClient();
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiToken);
    }

    /// <summary>
    /// Performs text classification (e.g., sentiment analysis, emotion detection).
    /// </summary>
    /// <param name="modelId">The HuggingFace model identifier (e.g., "SamLowe/roberta-base-go_emotions").</param>
    /// <param name="text">The text to classify.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result containing classification scores or an error message.</returns>
    public async Task<Result<List<ClassificationResult>, string>> ClassifyAsync(
        string modelId, string text, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(modelId);
        ArgumentNullException.ThrowIfNull(text);

        try
        {
            var response = await _http.PostAsJsonAsync(
                $"{BaseUrl}{modelId}",
                new { inputs = text },
                ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return Result<List<ClassificationResult>, string>.Failure(
                    $"HF API error: {response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var results = ParseClassificationResponse(json);
            return Result<List<ClassificationResult>, string>.Success(results);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result<List<ClassificationResult>, string>.Failure(
                $"HF request failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Performs zero-shot classification with candidate labels.
    /// </summary>
    /// <param name="modelId">The HuggingFace model identifier (e.g., "facebook/bart-large-mnli").</param>
    /// <param name="text">The text to classify.</param>
    /// <param name="candidateLabels">Labels to classify against.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result containing zero-shot scores or an error message.</returns>
    public async Task<Result<ZeroShotResult, string>> ZeroShotClassifyAsync(
        string modelId, string text, List<string> candidateLabels,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(modelId);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(candidateLabels);

        try
        {
            var payload = new
            {
                inputs = text,
                parameters = new { candidate_labels = candidateLabels },
            };

            var response = await _http.PostAsJsonAsync(
                $"{BaseUrl}{modelId}", payload, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return Result<ZeroShotResult, string>.Failure(
                    $"HF API error: {response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var result = ParseZeroShotResponse(json);
            return Result<ZeroShotResult, string>.Success(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result<ZeroShotResult, string>.Failure(
                $"HF request failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Generates text embeddings from a model.
    /// </summary>
    /// <param name="modelId">The HuggingFace model identifier (e.g., "sentence-transformers/all-MiniLM-L6-v2").</param>
    /// <param name="text">The text to embed.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result containing the embedding vector or an error message.</returns>
    public async Task<Result<float[], string>> EmbedAsync(
        string modelId, string text, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(modelId);
        ArgumentNullException.ThrowIfNull(text);

        try
        {
            var response = await _http.PostAsJsonAsync(
                $"{BaseUrl}{modelId}",
                new { inputs = text },
                ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return Result<float[], string>.Failure(
                    $"HF API error: {response.StatusCode}");
            }

            var embedding = await response.Content.ReadFromJsonAsync<float[]>(ct)
                .ConfigureAwait(false);
            return Result<float[], string>.Success(embedding ?? []);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result<float[], string>.Failure(
                $"HF embed failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Generates text from a prompt using a HuggingFace text generation model.
    /// </summary>
    /// <param name="modelId">The HuggingFace model identifier.</param>
    /// <param name="prompt">The input prompt.</param>
    /// <param name="maxTokens">Maximum number of new tokens to generate.</param>
    /// <param name="temperature">Sampling temperature (higher = more creative).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result containing the generated text or an error message.</returns>
    public async Task<Result<string, string>> GenerateAsync(
        string modelId, string prompt, int maxTokens = 256,
        double temperature = 0.7, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(modelId);
        ArgumentNullException.ThrowIfNull(prompt);

        try
        {
            var payload = new
            {
                inputs = prompt,
                parameters = new
                {
                    max_new_tokens = maxTokens,
                    temperature,
                },
            };

            var response = await _http.PostAsJsonAsync(
                $"{BaseUrl}{modelId}", payload, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return Result<string, string>.Failure(
                    $"HF API error: {response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var text = ParseGenerationResponse(json);
            return Result<string, string>.Success(text);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure(
                $"HF generate failed: {ex.Message}");
        }
    }

    /// <summary>A single classification result with label and confidence score.</summary>
    /// <param name="Label">The classification label.</param>
    /// <param name="Score">Confidence score [0, 1].</param>
    public sealed record ClassificationResult(string Label, double Score);

    /// <summary>Result of a zero-shot classification.</summary>
    /// <param name="TopLabel">The highest-scoring label.</param>
    /// <param name="TopScore">The score of the top label.</param>
    /// <param name="LabelScores">All label scores.</param>
    public sealed record ZeroShotResult(
        string TopLabel,
        double TopScore,
        Dictionary<string, double> LabelScores);

    /// <summary>
    /// Parses the HuggingFace classification API response.
    /// HF returns [[{label, score}, ...]] for classification pipelines.
    /// </summary>
    private static List<ClassificationResult> ParseClassificationResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var results = new List<ClassificationResult>();

            var root = doc.RootElement;
            var array = root.ValueKind == JsonValueKind.Array
                        && root.GetArrayLength() > 0
                        && root[0].ValueKind == JsonValueKind.Array
                ? root[0]
                : root;

            foreach (var item in array.EnumerateArray())
            {
                var label = item.GetProperty("label").GetString() ?? string.Empty;
                var score = item.GetProperty("score").GetDouble();
                results.Add(new ClassificationResult(label, score));
            }

            return results;
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Parses the HuggingFace zero-shot classification response.
    /// Format: { labels: [...], scores: [...] }.
    /// </summary>
    private static ZeroShotResult ParseZeroShotResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var labelArray = root.GetProperty("labels")
                .EnumerateArray()
                .Select(e => e.GetString() ?? string.Empty)
                .ToList();
            var scoreArray = root.GetProperty("scores")
                .EnumerateArray()
                .Select(e => e.GetDouble())
                .ToList();

            var dict = new Dictionary<string, double>();
            for (int i = 0; i < Math.Min(labelArray.Count, scoreArray.Count); i++)
            {
                dict[labelArray[i]] = scoreArray[i];
            }

            return new ZeroShotResult(
                labelArray.FirstOrDefault() ?? string.Empty,
                scoreArray.FirstOrDefault(),
                dict);
        }
        catch
        {
            return new ZeroShotResult(string.Empty, 0, new Dictionary<string, double>());
        }
    }

    /// <summary>
    /// Parses the HuggingFace text generation response.
    /// Format: [{ generated_text: "..." }] or { generated_text: "..." }.
    /// </summary>
    private static string ParseGenerationResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
            {
                return root[0].GetProperty("generated_text").GetString() ?? string.Empty;
            }

            return root.GetProperty("generated_text").GetString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _http.Dispose();
        }
    }
}
