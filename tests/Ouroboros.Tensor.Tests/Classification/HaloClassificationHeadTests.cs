// <copyright file="HaloClassificationHeadTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Numerics.Tensors;
using Ouroboros.Tensor.Classification;

namespace Ouroboros.Tests.Classification;

public class HaloClassificationHeadTests
{
    /// <summary>
    /// Helper to create L2-normalized unit vectors for testing.
    /// </summary>
    private static float[][] MakeCentroids(params float[][] vectors)
    {
        return vectors.Select(v =>
        {
            float norm = MathF.Sqrt(v.Sum(x => x * x));
            return norm > 0 ? v.Select(x => x / norm).ToArray() : v;
        }).ToArray();
    }

    /// <summary>
    /// Helper to create a simple 3-class head with given centroids.
    /// </summary>
    private static HaloClassificationHead CreateHead(float[][] centroids, float sigma = 1.0f, bool includeOriginSink = true)
    {
        var names = Enumerable.Range(0, centroids.Length).Select(i => $"class_{i}").ToArray();
        return new HaloClassificationHead(centroids, names, sigma, includeOriginSink);
    }

    /// <summary>
    /// Test 1: At-centroid embedding should produce high confidence (>0.9) and low sink probability.
    /// Verifies the core property that matching a centroid closely yields confident classification.
    /// Uses sigma=0.3 to sharpen the RBF kernel for 3D unit-vector centroids.
    /// </summary>
    [Fact]
    public void Classify_AtCentroid_HighConfidence()
    {
        // Arrange: 3 L2-normalized centroids in 3D with small sigma for sharp classification
        var centroids = MakeCentroids(
            [1f, 0f, 0f],
            [0f, 1f, 0f],
            [0f, 0f, 1f]
        );
        var names = new[] { "class_0", "class_1", "class_2" };
        var head = new HaloClassificationHead(centroids, names, sigma: 0.3f);

        // Act: classify an embedding identical to centroid[0]
        var result = head.Classify(centroids[0]);

        // Assert: confident classification with high probability
        // With sigma=0.3, logit at matching centroid = (1 - 0.5) / 0.09 = 5.56
        // vs non-matching logits = (0 - 0.5) / 0.09 = -5.56, sink = 0
        // This produces very high confidence for the matching class
        result.ClassIndex.Should().Be(0);
        result.ClassName.Should().Be("class_0");
        result.Confidence.Should().BeGreaterThan(0.9f);
        result.SinkProbability.Should().BeLessThan(0.1f);
        result.IsOutOfDistribution.Should().BeFalse();
    }

    /// <summary>
    /// Test 2: Embedding at the origin should be classified as OOD when origin sink is enabled.
    /// The origin is equidistant from all unit vectors but the origin sink captures it.
    /// </summary>
    [Fact]
    public void Classify_AtOrigin_WithSink_IsOOD()
    {
        // Arrange: all centroids are unit vectors (far from origin)
        var centroids = MakeCentroids(
            [1f, 0f, 0f],
            [0f, 1f, 0f],
            [0f, 0f, 1f]
        );
        var head = CreateHead(centroids);

        // Act: classify the zero vector (origin)
        var result = head.Classify([0f, 0f, 0f]);

        // Assert: OOD -- sink wins over any individual class
        // At origin, all class logits = (0 - 0.5)/1 = -0.5, sink logit = 0
        // So sink probability ~0.354 > each class probability ~0.215
        result.IsOutOfDistribution.Should().BeTrue();
        result.ClassIndex.Should().Be(-1);
        result.ClassName.Should().Be("unknown");

        // Sink probability exceeds each individual class probability
        float maxClassProb = 0f;
        for (int i = 0; i < centroids.Length; i++)
            maxClassProb = MathF.Max(maxClassProb, result.AllProbabilities[i]);
        result.SinkProbability.Should().BeGreaterThan(maxClassProb);
    }

