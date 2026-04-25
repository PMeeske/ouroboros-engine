// <copyright file="PooledTensor.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Memory;

/// <summary>
/// An <see cref="ITensor{T}"/> backed by a buffer rented from <see cref="ArrayPool{T}"/>.
/// Returning the buffer to the pool on <see cref="Dispose"/> minimises GC pressure (R12).
/// </summary>
/// <typeparam name="T">Unmanaged element type.</typeparam>
/// <remarks>
/// Instances are created exclusively by <see cref="TensorMemoryPool"/>. Callers must dispose
/// this tensor when done to return the underlying buffer to the pool.
/// </remarks>
public sealed class PooledTensor<T> : ITensor<T>
    where T : unmanaged
{
    private readonly T[] _buffer;
    private readonly int _length;
    private volatile bool _disposed;

    internal PooledTensor(T[] buffer, int length, TensorShape shape)
    {
        _buffer = buffer;
        _length = length;
        Shape = shape;
    }

    /// <inheritdoc/>
    public TensorShape Shape { get; }

    /// <inheritdoc/>
    public DeviceType Device => DeviceType.Cpu;

    /// <summary>
    /// Gets exposes the underlying writable memory segment (for use by backend implementations only).
    /// </summary>
    internal Memory<T> WritableMemory
    {
        get
        {
            ThrowIfDisposed();
            return _buffer.AsMemory(0, _length);
        }
    }

    /// <inheritdoc/>
    public ReadOnlyMemory<T> AsMemory()
    {
        ThrowIfDisposed();
        return _buffer.AsMemory(0, _length);
    }

    /// <inheritdoc/>
    public ReadOnlySpan<T> AsSpan()
    {
        ThrowIfDisposed();
        return _buffer.AsSpan(0, _length);
    }

    /// <inheritdoc/>
    /// <remarks>Already on CPU — returns <see langword="this"/> without allocation.</remarks>
    public ITensor<T> ToCpu() => this;

    /// <inheritdoc/>
    /// <exception cref="NotSupportedException">
    /// Always thrown. GPU tensors must be created via a GPU-capable <see cref="ITensorBackend"/>.
    /// </exception>
    public ITensor<T> ToGpu()
        => throw new NotSupportedException(
            "GPU tensors must be created via a GPU-capable ITensorBackend (e.g. GpuTensorBackend). " +
            "Call ITensorBackendSelector.SelectBackend(DeviceType.Cuda) to obtain one.");

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            ArrayPool<T>.Shared.Return(_buffer, clearArray: false);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(
                nameof(PooledTensor<T>),
                "Cannot access a tensor that has been disposed and its buffer returned to the pool.");
        }
    }
}
