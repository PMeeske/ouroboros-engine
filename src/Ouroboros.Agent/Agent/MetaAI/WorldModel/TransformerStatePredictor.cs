// <copyright file="TransformerStatePredictor.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Agent.MetaAI.WorldModel;

/// <summary>
/// Transformer-inspired state predictor using self-attention over state embedding features.
/// Applies scaled dot-product attention to capture temporal relationships in the
/// state representation, followed by a feed-forward layer that incorporates the action.
/// </summary>
public sealed class TransformerStatePredictor : IStatePredictor
{
    private readonly int _embeddingSize;
    private readonly int _headDim;
    private readonly float[][] _queryWeights;
    private readonly float[][] _keyWeights;
    private readonly float[][] _valueWeights;
    private readonly float[][] _outputProjection;
    private readonly float[] _outputBias;
    private readonly float[][] _ffnWeights1;
    private readonly float[] _ffnBias1;
    private readonly float[][] _ffnWeights2;
    private readonly float[] _ffnBias2;

    private TransformerStatePredictor(
        int embeddingSize,
        int headDim,
        float[][] queryWeights,
        float[][] keyWeights,
        float[][] valueWeights,
        float[][] outputProjection,
        float[] outputBias,
        float[][] ffnWeights1,
        float[] ffnBias1,
        float[][] ffnWeights2,
        float[] ffnBias2)
    {
        _embeddingSize = embeddingSize;
        _ = _embeddingSize;
        _headDim = headDim;
        _queryWeights = queryWeights;
        _keyWeights = keyWeights;
        _valueWeights = valueWeights;
        _outputProjection = outputProjection;
        _outputBias = outputBias;
        _ffnWeights1 = ffnWeights1;
        _ffnBias1 = ffnBias1;
        _ffnWeights2 = ffnWeights2;
        _ffnBias2 = ffnBias2;
    }

    /// <inheritdoc/>
    public Task<State> PredictAsync(State current, Action action, CancellationToken ct = default)
    {
        float[] embedding = current.Embedding;

        // Self-attention: treat each pair of embedding dimensions as a sequence element
        // Q, K, V projections
        float[] query = MatVecMul(_queryWeights, embedding);
        float[] key = MatVecMul(_keyWeights, embedding);
        float[] value = MatVecMul(_valueWeights, embedding);

        // Scaled dot-product attention
        float[] attended = ScaledDotProductAttention(query, key, value);

        // Output projection
        float[] projected = MatVecMulWithBias(_outputProjection, _outputBias, attended);

        // Residual connection
        float[] residual = AddVectors(embedding, projected);

        // Layer norm (simplified: mean-center + scale)
        float[] normed = LayerNorm(residual);

        // Incorporate action via concatenation then feed-forward compression
        float[] actionEmbedding = EncodeAction(action);
        float[] combined = ConcatenateVectors(normed, actionEmbedding);

        // Feed-forward network: hidden with ReLU, then output
        float[] hidden = MatVecMulWithBias(_ffnWeights1, _ffnBias1, combined);
        ApplyReLU(hidden);
        float[] output = MatVecMulWithBias(_ffnWeights2, _ffnBias2, hidden);

        var predictedState = new State(
            Features: new Dictionary<string, object>(current.Features),
            Embedding: output);

        return Task.FromResult(predictedState);
    }

    /// <summary>
    /// Creates a randomly initialized Transformer state predictor.
    /// </summary>
    public static TransformerStatePredictor CreateRandom(
        int stateSize, int actionSize, int hiddenSize, int seed = 42)
    {
        var random = new Random(seed);
        int headDim = Math.Max(8, stateSize / 4);
        int ffnInputSize = stateSize + actionSize;
        float qkvScale = (float)Math.Sqrt(2.0 / stateSize);
        float ffnScale = (float)Math.Sqrt(2.0 / ffnInputSize);
        float outScale = (float)Math.Sqrt(2.0 / hiddenSize);

        return new TransformerStatePredictor(
            embeddingSize: stateSize,
            headDim: headDim,
            queryWeights: InitWeights(random, stateSize, headDim, qkvScale),
            keyWeights: InitWeights(random, stateSize, headDim, qkvScale),
            valueWeights: InitWeights(random, stateSize, headDim, qkvScale),
            outputProjection: InitWeights(random, headDim, stateSize, qkvScale),
            outputBias: new float[stateSize],
            ffnWeights1: InitWeights(random, ffnInputSize, hiddenSize, ffnScale),
            ffnBias1: new float[hiddenSize],
            ffnWeights2: InitWeights(random, hiddenSize, stateSize, outScale),
            ffnBias2: new float[stateSize]);
    }

    private float[] ScaledDotProductAttention(float[] query, float[] key, float[] value)
    {
        // Compute attention score: (Q . K) / sqrt(d_k)
        float scale = (float)Math.Sqrt(_headDim);
        float score = DotProduct(query, key) / scale;

        // Softmax over single score → 1.0 (self-attention on single vector)
        // For a single sequence element, attention weight is always 1.0 after softmax
        // So the output is simply the value vector scaled by the attention pattern
        float weight = Sigmoid(score); // Use sigmoid as a soft gate instead

        float[] result = new float[value.Length];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = value[i] * weight;
        }

        return result;
    }

    private static float[] EncodeAction(Action action)
    {
        int hash = action.Name.GetHashCode();
        float[] embedding = new float[10];
        for (int i = 0; i < embedding.Length; i++)
        {
            embedding[i] = (float)Math.Sin(hash * (i + 1) * 0.1);
        }

        return embedding;
    }

    private static float[] MatVecMul(float[][] weights, float[] input)
    {
        int outSize = weights.Length > 0 ? weights[0].Length : 0;
        float[] output = new float[outSize];
        int inSize = Math.Min(input.Length, weights.Length);
        for (int j = 0; j < outSize; j++)
        {
            float sum = 0;
            for (int i = 0; i < inSize; i++)
            {
                sum += input[i] * weights[i][j];
            }

            output[j] = sum;
        }

        return output;
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

    private static float DotProduct(float[] a, float[] b)
    {
        float sum = 0;
        int len = Math.Min(a.Length, b.Length);
        for (int i = 0; i < len; i++)
        {
            sum += a[i] * b[i];
        }

        return sum;
    }

    private static float Sigmoid(float x) => 1.0f / (1.0f + (float)Math.Exp(-Math.Clamp(x, -20, 20)));

    private static float[] AddVectors(float[] a, float[] b)
    {
        int len = Math.Min(a.Length, b.Length);
        float[] result = new float[a.Length];
        Array.Copy(a, result, a.Length);
        for (int i = 0; i < len; i++)
        {
            result[i] += b[i];
        }

        return result;
    }

    private static float[] LayerNorm(float[] input)
    {
        float mean = 0;
        for (int i = 0; i < input.Length; i++)
        {
            mean += input[i];
        }

        mean /= input.Length;

        float variance = 0;
        for (int i = 0; i < input.Length; i++)
        {
            float diff = input[i] - mean;
            variance += diff * diff;
        }

        variance /= input.Length;
        float stddev = (float)Math.Sqrt(variance + 1e-6);

        float[] output = new float[input.Length];
        for (int i = 0; i < input.Length; i++)
        {
            output[i] = (input[i] - mean) / stddev;
        }

        return output;
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

    private static float[][] InitWeights(Random random, int rows, int cols, float scale)
    {
        float[][] weights = new float[rows][];
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
}
