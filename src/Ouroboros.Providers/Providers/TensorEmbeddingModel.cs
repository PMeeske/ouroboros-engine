using System.Numerics.Tensors;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace Ouroboros.Providers;

/// <summary>
/// Local embedding model with two execution paths:
/// <list type="bullet">
/// <item><b>ONNX + DirectML (GPU)</b> — when an ONNX model path is provided, embeddings
///   are computed on the GPU via DirectML execution provider.</item>
/// <item><b>TensorPrimitives (CPU SIMD)</b> — when no ONNX model is available, uses
///   Johnson–Lindenstrauss random projection with hashed n-gram features.</item>
/// </list>
/// Computed embeddings are cached in Qdrant to avoid recomputation.
/// </summary>
public sealed class TensorEmbeddingModel : IEmbeddingModel, IDisposable
{
    private readonly int _dimension;
    private readonly int _featureDim;
    private readonly float[] _weights;  // [featureDim x dimension] row-major
    private readonly float[] _bias;     // [dimension]
    private readonly InferenceSession? _onnxSession;
    private readonly string? _onnxInputName;
    private readonly QdrantClient? _qdrant;
    private readonly string _cacheCollection;
    private readonly ILogger? _logger;
    private bool _disposed;

    /// <summary>Default output dimension matching mxbai-embed-large (1024).</summary>
    public const int DefaultDimension = 1024;

    /// <summary>Default sparse feature dimension (number of hash buckets for n-grams).</summary>
    public const int DefaultFeatureDim = 4096;

    /// <summary>Qdrant collection name for embedding cache.</summary>
    public const string DefaultCacheCollection = "tensor_embedding_cache";

    /// <summary>
    /// Initializes a tensor embedding model.
    /// </summary>
    /// <param name="qdrant">Optional Qdrant client for caching embeddings.</param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="onnxModelPath">Optional path to an ONNX embedding model for DirectML GPU inference.</param>
    /// <param name="dimension">Output embedding dimension (used for CPU path; ONNX model determines its own).</param>
    /// <param name="featureDim">Internal sparse feature dimension (hash buckets) for CPU path.</param>
    /// <param name="seed">Random seed for reproducible projection matrix (CPU path).</param>
    /// <param name="cacheCollection">Qdrant collection name for cache.</param>
    public TensorEmbeddingModel(
        QdrantClient? qdrant = null,
        ILogger? logger = null,
        string? onnxModelPath = null,
        int dimension = DefaultDimension,
        int featureDim = DefaultFeatureDim,
        int seed = 42,
        string cacheCollection = DefaultCacheCollection)
    {
        _dimension = dimension > 0 ? dimension : DefaultDimension;
        _featureDim = featureDim > 0 ? featureDim : DefaultFeatureDim;
        _qdrant = qdrant;
        _cacheCollection = cacheCollection;
        _logger = logger;

        // Try ONNX + DirectML GPU path
        if (!string.IsNullOrEmpty(onnxModelPath) && File.Exists(onnxModelPath))
        {
            try
            {
                using var options = new SessionOptions();
                options.AppendExecutionProvider_DML(0); // Device 0 = primary GPU
                options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
                _onnxSession = new InferenceSession(onnxModelPath, options);
                _onnxInputName = _onnxSession.InputMetadata.Keys.First();
                _logger?.LogInformation(
                    "TensorEmbeddingModel: DirectML GPU inference enabled via {Model}",
                    Path.GetFileName(onnxModelPath));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger?.LogWarning(ex,
                    "DirectML session creation failed, falling back to CPU TensorPrimitives path");
                _onnxSession?.Dispose();
                _onnxSession = null;
            }
        }

        // Build CPU projection weights regardless (used as fallback)
        var rng = new System.Random(seed);
        float scale = MathF.Sqrt(2.0f / (_featureDim + _dimension));

        _weights = new float[_featureDim * _dimension];
        for (int i = 0; i < _weights.Length; i++)
        {
            float u1 = 1.0f - (float)rng.NextDouble();
            float u2 = (float)rng.NextDouble();
            _weights[i] = MathF.Sqrt(-2.0f * MathF.Log(u1)) * MathF.Cos(2.0f * MathF.PI * u2) * scale;
        }

        _bias = new float[_dimension];
        for (int i = 0; i < _dimension; i++)
            _bias[i] = (float)(rng.NextDouble() - 0.5) * 0.01f;
    }

