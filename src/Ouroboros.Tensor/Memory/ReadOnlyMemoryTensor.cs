// <copyright file="ReadOnlyMemoryTensor.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Memory;

/// <summary>
/// A zero-copy <see cref="ITensor{T}"/> that wraps externally-owned <see cref="ReadOnlyMemory{T}"/>.
/// The caller retains ownership of the underlying memory; disposing this tensor is a no-op.
/// </summary>
/// <typeparam name="T">Unmanaged element type.</typeparam>
/// <remarks>
/// Created by <see cref="ITensorBackend.FromMemory"/> to avoid a redundant copy when the caller
/// already holds the data in a compatible format (R09, R12).
/// </remarks>
internal sealed class ReadOnlyMemoryTensor<T> : ITensor<T> where T : unmanaged
{
    private readonly ReadOnlyMemory<T> _memory;

    internal ReadOnlyMemoryTensor(ReadOnlyMemory<T> memory, TensorShape shape)
    {
        if (memory.Length != (int)shape.ElementCount)
            throw new ArgumentException(
                $"Memory length {memory.Length} does not match shape element count {shape.ElementCount}.",
                nameof(memory));

        _memory = memory;
        Shape = shape;
    }

    /// <inheritdoc/>
    public TensorShape Shape { get; }

    /// <inheritdoc/>
    public DeviceType Device => DeviceType.Cpu;

    /// <inheritdoc/>
    public ReadOnlyMemory<T> AsMemory() => _memory;

    /// <inheritdoc/>
    public ReadOnlySpan<T> AsSpan() => _memory.Span;

    /// <inheritdoc/>
    public ITensor<T> ToCpu() => this;

    /// <inheritdoc/>
    public ITensor<T> ToGpu()
        => throw new NotSupportedException(
            "GPU tensors must be created via a GPU-capable ITensorBackend.");

    /// <inheritdoc/>
    /// <remarks>No-op: memory is owned by the caller.</remarks>
    public void Dispose() { }
}
