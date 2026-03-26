// <copyright file="OnnxTensorBackend.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Backends;

/// <summary>
/// Stub ONNX Runtime backend for model inference scenarios (R02).
/// Intended to be implemented when <c>Microsoft.ML.OnnxRuntime</c> is added as a dependency.
/// This stub preserves the extension point and allows <see cref="ITensorBackend"/>-typed
/// references to compile without the optional inference package (R16).
/// </summary>
/// <remarks>
/// ONNX Runtime is better suited as an inference execution layer rather than a general
/// tensor arithmetic backend. In practice, pipeline steps would accept ONNX model paths and
/// produce output tensors, keeping the data/execution boundary explicit (R13).
/// </remarks>
public sealed class OnnxTensorBackend : ITensorBackend
{
    private const string OnnxUnavailableMessage =
        "ONNX Runtime backend is not yet implemented. Add the Microsoft.ML.OnnxRuntime " +
        "NuGet package and implement this class to enable ONNX inference support.";

    /// <inheritdoc/>
    public DeviceType Device => DeviceType.Cpu;

    /// <inheritdoc/>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public ITensor<float> Create(TensorShape shape, ReadOnlySpan<float> data)
        => throw new NotSupportedException(OnnxUnavailableMessage);

    /// <inheritdoc/>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public ITensor<float> CreateUninitialized(TensorShape shape)
        => throw new NotSupportedException(OnnxUnavailableMessage);

    /// <inheritdoc/>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public ITensor<float> FromMemory(ReadOnlyMemory<float> memory, TensorShape shape)
        => throw new NotSupportedException(OnnxUnavailableMessage);

    /// <inheritdoc/>
    public Result<ITensor<float>, string> MatMul(ITensor<float> a, ITensor<float> b)
        => Result<ITensor<float>, string>.Failure(OnnxUnavailableMessage);

    /// <inheritdoc/>
    public Result<ITensor<float>, string> Add(ITensor<float> a, ITensor<float> b)
        => Result<ITensor<float>, string>.Failure(OnnxUnavailableMessage);
}
