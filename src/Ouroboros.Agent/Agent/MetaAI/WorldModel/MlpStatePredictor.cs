// <copyright file="MlpStatePredictor.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Tensor.Abstractions;

namespace Ouroboros.Agent.MetaAI.WorldModel;

/// <summary>
/// Multi-layer perceptron based state predictor.
/// Simple feed-forward neural network for state transition prediction.
/// Follows immutable and functional programming principles.
/// Supports optional GPU acceleration via <see cref="ITensorBackend"/>.
/// </summary>
public sealed class MlpStatePredictor : IStatePredictor
{
    private readonly int _inputSize;
    private readonly int _hiddenSize;
    private readonly int _outputSize;
    private readonly ITensorBackend? _backend;

    // Contiguous weight storage for GPU transfer
    private readonly float[] _weightsInputHiddenFlat;
    private readonly TensorShape _weightsInputHiddenShape;
    private readonly float[] _biasHidden;
    private readonly float[] _weightsHiddenOutputFlat;
    private readonly TensorShape _weightsHiddenOutputShape;
    private readonly float[] _biasOutput;

    // Original jagged arrays for CPU fallback
    private readonly float[][] _weightsInputHidden;
    private readonly float[][] _weightsHiddenOutput;

    /// <summary>
    /// Initializes a new instance of the <see cref="MlpStatePredictor"/> class.
    /// </summary>
    /// <param name="inputSize">Size of input layer (state + action embedding).</param>
    /// <param name="hiddenSize">Size of hidden layer.</param>
    /// <param name="outputSize">Size of output layer (next state embedding).</param>
    /// <param name="weightsInputHidden">Weights from input to hidden layer [inputSize, hiddenSize].</param>
    /// <param name="biasHidden">Bias for hidden layer.</param>
    /// <param name="weightsHiddenOutput">Weights from hidden to output layer [hiddenSize, outputSize].</param>
    /// <param name="biasOutput">Bias for output layer.</param>
    /// <param name="backend">Optional tensor backend for GPU acceleration.</param>
    public MlpStatePredictor(
        int inputSize,
        int hiddenSize,
        int outputSize,
        float[][] weightsInputHidden,
        float[] biasHidden,
        float[][] weightsHiddenOutput,
        float[] biasOutput,
        ITensorBackend? backend = null)
    {
        _inputSize = inputSize;
        _hiddenSize = hiddenSize;
        _outputSize = outputSize;
        _backend = backend;

        // Store original jagged arrays for CPU fallback
        _weightsInputHidden = weightsInputHidden;
        _weightsHiddenOutput = weightsHiddenOutput;
        _biasHidden = biasHidden;
        _biasOutput = biasOutput;

        // Flatten weights for GPU transfer
        _weightsInputHiddenFlat = FlattenWeights(weightsInputHidden);
        _weightsInputHiddenShape = TensorShape.Of(inputSize, hiddenSize);
        _weightsHiddenOutputFlat = FlattenWeights(weightsHiddenOutput);
        _weightsHiddenOutputShape = TensorShape.Of(hiddenSize, outputSize);
    }

    /// <inheritdoc/>
    public Task<State> PredictAsync(State current, Action action, CancellationToken ct = default)
    {
        // Concatenate state embedding with action embedding
        var actionEmbedding = EncodeAction(action);
        var input = ConcatenateEmbeddings(current.Embedding, actionEmbedding);

        // Forward pass through network
        float[] hidden;
        float[] output;

        if (_backend != null)
        {
            // GPU-accelerated path
            hidden = ForwardLayerGpu(input, _weightsInputHiddenFlat, _weightsInputHiddenShape, _biasHidden, relu: true);
            output = ForwardLayerGpu(hidden, _weightsHiddenOutputFlat, _weightsHiddenOutputShape, _biasOutput, relu: false);
        }
        else
        {
            // CPU fallback (original implementation)
            hidden = ForwardLayerCpu(input, _weightsInputHidden, _biasHidden, relu: true);
            output = ForwardLayerCpu(hidden, _weightsHiddenOutput, _biasOutput, relu: false);
        }

        // Create new state with predicted embedding
        var predictedState = new State(
            Features: new Dictionary<string, object>(current.Features),
            Embedding: output);

        return Task.FromResult(predictedState);
    }

