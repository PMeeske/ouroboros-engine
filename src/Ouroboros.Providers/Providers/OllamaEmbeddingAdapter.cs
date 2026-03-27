using Microsoft.Extensions.AI;
using OllamaSharp;
using OllamaSharp.Models;
using Ouroboros.Abstractions.Core;

namespace Ouroboros.Providers;

/// <summary>
/// Adapter that wraps the Ollama embedding API via OllamaSharp when available. If the daemon
/// cannot be reached we fall back to deterministic embeddings.
/// Implements <see cref="IEmbeddingGeneratorBridge"/> for zero-overhead MEAI interop
/// (OllamaApiClient natively implements <see cref="IEmbeddingGenerator{String, Embedding}"/>).
/// </summary>
public sealed class OllamaEmbeddingAdapter : IEmbeddingModel, IEmbeddingGeneratorBridge
{
    private readonly OllamaApiClient _client;
    private readonly string _modelName;
    private readonly DeterministicEmbeddingModel _fallback = new();

    public OllamaEmbeddingAdapter(OllamaApiClient client, string modelName)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName);
        _client = client;
        _modelName = modelName;
    }

    /// <inheritdoc/>
    public async Task<float[]> CreateEmbeddingsAsync(string input, CancellationToken ct = default)
    {
        string safeInput = SanitizeForEmbedding(input);

        try
        {
            EmbedResponse response = await _client.EmbedAsync(new EmbedRequest
            {
                Model = _modelName,
                Input = [safeInput]
            }, ct).ConfigureAwait(false);

            if (response?.Embeddings is { Count: > 0 } embeddings
                && embeddings[0] is { Length: > 0 } firstVector)
            {
                return firstVector;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // Genuine caller cancellation — propagate
        }
        catch (OperationCanceledException)
        {
            // HttpClient timeout (not caller cancellation) — fall through to fallback
        }
        catch (HttpRequestException)
        {
            // OllamaSharp communication error — fall through to fallback
        }
        catch (IOException)
        {
            // Transport-level failure — fall through to fallback
        }

        // Use deterministic fallback (hash-based embedding)
        return await _fallback.CreateEmbeddingsAsync(safeInput, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Sanitizes text for embedding by removing problematic characters.
    /// </summary>
    private static string SanitizeForEmbedding(string? text, int maxLength = 6000)
    {
        if (string.IsNullOrEmpty(text)) return "empty";

        // First pass: build clean string, skipping problematic characters
        var sb = new System.Text.StringBuilder(Math.Min(text.Length, maxLength));
        foreach (char c in text)
        {
            if (sb.Length >= maxLength) break;

            // Skip control characters (except newline/tab), surrogates, and null
            if (c == '\0') continue;
            if (char.IsSurrogate(c)) continue;
            if (char.IsControl(c) && c != '\n' && c != '\r' && c != '\t') continue;

            // Skip BMP symbols that commonly cause encoding issues with embedding APIs
            if (c >= 0x2600 && c <= 0x26FF) continue;   // Misc symbols
            if (c >= 0x2700 && c <= 0x27BF) continue;   // Dingbats

            sb.Append(c);
        }

        if (sb.Length == 0) return "empty";

        // Second pass: ensure valid UTF-8 round-trip
        try
        {
            var utf8 = System.Text.Encoding.UTF8;
            byte[] bytes = utf8.GetBytes(sb.ToString());
            // Limit byte size to prevent buffer overflow (4KB safe limit)
            if (bytes.Length > 4000)
            {
                // Truncate at byte level, then decode back
                bytes = bytes[..4000];
                // Find last valid UTF-8 sequence
                int lastValid = 4000;
                while (lastValid > 0 && (bytes[lastValid - 1] & 0xC0) == 0x80)
                    lastValid--;
                if (lastValid > 0 && lastValid < 4000)
                    bytes = bytes[..lastValid];
                return utf8.GetString(bytes);
            }
            return sb.ToString();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Ultimate fallback: ASCII only
            var ascii = new System.Text.StringBuilder();
            foreach (char c in sb.ToString())
            {
                if (c < 128 && ascii.Length < 2000)
                    ascii.Append(c);
            }
            return ascii.Length > 0 ? ascii.ToString() : "empty";
        }
    }

    /// <inheritdoc/>
    public IEmbeddingGenerator<string, Embedding<float>> GetEmbeddingGenerator() => _client;
}
