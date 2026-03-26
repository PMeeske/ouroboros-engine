// <copyright file="TorchSharpGpuTensorBackend.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

#if ENABLE_GPU

using TorchSharp;
using static TorchSharp.torch;

namespace Ouroboros.Tensor.Backends;

/// <summary>
/// Full TorchSharp-backed GPU tensor backend. Compiled only when the
/// <c>EnableGpu</c> MSBuild property is set to <c>true</c> and the
/// <c>TorchSharp</c> NuGet package is present (R02, R03).
/// </summary>
/// <remarks>
/// <para>
/// Uses <c>torch.cuda.is_available()</c> at construction time to verify GPU availability.
/// Throws <see cref="NotSupportedException"/> if CUDA is not present so the
/// <see cref="DefaultBackendSelector"/> can fall back to CPU gracefully (R14).
/// </para>
/// <para>
/// All tensor data crossing the CPU/GPU boundary must be explicitly transferred via
/// <see cref="TorchTensor{T}.ToCpu"/> or <see cref="TorchTensor{T}.ToGpu"/>. No silent
/// round-trips occur (R03, R13).
/// </para>
/// </remarks>
public sealed class TorchSharpGpuTensorBackend : ITensorBackend
{
    private readonly Device _device;

    /// <summary>
    /// Initializes a new <see cref="TorchSharpGpuTensorBackend"/> targeting the default CUDA device.
    /// </summary>
    /// <exception cref="NotSupportedException">Thrown when CUDA is unavailable.</exception>
    public TorchSharpGpuTensorBackend() : this(deviceIndex: 0) { }

    /// <summary>
    /// Initializes a new <see cref="TorchSharpGpuTensorBackend"/> targeting a specific CUDA device.
    /// </summary>
    /// <param name="deviceIndex">CUDA device ordinal (0 = first GPU).</param>
    public TorchSharpGpuTensorBackend(int deviceIndex)
    {
        if (!torch.cuda.is_available())
            throw new NotSupportedException(
                "CUDA is not available on this system. " +
                "Ensure the NVIDIA CUDA toolkit is installed and a compatible GPU is present.");

        _device = new Device(DeviceType.CUDA, deviceIndex);
    }

    /// <inheritdoc/>
    public global::Ouroboros.Tensor.Abstractions.DeviceType Device =>
        global::Ouroboros.Tensor.Abstractions.DeviceType.Cuda;

    /// <inheritdoc/>
    public ITensor<float> Create(TensorShape shape, ReadOnlySpan<float> data)
    {
        var torchShape = ToTorchShape(shape);
        // Copy host data to a CPU tensor, then transfer to GPU
        unsafe
        {
            fixed (float* ptr = data)
            {
                var cpuTensor = torch.from_blob(
                    (IntPtr)ptr,
                    torchShape,
                    dtype: torch.float32,
                    device: torch.CPU);
                var gpuTensor = cpuTensor.to(_device);
                return new TorchTensor(gpuTensor, shape);
            }
        }
    }

    /// <inheritdoc/>
    public ITensor<float> CreateUninitialized(TensorShape shape)
    {
        var t = torch.empty(ToTorchShape(shape), dtype: torch.float32, device: _device);
        return new TorchTensor(t, shape);
    }

    /// <inheritdoc/>
    public ITensor<float> FromMemory(ReadOnlyMemory<float> memory, TensorShape shape)
    {
        // Must copy because GPU cannot reference managed heap memory directly
        using var handle = memory.Pin();
        unsafe
        {
            var cpuTensor = torch.from_blob(
                (IntPtr)handle.Pointer,
                ToTorchShape(shape),
                dtype: torch.float32,
                device: torch.CPU);
            var gpuTensor = cpuTensor.to(_device);
            return new TorchTensor(gpuTensor, shape);
        }
    }

