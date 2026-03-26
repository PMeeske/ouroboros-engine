// <copyright file="ITensor.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Abstractions;

/// <summary>
/// A backend-agnostic tensor abstraction. Does not assume CPU or GPU residency and exposes no
/// implementation-specific details.
/// </summary>
/// <typeparam name="T">The unmanaged element type (typically <see langword="float"/>).</typeparam>
/// <remarks>
/// <para>
/// <see cref="AsMemory"/> and <see cref="AsSpan"/> provide best-effort CPU read access.
/// They will throw <see cref="NotSupportedException"/> when the tensor is GPU-resident.
/// Use <see cref="ToCpu"/> first to transfer data before reading.
/// </para>
/// <para>
/// Device transfers (<see cref="ToCpu"/>, <see cref="ToGpu"/>) are explicit — no hidden
/// conversions occur. Decorators can intercept these calls for metrics and logging (R03, R18).
/// </para>
/// </remarks>
public interface ITensor<T> : IDisposable where T : unmanaged
{
    /// <summary>Gets the shape (rank and dimension extents) of this tensor.</summary>
    TensorShape Shape { get; }

    /// <summary>Gets the device on which this tensor is resident.</summary>
    DeviceType Device { get; }

    /// <summary>
    /// Returns a read-only view of the tensor's data as a <see cref="ReadOnlyMemory{T}"/>.
    /// </summary>
    /// <exception cref="NotSupportedException">Thrown when the tensor is GPU-resident.</exception>
    /// <exception cref="ObjectDisposedException">Thrown after the tensor has been disposed.</exception>
    ReadOnlyMemory<T> AsMemory();

    /// <summary>
    /// Returns a read-only span over the tensor's data (stack-safe, synchronous).
    /// </summary>
    /// <exception cref="NotSupportedException">Thrown when the tensor is GPU-resident.</exception>
    /// <exception cref="ObjectDisposedException">Thrown after the tensor has been disposed.</exception>
    ReadOnlySpan<T> AsSpan();

    /// <summary>
    /// Returns a CPU-resident copy of this tensor. If already on CPU, returns
    /// <see langword="this"/> without allocation.
    /// </summary>
    ITensor<T> ToCpu();

    /// <summary>
    /// Returns a GPU-resident copy of this tensor. Requires a GPU-capable backend to be configured.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// Thrown when no GPU backend is available. The caller must use a GPU-capable
    /// <see cref="ITensorBackend"/> to create GPU tensors.
    /// </exception>
    ITensor<T> ToGpu();
}
