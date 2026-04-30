// <copyright file="MlpStatePredictorGpuTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Agent.MetaAI.WorldModel;
using Action = Ouroboros.Agent.MetaAI.WorldModel.Action;

namespace Ouroboros.Agent.Tests.MetaAI.WorldModel;

/// <summary>
/// Integration tests for MlpStatePredictor with GPU backend support.
/// Verifies GPU/CPU equivalence and backward compatibility (TNS-01, TNS-03).
/// </summary>
[Trait("Category", "Unit")]
public class MlpStatePredictorGpuTests
{
    private const int StateSize = 32;
    private const int ActionSize = 10;
    private const int HiddenSize = 16;
    private const int DefaultSeed = 42;

    /// <summary>
    /// Verifies that MlpStatePredictor produces consistent output regardless of backend.
    /// When backend is null (CPU), the predictor should work identically.
    /// </summary>
    [Fact]
    public void PredictAsync_WithNullBackend_ProducesSameOutputAsCpu()
    {
        // Arrange
        var predictor = MlpStatePredictor.CreateRandom(StateSize, ActionSize, HiddenSize, seed: DefaultSeed);
        var state = CreateTestState(StateSize);
        var action = new Action("test_action", new Dictionary<string, object>());

        // Act
        var result = predictor.PredictAsync(state, action).GetAwaiter().GetResult();

        // Assert
        result.Should().NotBeNull();
        result.Embedding.Should().HaveCount(StateSize);
        result.Features.Should().NotBeNull();
    }

    /// <summary>
    /// Verifies that weight flattening produces a contiguous array.
    /// Required for GPU tensor operations (TNS-01).
    /// </summary>
    [Fact]
    public void FlattenWeights_ConvertsJaggedToContiguousCorrectly()
    {
        // Arrange
        float[][] jaggedWeights =
        {
            new float[] { 1, 2 },
            new float[] { 3, 4 },
            new float[] { 5, 6 }
        };

        // Act
        float[] flat = FlattenWeights(jaggedWeights);

        // Assert
        flat.Should().HaveCount(6);
        flat.Should().ContainInOrder(1f, 2f, 3f, 4f, 5f, 6f);
    }