    /// <inheritdoc/>
    public Result<ITensor<float>, string> MatMul(ITensor<float> a, ITensor<float> b)
    {
        if (a is not TorchTensor ta || b is not TorchTensor tb)
            return Result<ITensor<float>, string>.Failure(
                "MatMul requires both operands to be TorchTensor instances on the GPU.");

        if (a.Shape.Rank < 2 || b.Shape.Rank < 2)
            return Result<ITensor<float>, string>.Failure(
                $"MatMul requires at least rank-2 tensors. Got {a.Shape} and {b.Shape}.");

        var aCols = a.Shape.Dimensions[^1];
        var bRows = b.Shape.Dimensions[^2];
        if (aCols != bRows)
            return Result<ITensor<float>, string>.Failure(
                $"MatMul shape mismatch: {a.Shape} × {b.Shape}.");

        try
        {
            var result = torch.mm(ta.Inner, tb.Inner);
            var resultShape = TensorShape.Of(a.Shape.Dimensions[^2], b.Shape.Dimensions[^1]);
            return Result<ITensor<float>, string>.Success(new TorchTensor(result, resultShape));
        }
        catch (Exception ex)
        {
            return Result<ITensor<float>, string>.Failure($"MatMul failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public Result<ITensor<float>, string> Add(ITensor<float> a, ITensor<float> b)
    {
        if (a is not TorchTensor ta || b is not TorchTensor tb)
            return Result<ITensor<float>, string>.Failure(
                "Add requires both operands to be TorchTensor instances on the GPU.");

        if (!a.Shape.IsCompatibleWith(b.Shape))
            return Result<ITensor<float>, string>.Failure(
                $"Add shape mismatch: {a.Shape} vs {b.Shape}.");

        try
        {
            var result = ta.Inner.add(tb.Inner);
            return Result<ITensor<float>, string>.Success(new TorchTensor(result, a.Shape));
        }
        catch (Exception ex)
        {
            return Result<ITensor<float>, string>.Failure($"Add failed: {ex.Message}");
        }
    }

    private static long[] ToTorchShape(TensorShape shape)
        => shape.Dimensions.Select(d => (long)d).ToArray();

    // ── Inner TorchTensor wrapper ─────────────────────────────────────────────

    /// <summary>
    /// Wraps a <c>torch.Tensor</c> as an <see cref="ITensor{T}"/>. GPU-resident data cannot
    /// be accessed via <see cref="AsSpan"/>; call <see cref="ToCpu"/> first.
    /// </summary>
    private sealed class TorchTensor : ITensor<float>
    {
        internal Tensor Inner { get; }

        internal TorchTensor(Tensor inner, TensorShape shape)
        {
            Inner = inner;
            Shape = shape;
        }

        public TensorShape Shape { get; }

        public global::Ouroboros.Tensor.Abstractions.DeviceType Device =>
            Inner.device.type == DeviceType.CUDA
                ? global::Ouroboros.Tensor.Abstractions.DeviceType.Cuda
                : global::Ouroboros.Tensor.Abstractions.DeviceType.Cpu;

        public ReadOnlyMemory<float> AsMemory()
        {
            if (Inner.device.type == DeviceType.CUDA)
                throw new NotSupportedException(
                    "Cannot access GPU tensor data directly. Call ToCpu() first.");

            var data = Inner.data<float>().ToArray();
            return data.AsMemory();
        }

        public ReadOnlySpan<float> AsSpan()
        {
            if (Inner.device.type == DeviceType.CUDA)
                throw new NotSupportedException(
                    "Cannot access GPU tensor data directly. Call ToCpu() first.");

            return Inner.data<float>().ToArray().AsSpan();
        }

        public ITensor<float> ToCpu()
        {
            if (Inner.device.type != DeviceType.CUDA)
                return this;

            // Transfer to CPU and wrap in a PooledTensor for uniform handling
            var cpuTensor = Inner.cpu();
            var data = cpuTensor.data<float>().ToArray();
            return TensorMemoryPool.RentAndFill(Shape, data.AsSpan());
        }

        public ITensor<float> ToGpu()
        {
            if (Inner.device.type == DeviceType.CUDA)
                return this;

            var gpuTensor = Inner.cuda();
            return new TorchTensor(gpuTensor, Shape);
        }

        public void Dispose()
        {
            Inner.Dispose();
        }
    }
}

#endif // ENABLE_GPU
