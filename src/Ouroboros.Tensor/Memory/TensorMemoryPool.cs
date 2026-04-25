// <copyright file="TensorMemoryPool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Memory;

/// <summary>
/// Factory for renting pooled tensor buffers via <see cref="ArrayPool{T}.Shared"/>.
/// All rented buffers are returned to the pool when the tensor is disposed (R12).
/// </summary>
public static class TensorMemoryPool
{
    /// <summary>
    /// Rents a <see cref="PooledTensor{T}"/> large enough to hold all elements in
    /// <paramref name="shape"/>. Buffer contents are uninitialised.
    /// </summary>
    /// <typeparam name="T">Unmanaged element type.</typeparam>
    /// <param name="shape">The shape of the tensor to allocate.</param>
    /// <returns>A new <see cref="PooledTensor{T}"/>; must be disposed by the caller.</returns>
    public static PooledTensor<T> Rent<T>(TensorShape shape)
        where T : unmanaged
    {
        var count = (int)shape.ElementCount;
        var buffer = ArrayPool<T>.Shared.Rent(count);
        return new PooledTensor<T>(buffer, count, shape);
    }

    /// <summary>
    /// Rents a <see cref="PooledTensor{T}"/> and fills it with <paramref name="data"/>.
    /// </summary>
    /// <typeparam name="T">Unmanaged element type.</typeparam>
    /// <param name="shape">The shape of the tensor to allocate.</param>
    /// <param name="data">
    /// Source data to copy; length must equal <see cref="TensorShape.ElementCount"/>.
    /// </param>
    /// <exception cref="ArgumentException">Thrown when data length mismatches the shape.</exception>
    /// <returns></returns>
    public static PooledTensor<T> RentAndFill<T>(TensorShape shape, ReadOnlySpan<T> data)
        where T : unmanaged
    {
        var count = (int)shape.ElementCount;
        if (data.Length != count)
        {
            throw new ArgumentException(
                $"Data length {data.Length} does not match shape element count {count}.",
                nameof(data));
        }

        var tensor = Rent<T>(shape);
        data.CopyTo(tensor.WritableMemory.Span);
        return tensor;
    }
}
