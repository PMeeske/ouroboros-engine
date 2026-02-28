// <copyright file="GnnStatePredictor.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Agent.MetaAI.WorldModel;

/// <summary>
/// Graph Neural Network state predictor using message passing over state feature groups.
/// Partitions the state embedding into node groups and computes learned adjacency from
/// feature similarity, then propagates messages between nodes before projecting to the
/// output state embedding.
/// </summary>
public sealed class GnnStatePredictor : IStatePredictor
{
    private readonly int _embeddingSize;
    private readonly int _nodeCount;
    private readonly int _nodeFeatureSize;
    private readonly float[][] _messageWeights;
    private readonly float[] _messageBias;
    private readonly float[][] _updateWeights;
    private readonly float[] _updateBias;
    private readonly float[][] _readoutWeights;
    private readonly float[] _readoutBias;
    private readonly int _messagePasses;

    private GnnStatePredictor(
        int embeddingSize,
        int nodeCount,
        int nodeFeatureSize,
        float[][] messageWeights,
        float[] messageBias,
        float[][] updateWeights,
        float[] updateBias,
        float[][] readoutWeights,
        float[] readoutBias,
        int messagePasses)
    {
        _embeddingSize = embeddingSize;
        _ = _embeddingSize;
        _nodeCount = nodeCount;
        _nodeFeatureSize = nodeFeatureSize;
        _messageWeights = messageWeights;
        _messageBias = messageBias;
        _updateWeights = updateWeights;
        _updateBias = updateBias;
        _readoutWeights = readoutWeights;
        _readoutBias = readoutBias;
        _messagePasses = messagePasses;
    }

    /// <inheritdoc/>
    public Task<State> PredictAsync(State current, Action action, CancellationToken ct = default)
    {
        // Partition embedding into node features
        float[][] nodeFeatures = PartitionIntoNodes(current.Embedding);

        // Compute adjacency matrix from feature similarity
        float[][] adjacency = ComputeAdjacency(nodeFeatures);

        // Incorporate action by modifying node features
        float[] actionEmbedding = EncodeAction(action);
        InjectActionIntoNodes(nodeFeatures, actionEmbedding);

        // Message passing rounds
        for (int pass = 0; pass < _messagePasses; pass++)
        {
            nodeFeatures = MessagePassingStep(nodeFeatures, adjacency);
        }

        // Graph-level readout: mean pool node features then project
        float[] pooled = MeanPool(nodeFeatures);
        float[] output = MatVecMulWithBias(_readoutWeights, _readoutBias, pooled);

        var predictedState = new State(
            Features: new Dictionary<string, object>(current.Features),
            Embedding: output);

        return Task.FromResult(predictedState);
    }

    /// <summary>
    /// Creates a randomly initialized GNN state predictor.
    /// </summary>
    public static GnnStatePredictor CreateRandom(
        int stateSize, int actionSize, int hiddenSize, int seed = 42)
    {
        var random = new Random(seed);

        // Partition state into nodes: each node gets a slice of the embedding
        int nodeCount = Math.Max(4, stateSize / 4);
        int nodeFeatureSize = (stateSize + nodeCount - 1) / nodeCount; // ceiling division

        // Message function: maps 2*nodeFeatureSize → nodeFeatureSize
        int msgInputSize = nodeFeatureSize * 2;
        float msgScale = (float)Math.Sqrt(2.0 / msgInputSize);

        // Update function: maps 2*nodeFeatureSize → nodeFeatureSize (concat node + aggregated message)
        float updateScale = (float)Math.Sqrt(2.0 / msgInputSize);

        // Readout: maps nodeFeatureSize → stateSize
        float readoutScale = (float)Math.Sqrt(2.0 / nodeFeatureSize);

        return new GnnStatePredictor(
            embeddingSize: stateSize,
            nodeCount: nodeCount,
            nodeFeatureSize: nodeFeatureSize,
            messageWeights: InitWeights(random, msgInputSize, nodeFeatureSize, msgScale),
            messageBias: new float[nodeFeatureSize],
            updateWeights: InitWeights(random, msgInputSize, nodeFeatureSize, updateScale),
            updateBias: new float[nodeFeatureSize],
            readoutWeights: InitWeights(random, nodeFeatureSize, stateSize, readoutScale),
            readoutBias: new float[stateSize],
            messagePasses: 2);
    }

