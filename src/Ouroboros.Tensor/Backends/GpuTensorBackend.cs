// <copyright file="GpuTensorBackend.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Backends;

/// <summary>
/// Stub GPU backend intended to be replaced with a TorchSharp implementation
/// when GPU support is enabled (add the <c>TorchSharp</c> NuGet package and set
/// <c>&lt;EnableGpu&gt;true&lt;/EnableGpu&gt;</c> in the project file).
/// All operations currently return a failure result indicating GPU is unavailable (R02, R16).
/// </summary>
/// <remarks>
/// This stub preserves the extension point so that GPU support can be added without changing any
/// core abstractions or callers (R16). The <see cref="DefaultBackendSelector"/> returns a
/// <see cref="CpuTensorBackend"/> when GPU is unavailable.
/// </remarks>
public sealed class GpuTensorBackend : ITensorBackend
{
    private const string GpuUnavailableMessage =
        "GPU backend is not available. Add the TorchSharp NuGet package and set " +
        "<EnableGpu>true</EnableGpu> in Ouroboros.Tensor.csproj to enable GPU support.";

    /// <inheritdoc/>
    public DeviceType Device => DeviceType.Cuda;

    /// <inheritdoc/>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public ITensor<float> Create(TensorShape shape, ReadOnlySpan<float> data)
        => throw new NotSupportedException(GpuUnavailableMessage);

    /// <inheritdoc/>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public ITensor<float> CreateUninitialized(TensorShape shape)
        => throw new NotSupportedException(GpuUnavailableMessage);

    /// <inheritdoc/>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public ITensor<float> FromMemory(ReadOnlyMemory<float> memory, TensorShape shape)
        => throw new NotSupportedException(GpuUnavailableMessage);

    /// <inheritdoc/>
    public Result<ITensor<float>, string> MatMul(ITensor<float> a, ITensor<float> b)
        => Result<ITensor<float>, string>.Failure(GpuUnavailableMessage);

    /// <inheritdoc/>
    public Result<ITensor<float>, string> Add(ITensor<float> a, ITensor<float> b)
        => Result<ITensor<float>, string>.Failure(GpuUnavailableMessage);
}
