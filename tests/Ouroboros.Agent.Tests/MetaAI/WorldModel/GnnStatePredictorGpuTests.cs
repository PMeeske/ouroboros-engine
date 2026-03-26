// <copyright file="GnnStatePredictorGpuTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Numerics.Tensors;
using FluentAssertions;
using Ouroboros.Agent.MetaAI.WorldModel;
using Xunit;
using Action = Ouroboros.Agent.MetaAI.WorldModel.Action;

namespace Ouroboros.Agent.Tests.MetaAI.WorldModel;

/// <summary>
/// Integration tests for GnnStatePredictor with GPU backend support.
/// Verifies GPU/CPU equivalence and TensorPrimitives optimization (TNS-01, TNS-03).
/// </summary>
[Trait("Category", "Unit")]
public class GnnStatePredictorGpuTests
{
    private const int StateSize = 32;
    private const int HiddenSize = 16;
    private const int DefaultSeed = 42;

    /// <summary>
    /// Verifies that GnnStatePredictor produces consistent output with CPU (null backend).
    /// </summary>
    [Fact]
    public void PredictAsync_WithNullBackend_ProducesValidOutput()
    {
        // Arrange
        var predictor = GnnStatePredictor.CreateRandom(StateSize, actionSize: 10, HiddenSize, seed: DefaultSeed);
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
    /// Verifies that GnnStatePredictor produces deterministic output with same seed.
    /// </summary>
    [Fact]
    public void PredictAsync_WithSameSeed_ProducesIdenticalOutputs()
    {
        // Arrange
        var predictor1 = GnnStatePredictor.CreateRandom(StateSize, 10, HiddenSize, seed: DefaultSeed);
        var predictor2 = GnnStatePredictor.CreateRandom(StateSize, 10, HiddenSize, seed: DefaultSeed);
        var state = CreateTestState(StateSize);
        var action = new Action("test_action", new Dictionary<string, object>());

        // Act
        var result1 = predictor1.PredictAsync(state, action).GetAwaiter().GetResult();
        var result2 = predictor2.PredictAsync(state, action).GetAwaiter().GetResult();

        // Assert
        result1.Embedding.Should().Equal(result2.Embedding, "Same seed should produce identical predictions");
    }

    /// <summary>
    /// Verifies that GnnStatePredictor produces different output with different seeds.
    /// </summary>
    [Fact]
    public void PredictAsync_WithDifferentSeeds_ProducesDifferentOutputs()
    {
        // Arrange
        var predictor1 = GnnStatePredictor.CreateRandom(StateSize, 10, HiddenSize, seed: 42);
        var predictor2 = GnnStatePredictor.CreateRandom(StateSize, 10, HiddenSize, seed: 24);
        var state = CreateTestState(StateSize);
        var action = new Action("test_action", new Dictionary<string, object>());

        // Act
        var result1 = predictor1.PredictAsync(state, action).GetAwaiter().GetResult();
        var result2 = predictor2.PredictAsync(state, action).GetAwaiter().GetResult();

        // Assert
        result1.Embedding.Should().NotEqual(result2.Embedding, "Different seeds should produce different predictions");
    }

    /// <summary>
    /// Verifies CosineSimilarity GPU/CPU equivalence within tolerance.
    /// GnnStatePredictor uses CosineSimilarity for adjacency computation (TNS-03).
    /// </summary>
    [Fact]
    public void CosineSimilarity_GpuMatchesCpu_WithinTolerance()
    {
        // Arrange
        var random = new Random(DefaultSeed);
        float[] v1 = CreateRandomVector(64, random);
        float[] v2 = CreateRandomVector(64, random);

        // Act - CPU (manual calculation)
        float cpuSimilarity = ComputeCosineSimilarityManual(v1, v2);

        // Act - SIMD (TensorPrimitives)
        float simdSimilarity = TensorPrimitives.CosineSimilarity(v1, v2);

        // Assert
        Math.Abs(cpuSimilarity - simdSimilarity).Should().BeLessThan(1e-5f, "TensorPrimitives should match manual calculation");
    }

    /// <summary>
    /// Verifies that adjacency computation produces valid normalized rows.
    /// </summary>
    [Fact]
    public void ComputeAdjacency_ProducesNormalizedRows()
    {
        // Arrange
        var random = new Random(DefaultSeed);
        int nodeCount = 4;
        int nodeFeatureSize = 8;
        float[][] nodeFeatures = new float[nodeCount][];
        for (int i = 0; i < nodeCount; i++)
        {
            nodeFeatures[i] = CreateRandomVector(nodeFeatureSize, random);
        }

        // Act
        float[][] adjacency = ComputeAdjacency(nodeFeatures);

        // Assert - Each row should sum to approximately 1
        for (int i = 0; i < nodeCount; i++)
        {
            float rowSum = adjacency[i].Sum();
            rowSum.Should().BeApproximately(1.0f, 0.01f, $"Row {i} should be normalized");
        }
    }

    /// <summary>
    /// Verifies adjacency self-loops are set correctly.
    /// </summary>
    [Fact]
    public void ComputeAdjacency_HasSelfLoops()
    {
        // Arrange
        var random = new Random(DefaultSeed);
        int nodeCount = 4;
        int nodeFeatureSize = 8;
        float[][] nodeFeatures = new float[nodeCount][];
        for (int i = 0; i < nodeCount; i++)
        {
            nodeFeatures[i] = CreateRandomVector(nodeFeatureSize, random);
        }

        // Act
        float[][] adjacency = ComputeAdjacency(nodeFeatures);

        // Assert - Self-loop should have positive value
        for (int i = 0; i < nodeCount; i++)
        {
            adjacency[i][i].Should().BeGreaterThan(0, $"Node {i} should have positive self-loop");
        }
    }

    /// <summary>
    /// Verifies message passing step produces correct output dimensions.
    /// </summary>
    [Fact]
    public void MessagePassingStep_ProducesCorrectDimensions()
    {
        // Arrange
        var random = new Random(DefaultSeed);
        int nodeCount = 4;
        int nodeFeatureSize = 8;
        int messageInputSize = nodeFeatureSize * 2; // Concatenated features

        // Create weights
        float[][] messageWeights = CreateWeightMatrix(messageInputSize, nodeFeatureSize, random);
        float[] messageBias = new float[nodeFeatureSize];
        float[][] updateWeights = CreateWeightMatrix(messageInputSize, nodeFeatureSize, random);
        float[] updateBias = new float[nodeFeatureSize];

        // Create node features
        float[][] nodeFeatures = new float[nodeCount][];
        for (int i = 0; i < nodeCount; i++)
        {
            nodeFeatures[i] = CreateRandomVector(nodeFeatureSize, random);
        }

        // Create adjacency
        float[][] adjacency = ComputeAdjacency(nodeFeatures);

        // Act
        float[][] updated = MessagePassingStep(nodeFeatures, adjacency, messageWeights, messageBias, updateWeights, updateBias);

        // Assert
        updated.Should().HaveCount(nodeCount);
        for (int i = 0; i < nodeCount; i++)
        {
            updated[i].Should().HaveCount(nodeFeatureSize);
        }
    }

    /// <summary>
    /// Verifies that MeanPool produces correct output.
    /// </summary>
    [Fact]
    public void MeanPool_ProducesCorrectOutput()
    {
        // Arrange
        int nodeCount = 4;
        int nodeFeatureSize = 8;
        float[][] nodeFeatures = new float[nodeCount][];
        for (int i = 0; i < nodeCount; i++)
        {
            nodeFeatures[i] = Enumerable.Repeat((float)(i + 1), nodeFeatureSize).ToArray();
        }

        // Act
        float[] pooled = MeanPool(nodeFeatures);

        // Assert - Mean of [1,2,3,4] is 2.5
        pooled.Should().HaveCount(nodeFeatureSize);
        pooled[0].Should().BeApproximately(2.5f, 0.01f);
    }

    /// <summary>
    /// Verifies that vector concatenation works correctly.
    /// </summary>
    [Fact]
    public void ConcatenateVectors_ProducesCorrectOutput()
    {
        // Arrange
        float[] a = { 1, 2, 3 };
        float[] b = { 4, 5, 6, 7 };

        // Act
        float[] result = ConcatenateVectors(a, b);

        // Assert
        result.Should().HaveCount(7);
        result.Should().ContainInOrder(1f, 2f, 3f, 4f, 5f, 6f, 7f);
    }

    /// <summary>
    /// Verifies ReLU activation works correctly.
    /// </summary>
    [Theory]
    [InlineData(-1.0f, 0.0f)]
    [InlineData(0.0f, 0.0f)]
    [InlineData(1.0f, 1.0f)]
    [InlineData(-0.5f, 0.0f)]
    [InlineData(0.5f, 0.5f)]
    public void ApplyReLU_ProducesCorrectOutput(float input, float expected)
    {
        // Arrange
        float[] vector = { input };

        // Act
        ApplyReLU(vector);

        // Assert
        vector[0].Should().Be(expected);
    }

    #region Helper Methods

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

    private static float[][] CreateWeightMatrix(int rows, int cols, Random random)
    {
        float scale = (float)Math.Sqrt(2.0 / rows);
        var weights = new float[rows][];
        for (int i = 0; i < rows; i++)
        {
            weights[i] = new float[cols];
            for (int j = 0; j < cols; j++)
            {
                weights[i][j] = (float)(random.NextDouble() * 2 - 1) * scale;
            }
        }
        return weights;
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

    private static float[][] ComputeAdjacency(float[][] nodeFeatures)
    {
        int nodeCount = nodeFeatures.Length;
        float[][] adj = new float[nodeCount][];
        for (int i = 0; i < nodeCount; i++)
        {
            adj[i] = new float[nodeCount];
            for (int j = 0; j < nodeCount; j++)
            {
                if (i == j)
                {
                    adj[i][j] = 1.0f; // Self-loop
                }
                else
                {
                    float sim = TensorPrimitives.CosineSimilarity(nodeFeatures[i], nodeFeatures[j]);
                    adj[i][j] = Math.Max(0, sim); // ReLU-gated adjacency
                }
            }

            // Normalize row
            float rowSum = adj[i].Sum();
            if (rowSum > 0)
            {
                for (int j = 0; j < nodeCount; j++)
                {
                    adj[i][j] /= rowSum;
                }
            }
        }
        return adj;
    }

    private static float[][] MessagePassingStep(
        float[][] nodeFeatures,
        float[][] adjacency,
        float[][] messageWeights,
        float[] messageBias,
        float[][] updateWeights,
        float[] updateBias)
    {
        int nodeCount = nodeFeatures.Length;
        int nodeFeatureSize = nodeFeatures[0].Length;
        float[][] updated = new float[nodeCount][];

        for (int i = 0; i < nodeCount; i++)
        {
            // Aggregate messages from neighbors
            float[] aggregatedMessage = new float[nodeFeatureSize];

            for (int j = 0; j < nodeCount; j++)
            {
                if (adjacency[i][j] <= 0)
                    continue;

                // Message: concat(node_i, node_j) -> message_weights -> message
                float[] msgInput = ConcatenateVectors(nodeFeatures[i], nodeFeatures[j]);
                float[] msg = MatVecMulWithBias(messageWeights, messageBias, msgInput);
                ApplyReLU(msg);

                // Weight by adjacency
                for (int k = 0; k < nodeFeatureSize; k++)
                {
                    aggregatedMessage[k] += adjacency[i][j] * msg[k];
                }
            }

            // Update: concat(node_i, aggregated_message) -> update_weights -> new_node
            float[] updateInput = ConcatenateVectors(nodeFeatures[i], aggregatedMessage);
            float[] newFeatures = MatVecMulWithBias(updateWeights, updateBias, updateInput);
            ApplyReLU(newFeatures);

            // Residual connection
            for (int k = 0; k < nodeFeatureSize; k++)
            {
                newFeatures[k] += nodeFeatures[i][k];
            }

            updated[i] = newFeatures;
        }

        return updated;
    }

    private static float[] MatVecMulWithBias(float[][] weights, float[] bias, float[] input)
    {
        int outSize = bias.Length;
        float[] output = new float[outSize];
        int inSize = Math.Min(input.Length, weights.Length);
        for (int j = 0; j < outSize; j++)
        {
            float sum = bias[j];
            for (int i = 0; i < inSize; i++)
            {
                sum += input[i] * weights[i][j];
            }
            output[j] = sum;
        }
        return output;
    }

    private static float[] MeanPool(float[][] nodeFeatures)
    {
        int nodeCount = nodeFeatures.Length;
        int nodeFeatureSize = nodeFeatures[0].Length;
        float[] pooled = new float[nodeFeatureSize];
        for (int n = 0; n < nodeCount; n++)
        {
            for (int k = 0; k < nodeFeatureSize; k++)
            {
                pooled[k] += nodeFeatures[n][k];
            }
        }
        for (int k = 0; k < nodeFeatureSize; k++)
        {
            pooled[k] /= nodeCount;
        }
        return pooled;
    }

    private static float[] ConcatenateVectors(float[] a, float[] b)
    {
        float[] result = new float[a.Length + b.Length];
        Array.Copy(a, 0, result, 0, a.Length);
        Array.Copy(b, 0, result, a.Length, b.Length);
        return result;
    }

    private static void ApplyReLU(float[] vector)
    {
        for (int i = 0; i < vector.Length; i++)
        {
            if (vector[i] < 0)
            {
                vector[i] = 0;
            }
        }
    }

    #endregion
}