    /// <summary>
    /// Test 3: Embedding equidistant from two orthogonal centroids should have
    /// approximately equal confidence (around 0.5 excluding sink).
    /// Verifies that HALO correctly handles ambiguous inputs.
    /// </summary>
    [Fact]
    public void Classify_Equidistant_LowConfidence()
    {
        // Arrange: two orthogonal centroids
        var centroids = MakeCentroids(
            [1f, 0f],
            [0f, 1f]
        );
        var head = CreateHead(centroids, sigma: 1.0f, includeOriginSink: false);

        // Act: embedding equidistant from both (45 degrees)
        var equidistant = new float[] { 1f, 1f }; // equal projection onto both
        var result = head.Classify(equidistant);

        // Assert: approximately equal confidence (within tolerance)
        // Without sink, the two class probabilities should be roughly equal
        var probs = result.AllProbabilities;
        probs[0].Should().BeApproximately(probs[1], 0.15f);
        result.Confidence.Should().BeApproximately(0.5f, 0.15f);
    }

    /// <summary>
    /// Test 4: Embedding orthogonal to all centroids triggers strong sink dominance.
    /// HALO uses negative squared L2 distance, so embeddings orthogonal to all centroids
    /// have zero dot products, making class logits negative while sink logit = 0.
    /// Uses small sigma to sharpen the distinction.
    /// </summary>
    [Fact]
    public void Classify_FarFromAll_SinkDominates()
    {
        // Arrange: 2 centroids in a 2D subspace of 3D space
        var centroids = new float[][]
        {
            [1f, 0f, 0f],
            [0f, 1f, 0f],
        };
        var names = new[] { "class_0", "class_1" };
        var head = new HaloClassificationHead(centroids, names, sigma: 0.3f);

        // Act: embedding along the 3rd dimension (orthogonal to all centroids)
        // This has zero dot product with any centroid, so all class logits are large negative
        // logit_k = (0 - ||w_k||^2/2) / sigma^2 = (0 - 0.5) / 0.09 = -5.56
        // sink logit = 0 -- strongly wins
        var orthogonalEmbedding = new float[] { 0f, 0f, 100f };
        var result = head.Classify(orthogonalEmbedding);

        // Assert: OOD with very dominant sink
        result.IsOutOfDistribution.Should().BeTrue();
        result.SinkProbability.Should().BeGreaterThan(0.9f);
    }

    /// <summary>
    /// Test 5: Without origin sink, even orthogonal embeddings should be classified to nearest centroid.
    /// Verifies that the sink mechanism is the only OOD path -- without it, everything gets a class.
    /// </summary>
    [Fact]
    public void Classify_WithoutSink_AlwaysClassifies()
    {
        // Arrange: centroids without origin sink
        var centroids = new float[][]
        {
            [1f, 0f, 0f],
            [0f, 1f, 0f],
        };
        var names = new[] { "class_0", "class_1" };
        var head = new HaloClassificationHead(centroids, names, includeOriginSink: false);

        // Act: embedding along orthogonal direction -- without sink, still classified
        var orthogonalEmbedding = new float[] { 0f, 0f, 100f };
        var result = head.Classify(orthogonalEmbedding);

        // Assert: not OOD, classified to some class (probabilities distributed uniformly)
        result.IsOutOfDistribution.Should().BeFalse();
        result.ClassIndex.Should().BeGreaterThanOrEqualTo(0);
        result.SinkProbability.Should().Be(0f);
    }

