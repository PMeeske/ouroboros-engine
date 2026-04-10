// <copyright file="HaloClassificationHead.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Classification;

/// <summary>
/// HALO-Loss classification head with shift-invariant RBF kernel and origin sink
/// for principled out-of-distribution detection.
///
/// <para>
/// Standard cross-entropy logits push embeddings radially toward infinity, causing
/// overconfidence on out-of-distribution inputs (the soap-bubble problem). HALO replaces
/// dot-product logits with negative squared L2 distance from class centroids, plus a
/// parameter-free origin sink that captures OOD inputs.
/// </para>
///
/// <para>
/// The shift-invariant logit formula avoids computing ||h||^2 per class:
/// <c>logit_k = (h . w_k - ||w_k||^2 / 2) / sigma^2</c>
/// Since softmax is shift-invariant, subtracting ||h||^2 from all logits is equivalent
/// to adding it back, so it cancels and only the dot product and precomputed norm are needed.
/// </para>
///
/// <para>
/// The origin sink has logit = 0 (after shift-invariant cancellation), which means:
/// <list type="bullet">
///   <item><description>Inputs near a centroid produce high class probability (Mark/Fire)</description></item>
///   <item><description>Inputs at the origin produce high sink probability (Imaginary/Clarify)</description></item>
///   <item><description>Inputs far from all centroids and the origin produce low confidence across all categories</description></item>
/// </list>
/// </para>
///
/// <para>
/// This maps directly to the Laws of Form three-valued logic:
/// Mark = certain (Fire), Void = irrelevant (Ignore), Imaginary = uncertain (Clarify).
/// </para>
/// </summary>
public sealed class HaloClassificationHead
{
    private readonly float[][] _centroids;
    private readonly string[] _classNames;
    private readonly float[] _centroidNormsSq;
    private readonly float _sigmaSquared;
    private readonly bool _includeOriginSink;

    /// <summary>
    /// Number of classes (centroids) in the head.
    /// </summary>
    public int ClassCount => _centroids.Length;

    /// <summary>
    /// Embedding dimension (length of each centroid vector).
    /// </summary>
    public int EmbeddingDim => _centroids.Length > 0 ? _centroids[0].Length : 0;

    /// <summary>
    /// Whether the origin sink is included for OOD detection.
    /// </summary>
    public bool HasOriginSink => _includeOriginSink;

    /// <summary>
    /// Creates a HALO classification head with the given centroids and configuration.
    /// </summary>
    /// <param name="centroids">
    /// Class centroids as jagged float arrays. Each centroid should typically be L2-normalized
    /// for well-calibrated probabilities with the default sigma.
    /// </param>
    /// <param name="classNames">
    /// Human-readable class names, one per centroid. Must be same length as centroids.
    /// </param>
    /// <param name="sigma">
    /// RBF kernel bandwidth parameter. Controls how sharply probability decays with distance.
    /// Larger sigma = softer classification (more uniform). Smaller sigma = sharper peaks.
    /// Default is 1.0, suitable for L2-normalized 256-dim embeddings.
    /// </param>
    /// <param name="includeOriginSink">
    /// Whether to include the parameter-free origin sink for OOD detection.
    /// When true, an extra "sink" class with logit = 0 captures inputs far from all centroids.
    /// Default is true.
    /// </param>
    /// <exception cref="ArgumentNullException">When centroids or classNames is null.</exception>
    /// <exception cref="ArgumentException">
    /// When centroids and classNames have different lengths, centroids have inconsistent dimensions,
    /// or sigma is not positive.
    /// </exception>
    public HaloClassificationHead(float[][] centroids, string[] classNames, float sigma = 1.0f, bool includeOriginSink = true)
    {
        ArgumentNullException.ThrowIfNull(centroids);
        ArgumentNullException.ThrowIfNull(classNames);

        if (centroids.Length != classNames.Length)
        {
            throw new ArgumentException(
                $"Centroids length ({centroids.Length}) must match classNames length ({classNames.Length}).",
                nameof(centroids));
        }

        if (centroids.Length == 0)
        {
            throw new ArgumentException("Must provide at least one centroid.", nameof(centroids));
        }

        if (sigma <= 0f)
        {
            throw new ArgumentException($"Sigma must be positive, got {sigma}.", nameof(sigma));
        }

        // Validate consistent dimensions across centroids
        int dim = centroids[0].Length;
        if (dim == 0)
        {
            throw new ArgumentException("Centroid dimension must be positive.", nameof(centroids));
        }

        for (int i = 1; i < centroids.Length; i++)
        {
            if (centroids[i].Length != dim)
            {
                throw new ArgumentException(
                    $"Centroid {i} has dimension {centroids[i].Length}, expected {dim}. " +
                    "All centroids must have the same dimension.",
                    nameof(centroids));
            }
        }

        _centroids = centroids;
        _classNames = classNames;
        _sigmaSquared = sigma * sigma;
        _includeOriginSink = includeOriginSink;

        // Precompute ||w_k||^2 for each centroid using SIMD-accelerated TensorPrimitives
        _centroidNormsSq = new float[centroids.Length];
        for (int k = 0; k < centroids.Length; k++)
        {
            _centroidNormsSq[k] = TensorPrimitives.Dot(centroids[k], centroids[k]);
        }
    }