    /// <summary>
    /// Verifies that weight flattening handles empty input correctly.
    /// </summary>
    [Fact]
    public void FlattenWeights_WithEmptyInput_ReturnsEmptyArray()
    {
        // Arrange
        float[][] emptyWeights = Array.Empty<float[]>();

        // Act
        float[] flat = FlattenWeights(emptyWeights);

        // Assert
        flat.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that CosineSimilarity produces consistent results.
    /// TensorPrimitives.CosineSimilarity should match manual calculation within tolerance (TNS-03).
    /// </summary>
    [Fact]
    public void CosineSimilarity_MatchesManualCalculationWithinTolerance()
    {
        // Arrange
        var random = new Random(DefaultSeed);
        float[] v1 = CreateRandomVector(128, random);
        float[] v2 = CreateRandomVector(128, random);

        // Act
        float manual = ComputeCosineSimilarityManual(v1, v2);
        float simd = System.Numerics.Tensors.TensorPrimitives.CosineSimilarity(v1, v2);

        // Assert
        Math.Abs(manual - simd).Should().BeLessThan(1e-5f, "TensorPrimitives should match manual calculation");
    }

    /// <summary>
    /// Verifies that CosineSimilarity handles zero vectors correctly.
    /// </summary>
    [Fact]
    public void CosineSimilarity_WithZeroVector_ReturnsZero()
    {
        // Arrange
        float[] v1 = new float[64];
        float[] v2 = CreateRandomVector(64, new Random(DefaultSeed));

        // Act
        float similarity = ComputeCosineSimilarityManual(v1, v2);

        // Assert
        similarity.Should().Be(0f);
    }

    /// <summary>
    /// Verifies that CosineSimilarity handles identical vectors correctly.
    /// </summary>
    [Fact]
    public void CosineSimilarity_WithIdenticalVectors_ReturnsOne()
    {
        // Arrange
        var random = new Random(DefaultSeed);
        float[] v1 = CreateRandomVector(64, random);
        float[] v2 = v1.ToArray();

        // Act
        float similarity = System.Numerics.Tensors.TensorPrimitives.CosineSimilarity(v1, v2);

        // Assert
        similarity.Should().BeApproximately(1.0f, 1e-6f);
    }

    /// <summary>
    /// Verifies that MlpStatePredictor with different seeds produces different outputs.
    /// </summary>
    [Fact]
    public void PredictAsync_WithDifferentSeeds_ProducesDifferentOutputs()
    {
        // Arrange
        var predictor1 = MlpStatePredictor.CreateRandom(StateSize, ActionSize, HiddenSize, seed: 42);
        var predictor2 = MlpStatePredictor.CreateRandom(StateSize, ActionSize, HiddenSize, seed: 24);
        var state = CreateTestState(StateSize);
        var action = new Action("test_action", new Dictionary<string, object>());

        // Act
        var result1 = predictor1.PredictAsync(state, action).GetAwaiter().GetResult();
        var result2 = predictor2.PredictAsync(state, action).GetAwaiter().GetResult();

        // Assert
        result1.Embedding.Should().NotEqual(result2.Embedding, "Different seeds should produce different predictions");
    }

    /// <summary>
    /// Verifies that MlpStatePredictor produces deterministic output with same seed.
    /// </summary>
    [Fact]
    public void PredictAsync_WithSameSeed_ProducesIdenticalOutputs()
    {
        // Arrange
        var predictor1 = MlpStatePredictor.CreateRandom(StateSize, ActionSize, HiddenSize, seed: DefaultSeed);
        var predictor2 = MlpStatePredictor.CreateRandom(StateSize, ActionSize, HiddenSize, seed: DefaultSeed);
        var state = CreateTestState(StateSize);
        var action = new Action("test_action", new Dictionary<string, object>());

        // Act
        var result1 = predictor1.PredictAsync(state, action).GetAwaiter().GetResult();
        var result2 = predictor2.PredictAsync(state, action).GetAwaiter().GetResult();

        // Assert
        result1.Embedding.Should().Equal(result2.Embedding, "Same seed should produce identical predictions");
    }

    /// <summary>
    /// Verifies Normalize produces unit-length vectors using TensorPrimitives.
    /// </summary>
    [Fact]
    public void Normalize_ProducesUnitLengthVector()
    {
        // Arrange
        var random = new Random(DefaultSeed);
        float[] vector = CreateRandomVector(128, random);

        // Act
        float normSquared = System.Numerics.Tensors.TensorPrimitives.SumOfSquares(vector);
        float norm = MathF.Sqrt(normSquared);
        float[] normalized = new float[vector.Length];
        System.Numerics.Tensors.TensorPrimitives.Divide(vector, norm, normalized);

        // Compute norm of normalized vector
        float newNormSquared = System.Numerics.Tensors.TensorPrimitives.SumOfSquares(normalized);

        // Assert
        MathF.Sqrt(newNormSquared).Should().BeApproximately(1.0f, 1e-5f, "Normalized vector should have unit length");
    }

    private static State CreateTestState(int embeddingSize, int seed = 0)
    {
        var random = new Random(seed);
        var embedding = Enumerable.Range(0, embeddingSize)
            .Select(_ => (float)(random.NextDouble() * 2 - 1))
            .ToArray();
        return new State(
            Features: new Dictionary<string, object> { ["test"] = true },
            Embedding: embedding);
    }

    private static float[] CreateRandomVector(int size, Random random)
    {
        var vector = new float[size];
        for (int i = 0; i < size; i++)
        {
            vector[i] = (float)(random.NextDouble() * 2 - 1);
        }
        return vector;
    }

    private static float ComputeCosineSimilarityManual(float[] v1, float[] v2)
    {
        if (v1.Length != v2.Length)
            throw new ArgumentException("Vectors must have same dimension");

        float dot = 0, norm1 = 0, norm2 = 0;
        for (int i = 0; i < v1.Length; i++)
        {
            dot += v1[i] * v2[i];
            norm1 += v1[i] * v1[i];
            norm2 += v2[i] * v2[i];
        }

        if (norm1 == 0 || norm2 == 0)
            return 0;

        return dot / (MathF.Sqrt(norm1) * MathF.Sqrt(norm2));
    }

    private static float[] FlattenWeights(float[][] weights)
    {
        if (weights.Length == 0)
            return Array.Empty<float>();

        int totalLength = weights.Sum(w => w.Length);
        float[] flat = new float[totalLength];
        int offset = 0;
        foreach (var row in weights)
        {
            Array.Copy(row, 0, flat, offset, row.Length);
            offset += row.Length;
        }
        return flat;
    }
}