    /// <summary>
    /// Creates a randomly initialized MLP predictor.
    /// </summary>
    /// <param name="stateSize">Size of state embeddings.</param>
    /// <param name="actionSize">Size of action embeddings.</param>
    /// <param name="hiddenSize">Size of hidden layer.</param>
    /// <param name="seed">Random seed for reproducibility.</param>
    /// <returns>A new MLP state predictor without GPU acceleration.</returns>
    public static MlpStatePredictor CreateRandom(int stateSize, int actionSize, int hiddenSize, int seed = 42)
        => CreateRandom(stateSize, actionSize, hiddenSize, backend: null, seed);

    /// <summary>
    /// Creates a randomly initialized MLP predictor with optional GPU acceleration.
    /// </summary>
    /// <param name="stateSize">Size of state embeddings.</param>
    /// <param name="actionSize">Size of action embeddings.</param>
    /// <param name="hiddenSize">Size of hidden layer.</param>
    /// <param name="backend">Optional tensor backend for GPU acceleration.</param>
    /// <param name="seed">Random seed for reproducibility.</param>
    /// <returns>A new MLP state predictor.</returns>
    public static MlpStatePredictor CreateRandom(int stateSize, int actionSize, int hiddenSize, ITensorBackend? backend, int seed = 42)
    {
        var random = new Random(seed);
        int inputSize = stateSize + actionSize;

        // Xavier initialization
        float inputScale = (float)Math.Sqrt(2.0 / inputSize);
        float hiddenScale = (float)Math.Sqrt(2.0 / hiddenSize);

        var weightsInputHidden = InitializeWeights(random, inputSize, hiddenSize, inputScale);
        var biasHidden = new float[hiddenSize];
        var weightsHiddenOutput = InitializeWeights(random, hiddenSize, stateSize, hiddenScale);
        var biasOutput = new float[stateSize];

        return new MlpStatePredictor(
            inputSize,
            hiddenSize,
            stateSize,
            weightsInputHidden,
            biasHidden,
            weightsHiddenOutput,
            biasOutput,
            backend);
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

    /// <summary>
    /// GPU-accelerated forward layer computation using ITensorBackend.MatMul.
    /// </summary>
    private float[] ForwardLayerGpu(float[] input, float[] weightsFlat, TensorShape weightsShape, float[] bias, bool relu)
    {
        // Create tensors
        using var inputTensor = _backend!.Create(TensorShape.Of(1, input.Length), input.AsSpan());
        using var weightsTensor = _backend.FromMemory(weightsFlat.AsMemory(), weightsShape);

        // GPU-accelerated matrix multiplication: [1, inputLen] x [inputLen, outputLen] = [1, outputLen]
        var result = _backend.MatMul(inputTensor, weightsTensor);

        if (result.IsFailure)
        {
            throw new InvalidOperationException($"MatMul failed: {result.Error}");
        }

        // Extract result and add bias
        var output = result.Value.AsSpan().ToArray();
        AddBiasInPlace(output, bias);

        if (relu)
        {
            ApplyReLUInPlace(output);
        }

        result.Value.Dispose();
        return output;
    }

    /// <summary>
    /// CPU fallback for forward layer (original implementation).
    /// </summary>
    private static float[] ForwardLayerCpu(float[] input, float[][] weights, float[] bias, bool relu)
    {
        var output = new float[weights[0].Length];

        // Matrix multiplication: input * weights + bias
        for (int j = 0; j < output.Length; j++)
        {
            float sum = bias[j];
            for (int i = 0; i < input.Length; i++)
            {
                sum += input[i] * weights[i][j];
            }

            output[j] = relu ? Math.Max(0, sum) : sum;
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

    private static void ApplyReLUInPlace(float[] output)
    {
        for (int i = 0; i < output.Length; i++)
        {
            if (output[i] < 0)
            {
                output[i] = 0;
            }
        }
    }

    private static float[][] InitializeWeights(Random random, int rows, int cols, float scale)
    {
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

    private static float[] EncodeAction(Action action)
    {
        // Simple hash-based encoding - in practice would use learned embeddings
        var hash = action.Name.GetHashCode();
        var embedding = new float[10];
        for (int i = 0; i < embedding.Length; i++)
        {
            embedding[i] = (float)Math.Sin(hash * (i + 1) * 0.1);
        }

        return embedding;
    }

    private static float[] ConcatenateEmbeddings(float[] a, float[] b)
    {
        var result = new float[a.Length + b.Length];
        Array.Copy(a, 0, result, 0, a.Length);
        Array.Copy(b, 0, result, a.Length, b.Length);
        return result;
    }
}