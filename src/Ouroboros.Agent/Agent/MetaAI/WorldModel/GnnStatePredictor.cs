// <copyright file="GnnStatePredictor.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System.Numerics.Tensors;
using Ouroboros.Tensor.Abstractions;

namespace Ouroboros.Agent.MetaAI.WorldModel;

/// <summary>
/// Graph Neural Network state predictor using message passing over state feature groups.
/// Partitions the state embedding into node groups and computes learned adjacency from
/// feature similarity, then propagates messages between nodes before projecting to the
/// output state embedding.
/// Supports optional GPU acceleration via <see cref="ITensorBackend"/>.
/// </summary>
public sealed class GnnStatePredictor : IStatePredictor
{
    private readonly int _embeddingSize;
    private readonly int _nodeCount;
    private readonly int _nodeFeatureSize;
    private readonly ITensorBackend? _backend;

    // Contiguous weight storage for GPU transfer (mutable for training)
    private float[] _messageWeightsFlat;
    private readonly TensorShape _messageWeightsShape;
    private float[] _messageBias;
    private float[] _updateWeightsFlat;
    private readonly TensorShape _updateWeightsShape;
    private float[] _updateBias;
    private float[] _readoutWeightsFlat;
    private readonly TensorShape _readoutWeightsShape;
    private float[] _readoutBias;
    private readonly int _messagePasses;