    private float[][] PartitionIntoNodes(float[] embedding)
    {
        float[][] nodes = new float[_nodeCount][];
        for (int n = 0; n < _nodeCount; n++)
        {
            nodes[n] = new float[_nodeFeatureSize];
            int start = n * _nodeFeatureSize;
            int count = Math.Min(_nodeFeatureSize, embedding.Length - start);
            if (count > 0)
            {
                Array.Copy(embedding, start, nodes[n], 0, count);
            }
        }

        return nodes;
    }

    private float[][] ComputeAdjacency(float[][] nodeFeatures)
    {
        // Compute cosine similarity between all pairs, then apply threshold
        float[][] adj = new float[_nodeCount][];
        for (int i = 0; i < _nodeCount; i++)
        {
            adj[i] = new float[_nodeCount];
            for (int j = 0; j < _nodeCount; j++)
            {
                if (i == j)
                {
                    adj[i][j] = 1.0f; // Self-loop
                }
                else
                {
                    float sim = CosineSimilarity(nodeFeatures[i], nodeFeatures[j]);
                    adj[i][j] = Math.Max(0, sim); // ReLU-gated adjacency
                }
            }

            // Normalize row to sum to 1
            float rowSum = 0;
            for (int j = 0; j < _nodeCount; j++)
            {
                rowSum += adj[i][j];
            }

            if (rowSum > 0)
            {
                for (int j = 0; j < _nodeCount; j++)
                {
                    adj[i][j] /= rowSum;
                }
            }
        }

        return adj;
    }

    private static void InjectActionIntoNodes(float[][] nodeFeatures, float[] actionEmbedding)
    {
        // Add action signal to each node (broadcast)
        for (int n = 0; n < nodeFeatures.Length; n++)
        {
            int overlap = Math.Min(nodeFeatures[n].Length, actionEmbedding.Length);
            for (int i = 0; i < overlap; i++)
            {
                nodeFeatures[n][i] += actionEmbedding[i] * 0.1f; // Scaled injection
            }
        }
    }

    private float[][] MessagePassingStep(float[][] nodeFeatures, float[][] adjacency)
    {
        float[][] updated = new float[_nodeCount][];

        for (int i = 0; i < _nodeCount; i++)
        {
            // Aggregate messages from neighbors
            float[] aggregatedMessage = new float[_nodeFeatureSize];

            for (int j = 0; j < _nodeCount; j++)
            {
                if (adjacency[i][j] <= 0)
                {
                    continue;
                }

                // Message: concat(node_i, node_j) → message_weights → message
                float[] msgInput = ConcatenateVectors(nodeFeatures[i], nodeFeatures[j]);
                float[] msg = MatVecMulWithBias(_messageWeights, _messageBias, msgInput);
                ApplyReLU(msg);

                // Weight by adjacency
                for (int k = 0; k < _nodeFeatureSize; k++)
                {
                    aggregatedMessage[k] += adjacency[i][j] * msg[k];
                }
            }

            // Update: concat(node_i, aggregated_message) → update_weights → new_node
            float[] updateInput = ConcatenateVectors(nodeFeatures[i], aggregatedMessage);
            float[] newFeatures = MatVecMulWithBias(_updateWeights, _updateBias, updateInput);
            ApplyReLU(newFeatures);

            // Residual connection
            for (int k = 0; k < _nodeFeatureSize; k++)
            {
                newFeatures[k] += nodeFeatures[i][k];
            }

            updated[i] = newFeatures;
        }

        return updated;
    }

    private float[] MeanPool(float[][] nodeFeatures)
    {
        float[] pooled = new float[_nodeFeatureSize];
        for (int n = 0; n < _nodeCount; n++)
        {
            for (int k = 0; k < _nodeFeatureSize; k++)
            {
                pooled[k] += nodeFeatures[n][k];
            }
        }

        for (int k = 0; k < _nodeFeatureSize; k++)
        {
            pooled[k] /= _nodeCount;
        }

        return pooled;
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

    private static float CosineSimilarity(float[] a, float[] b)
    {
        float dot = 0, normA = 0, normB = 0;
        int len = Math.Min(a.Length, b.Length);
        for (int i = 0; i < len; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        float denom = (float)(Math.Sqrt(normA) * Math.Sqrt(normB));
        return denom > 1e-8f ? dot / denom : 0;
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
