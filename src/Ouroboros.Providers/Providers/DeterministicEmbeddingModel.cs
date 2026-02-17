namespace Ouroboros.Providers;

/// <summary>
/// Deterministic embedding generator that hashes the input string. It is not a
/// semantic encoder, but it provides stable vectors for testing and demos when
/// no real embedding service is available.
/// </summary>
public sealed class DeterministicEmbeddingModel : IEmbeddingModel
{
    /// <summary>
    /// Default vector dimension matching nomic-embed-text (768).
    /// </summary>
    public const int DefaultDimension = 768;

    private readonly int _dimension;

    /// <summary>
    /// Initializes a new instance with the default dimension (768).
    /// </summary>
    public DeterministicEmbeddingModel() : this(DefaultDimension) { }

    /// <summary>
    /// Initializes a new instance with a custom dimension.
    /// </summary>
    /// <param name="dimension">The vector dimension to generate.</param>
    public DeterministicEmbeddingModel(int dimension)
    {
        _dimension = dimension > 0 ? dimension : DefaultDimension;
    }

    /// <inheritdoc/>
    public Task<float[]> CreateEmbeddingsAsync(string input, CancellationToken ct = default)
    {
        if (input is null) input = string.Empty;

        // Compress long inputs instead of truncating to preserve semantic information
        // This captures essence from entire text rather than just the beginning
        byte[] buffer;
        if (input.Length > 2000)
        {
            // Use compression: extract semantic fingerprint from entire text
            buffer = CompressTextForEmbedding(input);
        }
        else
        {
            buffer = System.Text.Encoding.UTF8.GetBytes(input);
        }

        byte[] hash = System.Security.Cryptography.SHA256.HashData(buffer);

        // Generate a vector of the target dimension by cycling through hash bytes
        float[] vector = new float[_dimension];
        for (int i = 0; i < _dimension; i++)
        {
            // Use hash bytes cyclically and add position-based variation
            byte hashByte = hash[i % hash.Length];
            float positionFactor = (float)Math.Sin(i * 0.1) * 0.1f;
            vector[i] = (hashByte / 255f) + positionFactor;
        }

        // Normalize the vector for better similarity comparisons
        float magnitude = 0f;
        for (int i = 0; i < _dimension; i++)
        {
            magnitude += vector[i] * vector[i];
        }
        magnitude = (float)Math.Sqrt(magnitude);
        if (magnitude > 0)
        {
            for (int i = 0; i < _dimension; i++)
            {
                vector[i] /= magnitude;
            }
        }

        return Task.FromResult(vector);
    }

    /// <summary>
    /// Compresses long text for embedding by extracting semantic fingerprint from entire content.
    /// Uses chunking and rolling hash to capture information from throughout the text.
    /// </summary>
    private static byte[] CompressTextForEmbedding(string input)
    {
        const int chunkSize = 200;  // Characters per chunk
        const int maxChunks = 20;   // Sample up to 20 chunks

        // Sample chunks from throughout the text
        int totalChunks = (input.Length + chunkSize - 1) / chunkSize;
        int stride = Math.Max(1, totalChunks / maxChunks);

        using var ms = new System.IO.MemoryStream();
        using var writer = new System.IO.BinaryWriter(ms);

        // Hash each sampled chunk and combine
        int chunksProcessed = 0;
        for (int i = 0; i < input.Length && chunksProcessed < maxChunks; i += chunkSize * stride)
        {
            int end = Math.Min(i + chunkSize, input.Length);
            string chunk = input[i..end];

            // Get hash of this chunk
            byte[] chunkBytes = System.Text.Encoding.UTF8.GetBytes(chunk);
            byte[] chunkHash = System.Security.Cryptography.MD5.HashData(chunkBytes);
            writer.Write(chunkHash);
            chunksProcessed++;
        }

        // Add length as additional semantic signal
        writer.Write(input.Length);

        // Add word count signature
        int wordCount = 0;
        bool inWord = false;
        foreach (char c in input)
        {
            if (char.IsLetterOrDigit(c))
            {
                if (!inWord) { wordCount++; inWord = true; }
            }
            else { inWord = false; }
        }
        writer.Write(wordCount);

        // Add character frequency signature (top 8 chars)
        var freqs = new Dictionary<char, int>();
        foreach (char c in input.Where(char.IsLetter).Select(char.ToLowerInvariant))
        {
            freqs[c] = freqs.GetValueOrDefault(c) + 1;
        }
        foreach (var (ch, count) in freqs.OrderByDescending(kv => kv.Value).Take(8))
        {
            writer.Write((byte)ch);
            writer.Write((ushort)Math.Min(count, ushort.MaxValue));
        }

        return ms.ToArray();
    }
}