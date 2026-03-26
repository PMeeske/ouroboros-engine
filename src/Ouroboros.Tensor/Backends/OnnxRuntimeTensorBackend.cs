// <copyright file="OnnxRuntimeTensorBackend.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Runtime.InteropServices;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Ouroboros.Tensor.Backends;

/// <summary>
/// An <see cref="ITensorBackend"/> adapter for running ONNX models via
/// <c>Microsoft.ML.OnnxRuntime</c>. Intended for inference scenarios where a pre-trained
/// ONNX model is the execution unit rather than generic tensor arithmetic (R02, R13).
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="CpuTensorBackend"/>, this backend is primarily a data conversion layer:
/// <see cref="Create"/> and <see cref="FromMemory"/> produce tensors compatible with ONNX
/// Runtime session inputs, while <see cref="RunInference"/> executes a model and returns
/// the outputs as <see cref="ITensor{T}"/> instances.
/// </para>
/// <para>
/// <see cref="MatMul"/> and <see cref="Add"/> delegate to <see cref="CpuTensorBackend"/>
/// for general-purpose arithmetic so that pre/post-processing steps can share the same
/// backend interface. Callers that need GPU-accelerated ONNX inference should use
/// <c>ExecutionProviders</c> (CUDA EP) via <see cref="SessionOptions"/>.
/// </para>
/// </remarks>
public sealed class OnnxRuntimeTensorBackend : ITensorBackend, IDisposable
{
    private readonly CpuTensorBackend _cpuFallback = CpuTensorBackend.Instance;

    /// <summary>
    /// Initializes a new <see cref="OnnxRuntimeTensorBackend"/> with optional session options.
    /// Pass a configured <see cref="SessionOptions"/> to enable GPU execution providers.
    /// </summary>
    public OnnxRuntimeTensorBackend() { }

    /// <inheritdoc/>
    /// <remarks>Returns <see cref="Abstractions.DeviceType.Cpu"/> for the CPU ONNX EP.</remarks>
    public DeviceType Device => DeviceType.Cpu;

    /// <inheritdoc/>
    public ITensor<float> Create(TensorShape shape, ReadOnlySpan<float> data)
        => _cpuFallback.Create(shape, data);

    /// <inheritdoc/>
    public ITensor<float> CreateUninitialized(TensorShape shape)
        => _cpuFallback.CreateUninitialized(shape);

    /// <inheritdoc/>
    public ITensor<float> FromMemory(ReadOnlyMemory<float> memory, TensorShape shape)
        => _cpuFallback.FromMemory(memory, shape);

    /// <inheritdoc/>
    public Result<ITensor<float>, string> MatMul(ITensor<float> a, ITensor<float> b)
        => _cpuFallback.MatMul(a, b);

    /// <inheritdoc/>
    public Result<ITensor<float>, string> Add(ITensor<float> a, ITensor<float> b)
        => _cpuFallback.Add(a, b);

    /// <summary>
    /// Runs an ONNX model session against a set of named input tensors and returns the
    /// named output tensors. This is the primary API for inference use cases (R13).
    /// </summary>
    /// <param name="session">The ONNX inference session.</param>
    /// <param name="inputs">
    /// Named input map. Keys must match the session's input names.
    /// </param>
    /// <param name="outputNames">
    /// Names of the outputs to collect. When null, all outputs are returned.
    /// </param>
    /// <returns>
    /// A dictionary mapping output name to tensor on success,
    /// or a failure with an error description.
    /// </returns>
    public Result<IReadOnlyDictionary<string, ITensor<float>>, string> RunInference(
        InferenceSession session,
        IReadOnlyDictionary<string, ITensor<float>> inputs,
        IEnumerable<string>? outputNames = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(inputs);

        try
        {
            // Convert ITensor<float> → OnnxValue
            var onnxInputs = new List<NamedOnnxValue>(inputs.Count);
            foreach (var (name, tensor) in inputs)
            {
                var dims = tensor.Shape.Dimensions.Select(d => (long)d).ToArray();
                var ortTensor = new DenseTensor<float>(
                    MemoryMarshal.AsMemory(tensor.AsMemory()),
                    dims.Select(d => (int)d).ToArray());
                onnxInputs.Add(NamedOnnxValue.CreateFromTensor(name, ortTensor));
            }

            var requestedOutputs = outputNames?.ToList();
            using var results = requestedOutputs is null
                ? session.Run(onnxInputs)
                : session.Run(onnxInputs, requestedOutputs);

            var outputs = new Dictionary<string, ITensor<float>>(results.Count);
            foreach (var output in results)
            {
                if (output.Value is DenseTensor<float> denseTensor)
                {
                    var shape = TensorShape.Of(denseTensor.Dimensions.ToArray());
                    var data = denseTensor.Buffer.ToArray();
                    outputs[output.Name] = _cpuFallback.Create(shape, data.AsSpan());
                }
            }

            return Result<IReadOnlyDictionary<string, ITensor<float>>, string>.Success(outputs);
        }
        catch (OnnxRuntimeException ex)
        {
            return Result<IReadOnlyDictionary<string, ITensor<float>>, string>.Failure(
                $"ONNX inference failed: {ex.Message}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<IReadOnlyDictionary<string, ITensor<float>>, string>.Failure(
                $"Unexpected error during ONNX inference: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public void Dispose() { }
}