    /// <summary>
    /// Test 6: Shift-invariance property: adding a constant vector to all centroids and
    /// the input should produce identical classification results.
    /// This verifies the core mathematical property of HALO logits.
    /// </summary>
    [Fact]
    public void Classify_ShiftInvariance_SameResult()
    {
        // Arrange: two simple centroids
        var centroids = new float[][]
        {
            [1f, 0f],
            [0f, 1f],
        };
        var names = new[] { "alpha", "beta" };
        var head = new HaloClassificationHead(centroids, names, sigma: 1.0f);

        // Act: classify the original input
        var originalInput = new float[] { 0.8f, 0.3f };
        var result1 = head.Classify(originalInput);

        // Shift all centroids and input by the same constant vector
        var shift = new float[] { 5f, -3f };
        var shiftedCentroids = centroids.Select(c =>
            c.Zip(shift).Select((pair, _) => pair.First + pair.Second).ToArray()
        ).ToArray();
        var shiftedInput = originalInput.Zip(shift).Select((pair, _) => pair.First + pair.Second).ToArray();
        var shiftedHead = new HaloClassificationHead(shiftedCentroids, names, sigma: 1.0f);
        var result2 = shiftedHead.Classify(shiftedInput);

        // Assert: same classification (shift-invariant)
        // Note: probabilities may differ slightly due to the origin sink, but class ranking should be the same
        result2.ClassIndex.Should().Be(result1.ClassIndex);
        result2.ClassName.Should().Be(result1.ClassName);
        result2.IsOutOfDistribution.Should().Be(result1.IsOutOfDistribution);
    }

    /// <summary>
    /// Test 7: Updating a centroid should change subsequent classification results.
    /// Verifies that UpdateCentroid recomputes norms and affects logit computation.
    /// </summary>
    [Fact]
    public void UpdateCentroid_ChangesClassification()
    {
        // Arrange: two centroids far apart
        var centroids = new float[][]
        {
            [1f, 0f],  // class_0 on x-axis
            [0f, 1f],  // class_1 on y-axis
        };
        var names = new[] { "class_0", "class_1" };
        var head = new HaloClassificationHead(centroids, names, sigma: 1.0f);

        // Act: classify a point near class_0
        var input = new float[] { 0.9f, 0.1f };
        var result1 = head.Classify(input);
        result1.ClassIndex.Should().Be(0); // initially classified as class_0

        // Update class_1's centroid to be closer to the input
        head.UpdateCentroid(1, [0.85f, 0.15f]);

        // Classify again -- should now favor class_1 or show reduced confidence in class_0
        var result2 = head.Classify(input);

        // Assert: confidence in class_0 should decrease or class should change
        // Either the class changes, or confidence drops significantly
        bool classChanged = result2.ClassIndex != 0;
        bool confidenceDecreased = result2.ClassIndex == 0 && result2.Confidence < result1.Confidence;
        (classChanged || confidenceDecreased).Should().BeTrue(
            "updating centroid should change classification behavior");
    }

    /// <summary>
    /// Test 8: Large logit values should not produce NaN or Infinity.
    /// Verifies numerical stability of softmax with extreme inputs.
    /// </summary>
    [Fact]
    public void Classify_NumericalStability_LargeValues()
    {
        // Arrange: centroids with large values
        var centroids = new float[][]
        {
            [1000f, -500f, 200f],
            [-300f, 800f, -600f],
            [400f, -700f, 900f],
        };
        var names = new[] { "large_0", "large_1", "large_2" };
        var head = new HaloClassificationHead(centroids, names, sigma: 1.0f);

        // Act: classify with large embedding
        var largeInput = new float[] { -800f, 600f, 1000f };
        var result = head.Classify(largeInput);

        // Assert: no NaN or Infinity in probabilities
        result.Confidence.Should().BeInRange(0f, 1f);
        result.SinkProbability.Should().BeInRange(0f, 1f);
        result.AllProbabilities.Should().OnlyContain(p => !float.IsNaN(p) && !float.IsInfinity(p));
        result.AllProbabilities.Sum().Should().BeApproximately(1.0f, 0.001f);
    }

    /// <summary>
    /// Additional validation: constructor rejects mismatched array lengths.
    /// </summary>
    [Fact]
    public void Constructor_MismatchedLengths_Throws()
    {
        var centroids = new float[][] { [1f, 0f], [0f, 1f] };
        var names = new[] { "only_one" };

        var act = () => new HaloClassificationHead(centroids, names);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*must match*");
    }