    // CPU fallback jagged arrays (rebuild from flat after training updates)
    private float[][] _messageWeights;
    private float[][] _updateWeights;
    private float[][] _readoutWeights;

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
        int messagePasses,
        ITensorBackend? backend)
    {
        _embeddingSize = embeddingSize;
        _nodeCount = nodeCount;
        _nodeFeatureSize = nodeFeatureSize;
        _messagePasses = messagePasses;
        _backend = backend;

        // Store original for CPU fallback
        _messageWeights = messageWeights;
        _messageBias = messageBias;
        _updateWeights = updateWeights;
        _updateBias = updateBias;
        _readoutWeights = readoutWeights;
        _readoutBias = readoutBias;

        // Flatten weights for GPU transfer
        int msgInputSize = nodeFeatureSize * 2;
        _messageWeightsFlat = FlattenWeights(messageWeights);
        _messageWeightsShape = TensorShape.Of(msgInputSize, nodeFeatureSize);

        _updateWeightsFlat = FlattenWeights(updateWeights);
        _updateWeightsShape = TensorShape.Of(msgInputSize, nodeFeatureSize);

        _readoutWeightsFlat = FlattenWeights(readoutWeights);
        _readoutWeightsShape = TensorShape.Of(nodeFeatureSize, embeddingSize);
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
        float[] output;

        if (_backend != null)
        {
            output = MatVecMulWithBiasGpu(_readoutWeightsFlat, _readoutWeightsShape, _readoutBias, pooled);
        }
        else
        {
            output = MatVecMulWithBiasCpu(_readoutWeights, _readoutBias, pooled);
        }

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
        => CreateRandom(stateSize, actionSize, hiddenSize, backend: null, seed);

    /// <summary>
    /// Creates a randomly initialized GNN state predictor with optional GPU acceleration.
    /// </summary>
    public static GnnStatePredictor CreateRandom(
        int stateSize, int actionSize, int hiddenSize, ITensorBackend? backend, int seed = 42)
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
            messagePasses: 2,
            backend: backend);
    }

    /// <summary>
    /// Converts jagged weight array to contiguous format for GPU transfer.
    /// </summary>
    private static float[] FlattenWeights(float[][] jagged)
    {
        int rows = jagged.Length;
        int cols = jagged[0].Length;
        float[] flat = new float[rows * cols];
        for (int i = 0; i < rows; i++)
        {
            Array.Copy(jagged[i], 0, flat, i * cols, cols);
        }

        return flat;
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
                float[] msg;

                if (_backend != null)
                {
                    msg = MatVecMulWithBiasGpu(_messageWeightsFlat, _messageWeightsShape, _messageBias, msgInput);
                }
                else
                {
                    msg = MatVecMulWithBiasCpu(_messageWeights, _messageBias, msgInput);
                }

                ApplyReLU(msg);

                // Weight by adjacency
                for (int k = 0; k < _nodeFeatureSize; k++)
                {
                    aggregatedMessage[k] += adjacency[i][j] * msg[k];
                }
            }

            // Update: concat(node_i, aggregated_message) → update_weights → new_node
            float[] updateInput = ConcatenateVectors(nodeFeatures[i], aggregatedMessage);
            float[] newFeatures;

            if (_backend != null)
            {
                newFeatures = MatVecMulWithBiasGpu(_updateWeightsFlat, _updateWeightsShape, _updateBias, updateInput);
            }
            else
            {
                newFeatures = MatVecMulWithBiasCpu(_updateWeights, _updateBias, updateInput);
            }

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

    /// <summary>
    /// GPU-accelerated cosine similarity using ITensorBackend.
    /// </summary>
    private float CosineSimilarity(float[] a, float[] b)
    {
        if (_backend != null)
        {
            // Use GPU for cosine similarity
            return CosineSimilarityGpu(a, b);
        }

        return CosineSimilarityCpu(a, b);
    }

    /// <summary>
    /// CPU cosine similarity using TensorPrimitives for SIMD acceleration (TNS-03).
    /// </summary>
    private static float CosineSimilarityCpu(float[] a, float[] b)
    {
        int len = Math.Min(a.Length, b.Length);
        return TensorPrimitives.CosineSimilarity(a.AsSpan()[..len], b.AsSpan()[..len]);
    }

    /// <summary>
    /// GPU-accelerated cosine similarity using MatMul for dot product.
    /// Note: For small vectors like node features, CPU may be faster due to GPU transfer overhead.
    /// </summary>
    private float CosineSimilarityGpu(float[] a, float[] b)
    {
        int len = Math.Min(a.Length, b.Length);

        // For small vectors, use CPU (GPU transfer overhead dominates)
        if (len < 64)
        {
            return CosineSimilarityCpu(a, b);
        }

        try
        {
            // Compute dot product via MatMul: [1, len] x [len, 1] = [1, 1]
            using var aTensor = _backend!.Create(TensorShape.Of(1, len), a.AsSpan()[..len]);
            using var bTensor = _backend.Create(TensorShape.Of(len, 1), b.AsSpan()[..len]);

            var dotResult = _backend.MatMul(aTensor, bTensor);
            if (dotResult.IsFailure)
            {
                return CosineSimilarityCpu(a, b);
            }

            float dot = dotResult.Value.AsSpan()[0];
            dotResult.Value.Dispose();

            // Compute norms via MatMul: [1, len] x [len, 1] for each
            float normA = ComputeNormGpu(a, len);
            float normB = ComputeNormGpu(b, len);

            float denom = normA * normB;
            return denom > 1e-8f ? dot / denom : 0;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Fall back to CPU on any GPU error
            return CosineSimilarityCpu(a, b);
        }
    }

    /// <summary>
    /// Computes L2 norm using GPU MatMul.
    /// </summary>
    private float ComputeNormGpu(float[] v, int len)
    {
        // Norm via element-wise square then sum
        // For efficiency, compute on CPU for small vectors
        float norm = 0;
        for (int i = 0; i < len; i++)
        {
            norm += v[i] * v[i];
        }

        return (float)Math.Sqrt(norm);
    }

    /// <summary>
    /// GPU-accelerated matrix-vector multiplication using ITensorBackend.MatMul.
    /// </summary>
    private float[] MatVecMulWithBiasGpu(float[] weightsFlat, TensorShape weightsShape, float[] bias, float[] input)
    {
        // Reshape input as row vector: [1, inputLen]
        using var inputTensor = _backend!.Create(TensorShape.Of(1, input.Length), input.AsSpan());
        using var weightsTensor = _backend.FromMemory(weightsFlat.AsMemory(), weightsShape);

        // Matrix multiplication: [1, inputLen] x [inputLen, outputLen] = [1, outputLen]
        var result = _backend.MatMul(inputTensor, weightsTensor);

        if (result.IsFailure)
        {
            throw new InvalidOperationException($"MatMul failed: {result.Error}");
        }

        // Extract result and add bias
        var output = result.Value.AsSpan().ToArray();
        AddBiasInPlace(output, bias);
        result.Value.Dispose();

        return output;
    }

    /// <summary>
    /// CPU fallback for matrix-vector multiplication (original implementation).
    /// </summary>
    private static float[] MatVecMulWithBiasCpu(float[][] weights, float[] bias, float[] input)
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

    private static void AddBiasInPlace(float[] output, float[] bias)
    {
        for (int i = 0; i < output.Length && i < bias.Length; i++)
        {
            output[i] += bias[i];
        }
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

    /// <summary>
    /// Computes mean squared error loss between predicted and actual state embeddings.
    /// </summary>
    /// <param name="predicted">The predicted state.</param>
    /// <param name="actual">The actual (ground truth) state.</param>
    /// <returns>MSE value: mean of squared differences per embedding dimension.</returns>
    public float ComputeLoss(State predicted, State actual)
    {
        int len = Math.Min(predicted.Embedding.Length, actual.Embedding.Length);
        if (len == 0)
        {
            return 0f;
        }

        float sum = 0f;
        for (int i = 0; i < len; i++)
        {
            float diff = predicted.Embedding[i] - actual.Embedding[i];
            sum += diff * diff;
        }

        return sum / len;
    }

    /// <summary>
    /// Computes average MSE loss over a batch of experience tuples.
    /// For each experience, predicts next state from (current, action) and compares to actual next state.
    /// </summary>
    /// <param name="batch">Collection of experience tuples.</param>
    /// <returns>Average MSE loss across the batch.</returns>
    public float ComputeLoss(IEnumerable<Experience> batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        float totalLoss = 0f;
        int count = 0;

        foreach (var exp in batch)
        {
            var predicted = PredictAsync(exp.Current, exp.Action).GetAwaiter().GetResult();
            totalLoss += ComputeLoss(predicted, exp.NextState);
            count++;
        }

        return count > 0 ? totalLoss / count : 0f;
    }

    /// <summary>
    /// Computes finite-difference gradients for all weight arrays.
    /// Uses central differences: gradient[i] = (loss(w+delta) - loss(w-delta)) / (2*delta).
    /// </summary>
    /// <param name="batch">Experience batch to compute loss over.</param>
    /// <param name="delta">Perturbation magnitude for finite differences.</param>
    /// <returns>Dictionary mapping parameter names to gradient arrays.</returns>
    /// <remarks>
    /// Complexity is O(n * batch_size) where n is the total number of weights.
    /// For large weight arrays (>1000 elements), this is expensive and could be
    /// optimized with analytical gradients in a future iteration.
    /// </remarks>
    public Dictionary<string, float[]> ComputeGradients(IEnumerable<Experience> batch, float delta = 1e-5f)
    {
        ArgumentNullException.ThrowIfNull(batch);

        // Materialize batch once to avoid multiple enumeration
        var batchList = batch as IList<Experience> ?? batch.ToList();

        var gradients = new Dictionary<string, float[]>();

        // Compute gradients for each weight array
        gradients["messageWeights"] = ComputeGradientForArray(_messageWeightsFlat, batchList, delta);
        gradients["messageBias"] = ComputeGradientForArray(_messageBias, batchList, delta);
        gradients["updateWeights"] = ComputeGradientForArray(_updateWeightsFlat, batchList, delta);
        gradients["updateBias"] = ComputeGradientForArray(_updateBias, batchList, delta);
        gradients["readoutWeights"] = ComputeGradientForArray(_readoutWeightsFlat, batchList, delta);
        gradients["readoutBias"] = ComputeGradientForArray(_readoutBias, batchList, delta);

        return gradients;
    }

    /// <summary>
    /// Executes a single training step: sample from buffer, compute gradients, update weights.
    /// </summary>
    /// <param name="buffer">Experience buffer to sample from.</param>
    /// <param name="learningRate">Step size for gradient descent.</param>
    /// <param name="batchSize">Number of experiences to sample per step.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The loss value before the weight update (for logging).</returns>
    public async Task<float> TrainStepAsync(ExperienceBuffer buffer, float learningRate = 0.01f, int batchSize = 32, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        var batch = buffer.Sample(batchSize);

        // Record loss before update
        float lossBeforeUpdate = ComputeLoss(batch);

        // Compute gradients via finite differences
        var gradients = ComputeGradients(batch);

        // Apply gradient descent: w[i] -= learningRate * gradient[i]
        ApplyGradientUpdate(_messageWeightsFlat, gradients["messageWeights"], learningRate);
        ApplyGradientUpdate(_messageBias, gradients["messageBias"], learningRate);
        ApplyGradientUpdate(_updateWeightsFlat, gradients["updateWeights"], learningRate);
        ApplyGradientUpdate(_updateBias, gradients["updateBias"], learningRate);
        ApplyGradientUpdate(_readoutWeightsFlat, gradients["readoutWeights"], learningRate);
        ApplyGradientUpdate(_readoutBias, gradients["readoutBias"], learningRate);

        // Rebuild CPU fallback jagged arrays from updated flat arrays
        RebuildJaggedArrays();

        return lossBeforeUpdate;
    }

    /// <summary>
    /// Returns current flat weight arrays as jagged for inspection.
    /// </summary>
    /// <returns>Array of flat weight arrays: messageWeights, messageBias, updateWeights, updateBias, readoutWeights, readoutBias.</returns>
    public float[][] GetAllWeights()
    {
        return new float[][]
        {
            _messageWeightsFlat,
            _messageBias,
            _updateWeightsFlat,
            _updateBias,
            _readoutWeightsFlat,
            _readoutBias,
        };
    }

    /// <summary>
    /// Replaces a weight array by name. Used for testing and parameter inspection.
    /// </summary>
    /// <param name="name">Parameter name: messageWeights, messageBias, updateWeights, updateBias, readoutWeights, readoutBias.</param>
    /// <param name="weights">New weight values.</param>
    public void SetAllWeights(string name, float[] weights)
    {
        ArgumentNullException.ThrowIfNull(weights);

        switch (name)
        {
            case "messageWeights":
                _messageWeightsFlat = weights;
                break;
            case "messageBias":
                _messageBias = weights;
                break;
            case "updateWeights":
                _updateWeightsFlat = weights;
                break;
            case "updateBias":
                _updateBias = weights;
                break;
            case "readoutWeights":
                _readoutWeightsFlat = weights;
                break;
            case "readoutBias":
                _readoutBias = weights;
                break;
            default:
                throw new ArgumentException($"Unknown weight name: {name}", nameof(name));
        }

        RebuildJaggedArrays();
    }

    /// <summary>
    /// Computes finite-difference gradient for a single weight array.
    /// </summary>
    private float[] ComputeGradientForArray(float[] weights, IList<Experience> batch, float delta)
    {
        float[] gradient = new float[weights.Length];

        for (int i = 0; i < weights.Length; i++)
        {
            float original = weights[i];

            // Perturb +delta
            weights[i] = original + delta;
            RebuildJaggedArrays();
            float lossPlus = ComputeLoss(batch);

            // Perturb -delta
            weights[i] = original - delta;
            RebuildJaggedArrays();
            float lossMinus = ComputeLoss(batch);

            // Restore original
            weights[i] = original;

            // Central difference gradient
            gradient[i] = (lossPlus - lossMinus) / (2f * delta);
        }

        // Ensure jagged arrays are consistent after gradient computation
        RebuildJaggedArrays();

        return gradient;
    }

    /// <summary>
    /// Applies gradient descent update to a weight array.
    /// </summary>
    private static void ApplyGradientUpdate(float[] weights, float[] gradients, float learningRate)
    {
        for (int i = 0; i < weights.Length; i++)
        {
            weights[i] -= learningRate * gradients[i];
        }
    }

    /// <summary>
    /// Reconstructs CPU fallback jagged arrays from flat weight arrays.
    /// Called after training updates modify the flat arrays.
    /// </summary>
    private void RebuildJaggedArrays()
    {
        _messageWeights = RebuildJaggedFromFlat(_messageWeightsFlat, _messageWeightsShape);
        _updateWeights = RebuildJaggedFromFlat(_updateWeightsFlat, _updateWeightsShape);
        _readoutWeights = RebuildJaggedFromFlat(_readoutWeightsFlat, _readoutWeightsShape);
    }

    /// <summary>
    /// Reconstructs a jagged array from a flat array using the stored shape dimensions.
    /// </summary>
    private static float[][] RebuildJaggedFromFlat(float[] flat, TensorShape shape)
    {
        int rows = shape.Dimensions[0];
        int cols = shape.Dimensions[1];
        float[][] jagged = new float[rows][];
        for (int i = 0; i < rows; i++)
        {
            jagged[i] = new float[cols];
            Array.Copy(flat, i * cols, jagged[i], 0, cols);
        }

        return jagged;
    }
}