    /// <summary>
    /// Classifies an embedding using shift-invariant HALO logits with numerically stable softmax.
    /// </summary>
    /// <param name="embedding">
    /// The input embedding vector to classify. Must have the same dimension as the centroids.
    /// </param>
    /// <returns>
    /// A <see cref="HaloResult"/> containing the winning class, confidence, OOD status,
    /// sink probability, and full probability distribution.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// When embedding length does not match centroid dimension.
    /// </exception>
    public HaloResult Classify(ReadOnlySpan<float> embedding)
    {
        if (embedding.Length != EmbeddingDim)
        {
            throw new ArgumentException(
                $"Embedding dimension ({embedding.Length}) must match centroid dimension ({EmbeddingDim}).",
                nameof(embedding));
        }

        int K = _centroids.Length;
        int totalLogits = K + (_includeOriginSink ? 1 : 0);

        // Use stack allocation for logits and probs (typically < 20 classes)
        Span<float> logits = stackalloc float[totalLogits];
        Span<float> probs = stackalloc float[totalLogits];

        // Step 1: Compute shift-invariant HALO logits
        // logit_k = (h . w_k - ||w_k||^2 / 2) / sigma^2
        // This is equivalent to -||h - w_k||^2 / (2 * sigma^2) minus the shift ||h||^2 / (2 * sigma^2)
        // which cancels in softmax.
        for (int k = 0; k < K; k++)
        {
            float dot = TensorPrimitives.Dot(embedding, _centroids[k]);
            logits[k] = (dot - _centroidNormsSq[k] / 2f) / _sigmaSquared;
        }

        // Origin sink: logit = 0 (after shift-invariant cancellation)
        // The origin has dot(h, 0) = 0 and ||0||^2 = 0, so logit = (0 - 0) / sigma^2 = 0
        if (_includeOriginSink)
        {
            logits[K] = 0f;
        }

        // Step 2: Numerically stable softmax (subtract max before exp to prevent overflow)
        float maxLogit = float.NegativeInfinity;
        for (int i = 0; i < totalLogits; i++)
        {
            if (logits[i] > maxLogit)
                maxLogit = logits[i];
        }

        float sumExp = 0f;
        for (int i = 0; i < totalLogits; i++)
        {
            probs[i] = MathF.Exp(logits[i] - maxLogit);
            sumExp += probs[i];
        }

        // Normalize to get probability distribution
        for (int i = 0; i < totalLogits; i++)
        {
            probs[i] /= sumExp;
        }

        // Step 3: Find winner (highest probability class or sink)
        int winnerIndex = 0;
        float winnerProb = probs[0];
        for (int i = 1; i < totalLogits; i++)
        {
            if (probs[i] > winnerProb)
            {
                winnerProb = probs[i];
                winnerIndex = i;
            }
        }

        // Step 4: Determine if OOD (sink wins) and populate result
        bool isOod = _includeOriginSink && winnerIndex == K;
        float sinkProbability = _includeOriginSink ? probs[K] : 0f;

        // Copy probabilities to heap-allocated array for the result
        var allProbabilities = new float[totalLogits];
        probs.CopyTo(allProbabilities);

        return new HaloResult
        {
            ClassIndex = isOod ? -1 : winnerIndex,
            ClassName = isOod ? "unknown" : _classNames[winnerIndex],
            Confidence = isOod ? 0f : winnerProb,
            SinkProbability = sinkProbability,
            IsOutOfDistribution = isOod,
            AllProbabilities = allProbabilities,
        };
    }

    /// <summary>
    /// Updates a class centroid and recomputes its precomputed norm.
    /// This enables online centroid adaptation without rebuilding the entire head.
    /// </summary>
    /// <param name="classIndex">Index of the centroid to update.</param>
    /// <param name="newCentroid">The new centroid vector. Must have the same dimension as the original.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// When classIndex is negative or >= ClassCount.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// When newCentroid length does not match EmbeddingDim.
    /// </exception>
    public void UpdateCentroid(int classIndex, ReadOnlySpan<float> newCentroid)
    {
        if (classIndex < 0 || classIndex >= _centroids.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(classIndex),
                $"Class index {classIndex} is out of range [0, {_centroids.Length}).");
        }

        if (newCentroid.Length != EmbeddingDim)
        {
            throw new ArgumentException(
                $"New centroid dimension ({newCentroid.Length}) must match EmbeddingDim ({EmbeddingDim}).",
                nameof(newCentroid));
        }

        // Copy new centroid data
        newCentroid.CopyTo(_centroids[classIndex]);

        // Recompute precomputed norm for this centroid
        _centroidNormsSq[classIndex] = TensorPrimitives.Dot(
            _centroids[classIndex], _centroids[classIndex]);
    }
}