    /// <summary>
    /// Additional validation: constructor rejects non-positive sigma.
    /// </summary>
    [Fact]
    public void Constructor_NonPositiveSigma_Throws()
    {
        var centroids = new float[][] { [1f, 0f] };
        var names = new[] { "a" };

        var act = () => new HaloClassificationHead(centroids, names, sigma: 0f);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*positive*");
    }

    /// <summary>
    /// Additional validation: constructor rejects inconsistent centroid dimensions.
    /// </summary>
    [Fact]
    public void Constructor_InconsistentDimensions_Throws()
    {
        var centroids = new float[][] { [1f, 0f], [1f] }; // different dimensions
        var names = new[] { "a", "b" };

        var act = () => new HaloClassificationHead(centroids, names);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*dimension*");
    }

    /// <summary>
    /// Additional validation: Classify rejects wrong embedding dimension.
    /// </summary>
    [Fact]
    public void Classify_WrongDimension_Throws()
    {
        var centroids = new float[][] { [1f, 0f, 0f] };
        var names = new[] { "a" };
        var head = new HaloClassificationHead(centroids, names);

        var act = () => head.Classify([1f, 0f]); // wrong dimension

        act.Should().Throw<ArgumentException>()
            .WithMessage("*dimension*");
    }

    /// <summary>
    /// Additional validation: UpdateCentroid rejects out-of-range index.
    /// </summary>
    [Fact]
    public void UpdateCentroid_OutOfRange_Throws()
    {
        var centroids = new float[][] { [1f, 0f] };
        var names = new[] { "a" };
        var head = new HaloClassificationHead(centroids, names);

        var act = () => head.UpdateCentroid(5, [0.5f, 0.5f]);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// Additional validation: UpdateCentroid rejects wrong dimension.
    /// </summary>
    [Fact]
    public void UpdateCentroid_WrongDimension_Throws()
    {
        var centroids = new float[][] { [1f, 0f] };
        var names = new[] { "a" };
        var head = new HaloClassificationHead(centroids, names);

        var act = () => head.UpdateCentroid(0, [0.5f]); // wrong dimension

        act.Should().Throw<ArgumentException>()
            .WithMessage("*dimension*");
    }

    /// <summary>
    /// Additional validation: properties reflect constructor arguments.
    /// </summary>
    [Fact]
    public void Properties_ReflectConstructor()
    {
        var centroids = new float[][] { [1f, 0f], [0f, 1f], [1f, 1f] };
        var names = new[] { "a", "b", "c" };
        var head = new HaloClassificationHead(centroids, names, sigma: 2.0f, includeOriginSink: false);

        head.ClassCount.Should().Be(3);
        head.EmbeddingDim.Should().Be(2);
        head.HasOriginSink.Should().BeFalse();
    }

    /// <summary>
    /// Additional validation: probabilities sum to 1.0 (with origin sink).
    /// </summary>
    [Fact]
    public void Classify_ProbabilitiesSumToOne_WithSink()
    {
        var centroids = MakeCentroids(
            [1f, 0f, 0f],
            [0f, 1f, 0f],
            [0f, 0f, 1f]
        );
        var head = CreateHead(centroids);

        var result = head.Classify([0.5f, 0.5f, 0.5f]);

        // Probabilities should sum to approximately 1.0 (K classes + 1 sink)
        result.AllProbabilities.Sum().Should().BeApproximately(1.0f, 0.001f);
    }

    /// <summary>
    /// Additional validation: probabilities sum to 1.0 (without origin sink).
    /// </summary>
    [Fact]
    public void Classify_ProbabilitiesSumToOne_WithoutSink()
    {
        var centroids = MakeCentroids(
            [1f, 0f, 0f],
            [0f, 1f, 0f],
            [0f, 0f, 1f]
        );
        var head = CreateHead(centroids, includeOriginSink: false);

        var result = head.Classify([0.5f, 0.5f, 0.5f]);

        // Without sink, probabilities sum to 1.0 across K classes
        result.AllProbabilities.Sum().Should().BeApproximately(1.0f, 0.001f);
        result.SinkProbability.Should().Be(0f);
    }
}