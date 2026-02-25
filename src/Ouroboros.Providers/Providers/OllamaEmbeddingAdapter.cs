using LangChain.Providers.Ollama;

namespace Ouroboros.Providers;

/// <summary>
/// Adapter that wraps the Ollama embedding API when available. If the daemon
/// cannot be reached we fall back to deterministic embeddings.
/// </summary>
public sealed class OllamaEmbeddingAdapter : IEmbeddingModel
{
    private readonly OllamaEmbeddingModel _model;
    private readonly DeterministicEmbeddingModel _fallback = new();

    public OllamaEmbeddingAdapter(OllamaEmbeddingModel model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
    }

    /// <inheritdoc/>
    public async Task<float[]> CreateEmbeddingsAsync(string input, CancellationToken ct = default)
    {
        string safeInput = SanitizeForEmbedding(input);

        try
        {
            LangChain.Providers.EmbeddingResponse response = await _model.CreateEmbeddingsAsync(safeInput, cancellationToken: ct).ConfigureAwait(false);
            if (TryExtractEmbedding(response, out float[]? vector))
            {
                return vector;
            }
        }
        catch
        {
            // LangChain encoding error - fall through to fallback
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

            // Skip emoji and other high Unicode (above BMP can cause issues)
            if (c > 0xFFFF || (c >= 0xD800 && c <= 0xDFFF)) continue;

            // Skip common problematic ranges
            if (c >= 0x1F600 && c <= 0x1F64F) continue; // Emoticons
            if (c >= 0x1F300 && c <= 0x1F5FF) continue; // Misc symbols
            if (c >= 0x1F680 && c <= 0x1F6FF) continue; // Transport
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
        catch
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

    private static bool TryExtractEmbedding(object? response, out float[] embedding)
    {
        embedding = Array.Empty<float>();
        if (response is null)
        {
            return false;
        }

        switch (response)
        {
            case float[] floats:
                embedding = floats;
                return true;
            case IReadOnlyList<float> roList:
                embedding = roList.ToArray();
                return true;
            case IEnumerable<float> enumerable:
                embedding = enumerable.ToArray();
                return true;
        }

        Type type = response.GetType();

        System.Reflection.PropertyInfo? vectorProperty = type.GetProperty("Vector");
        if (vectorProperty?.GetValue(response) is IEnumerable<float> vectorEnum)
        {
            embedding = vectorEnum.ToArray();
            return embedding.Length > 0;
        }

        System.Reflection.PropertyInfo? embeddingsProperty = type.GetProperty("Embeddings");
        if (embeddingsProperty?.GetValue(response) is System.Collections.IEnumerable embeddingsEnum)
        {
            foreach (object? entry in embeddingsEnum)
            {
                if (entry is float[] entryArray)
                {
                    embedding = entryArray;
                    return embedding.Length > 0;
                }

                if (entry is IEnumerable<float> direct)
                {
                    embedding = direct.ToArray();
                    if (embedding.Length > 0)
                    {
                        return true;
                    }
                }
                else if (entry is { })
                {
                    Type entryType = entry.GetType();
                    IEnumerable<float>? vectorInner = entryType.GetProperty("Vector")?.GetValue(entry) as IEnumerable<float>;
                    if (vectorInner is not null)
                    {
                        embedding = vectorInner.ToArray();
                        return embedding.Length > 0;
                    }

                    IEnumerable<float>? inner = entryType.GetProperty("Embedding")?.GetValue(entry) as IEnumerable<float>;
                    if (inner is not null)
                    {
                        embedding = inner.ToArray();
                        return embedding.Length > 0;
                    }
                }
            }
        }

        return false;
    }
}