    /// <summary>Whether the DirectML GPU path is active.</summary>
    public bool IsGpuAccelerated => _onnxSession is not null;

    /// <inheritdoc/>
    public int Dimension => _dimension;

    /// <inheritdoc/>
    public async Task<float[]> CreateEmbeddingsAsync(string input, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrEmpty(input))
            input = "empty";

        // Check Qdrant cache first
        ulong pointId = ComputePointId(input);
        if (_qdrant is not null)
        {
            var cached = await TryGetCachedAsync(pointId, ct).ConfigureAwait(false);
            if (cached is not null)
                return cached;
        }

        // Compute embedding — GPU or CPU path
        float[] output = _onnxSession is not null
            ? ComputeOnnxEmbedding(input)
            : ComputeCpuEmbedding(input);

        // Cache in Qdrant (fire-and-forget)
        if (_qdrant is not null)
            _ = CacheEmbeddingAsync(pointId, input, output);

        return output;
    }

    /// <summary>
    /// GPU path: run input through ONNX model with DirectML execution provider.
    /// </summary>
    private float[] ComputeOnnxEmbedding(string input)
    {
        // Tokenize: simple character-level encoding (ONNX models may need different tokenization)
        var tokenIds = TokenizeForOnnx(input);
        var inputTensor = new DenseTensor<long>(tokenIds, [1, tokenIds.Length]);
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(_onnxInputName!, inputTensor)
        };

        // Add attention mask if the model expects it
        if (_onnxSession!.InputMetadata.ContainsKey("attention_mask"))
        {
            var mask = new long[tokenIds.Length];
            Array.Fill(mask, 1L);
            var maskTensor = new DenseTensor<long>(mask, [1, tokenIds.Length]);
            inputs.Add(NamedOnnxValue.CreateFromTensor("attention_mask", maskTensor));
        }

        using var results = _onnxSession.Run(inputs);
        var outputTensor = results.First().AsEnumerable<float>().ToArray();

        // Mean pooling over sequence dimension if output is 3D [batch, seq, hidden]
        var outputMeta = _onnxSession.OutputMetadata.Values.First();
        if (outputMeta.Dimensions.Length == 3)
        {
            int hiddenDim = outputMeta.Dimensions[2];
            int seqLen = outputTensor.Length / hiddenDim;
            var pooled = new float[hiddenDim];
            for (int s = 0; s < seqLen; s++)
            {
                var slice = outputTensor.AsSpan(s * hiddenDim, hiddenDim);
                TensorPrimitives.Add(pooled, slice, pooled);
            }
            TensorPrimitives.Divide(pooled, seqLen, pooled);
            outputTensor = pooled;
        }

        // L2 normalize
        float norm = TensorPrimitives.Norm(outputTensor);
        if (norm > 1e-10f)
            TensorPrimitives.Divide(outputTensor, norm, outputTensor);

        return outputTensor;
    }

    /// <summary>
    /// CPU path: n-gram feature extraction → random projection → ReLU → L2 normalize.
    /// Uses TensorPrimitives for SIMD-accelerated vector ops.
    /// </summary>
    private float[] ComputeCpuEmbedding(string input)
    {
        var features = ExtractFeatures(input);
        var output = new float[_dimension];

        // Sparse MatMul: features[1 x featureDim] @ weights[featureDim x dimension]
        for (int i = 0; i < _featureDim; i++)
        {
            float f = features[i];
            if (f == 0) continue;
            var weightRow = _weights.AsSpan(i * _dimension, _dimension);
            // output += f * weightRow (scatter accumulate)
            for (int j = 0; j < _dimension; j++)
                output[j] += f * weightRow[j];
        }

        // Add bias
        TensorPrimitives.Add(output, _bias, output);

        // ReLU
        for (int i = 0; i < output.Length; i++)
            if (output[i] < 0) output[i] = 0;

        // L2 normalize
        float norm = TensorPrimitives.Norm(output);
        if (norm > 1e-10f)
            TensorPrimitives.Divide(output, norm, output);

        return output;
    }

    /// <summary>
    /// Simple tokenization for ONNX models. Encodes each character as its Unicode codepoint.
    /// Replace with proper BPE/WordPiece tokenizer when using a specific model.
    /// </summary>
    private static long[] TokenizeForOnnx(string input, int maxLength = 512)
    {
        int len = Math.Min(input.Length, maxLength);
        var tokens = new long[len];
        for (int i = 0; i < len; i++)
            tokens[i] = input[i];
        return tokens;
    }

    /// <summary>
    /// Extracts sparse features from text using hashed character n-grams (bi/tri/quad-grams)
    /// plus word-level unigrams.
    /// </summary>
    private float[] ExtractFeatures(string text)
    {
        var features = new float[_featureDim];
        var lower = text.ToLowerInvariant();

        // Character bigrams
        for (int i = 0; i < lower.Length - 1; i++)
        {
            uint hash = FnvHash(lower.AsSpan(i, 2));
            features[hash % (uint)_featureDim] += 1.0f;
        }

        // Character trigrams (weighted higher — more discriminative)
        for (int i = 0; i < lower.Length - 2; i++)
        {
            uint hash = FnvHash(lower.AsSpan(i, 3));
            features[hash % (uint)_featureDim] += 1.5f;
        }

        // Character quadgrams
        for (int i = 0; i < lower.Length - 3; i++)
        {
            uint hash = FnvHash(lower.AsSpan(i, 4));
            features[hash % (uint)_featureDim] += 1.0f;
        }

        // Word-level unigrams (weighted highest)
        int wordStart = -1;
        for (int i = 0; i <= lower.Length; i++)
        {
            bool isSep = i == lower.Length || !char.IsLetterOrDigit(lower[i]);
            if (isSep && wordStart >= 0)
            {
                var word = lower.AsSpan(wordStart, i - wordStart);
                if (word.Length >= 2)
                {
                    uint hash = FnvHash(word) ^ 0xDEADBEEF;
                    features[hash % (uint)_featureDim] += 2.0f;
                }
                wordStart = -1;
            }
            else if (!isSep && wordStart < 0)
            {
                wordStart = i;
            }
        }

        // TF normalization
        float docLen = MathF.Sqrt(lower.Length + 1.0f);
        TensorPrimitives.Divide(features, docLen, features);

        return features;
    }

    /// <summary>FNV-1a hash for character spans.</summary>
    private static uint FnvHash(ReadOnlySpan<char> span)
    {
        uint hash = 2166136261u;
        foreach (char c in span)
        {
            hash ^= c;
            hash *= 16777619u;
        }
        return hash;
    }

    /// <summary>Stable point ID from input text for Qdrant cache keying.</summary>
    private static ulong ComputePointId(string input)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToUInt64(hash, 0);
    }

    private async Task<float[]?> TryGetCachedAsync(ulong pointId, CancellationToken ct)
    {
        try
        {
            IReadOnlyList<RetrievedPoint> points = await _qdrant!.RetrieveAsync(
                _cacheCollection,
                [pointId],
                withVectors: true,
                cancellationToken: ct).ConfigureAwait(false);

            if (points is { Count: > 0 } && points[0].Vectors?.Vector?.Data is { Count: > 0 } data)
                return [.. data];
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Grpc.Core.RpcException ex)
        {
            _logger?.LogDebug(ex, "Qdrant embedding cache miss for point {PointId}", pointId);
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogDebug(ex, "Qdrant embedding cache miss for point {PointId}", pointId);
        }

        return null;
    }

    private async Task CacheEmbeddingAsync(ulong pointId, string input, float[] embedding)
    {
        try
        {
            await _qdrant!.UpsertAsync(
                _cacheCollection,
                [
                    new PointStruct
                    {
                        Id = new PointId { Num = pointId },
                        Vectors = embedding,
                        Payload =
                        {
                            ["text_preview"] = input.Length > 200 ? input[..200] : input,
                            ["created_at"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                        }
                    }
                ]).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogDebug(ex, "Failed to cache embedding in Qdrant");
        }
    }

    /// <summary>
    /// Ensures the Qdrant cache collection exists. Call once at startup.
    /// </summary>
    public async Task EnsureCacheCollectionAsync(CancellationToken ct = default)
    {
        if (_qdrant is null) return;

        try
        {
            var collections = await _qdrant.ListCollectionsAsync(ct).ConfigureAwait(false);
            if (collections.Any(c => c == _cacheCollection))
                return;

            await _qdrant.CreateCollectionAsync(
                _cacheCollection,
                new VectorParams
                {
                    Size = (ulong)_dimension,
                    Distance = Distance.Cosine
                },
                cancellationToken: ct).ConfigureAwait(false);

            _logger?.LogInformation(
                "Created Qdrant embedding cache collection: {Collection}", _cacheCollection);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogWarning(ex, "Could not create Qdrant cache collection");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _onnxSession?.Dispose();
    }
}
