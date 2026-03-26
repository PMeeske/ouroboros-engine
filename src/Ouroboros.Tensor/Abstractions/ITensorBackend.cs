// <copyright file="ITensorBackend.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Abstractions;

/// <summary>
/// Abstracts the compute backend for tensor creation and operations.
/// Pluggable implementations include CPU (<c>System.Numerics.Tensors</c>),
/// GPU (TorchSharp), and inference (ONNX Runtime).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Create"/> and <see cref="FromMemory"/> are data-construction helpers that throw on
/// programmer error (wrong sizes). Operations like <see cref="MatMul"/> and <see cref="Add"/>
/// return <see cref="Result{TSuccess,TError}"/> because shape validation failures are expected,
/// recoverable pipeline errors (R15).
/// </para>
/// <para>
/// All returned tensors must be disposed by the caller. Backend implementations should use pooled
/// memory to minimise allocations (R12, R17).
/// </para>
/// </remarks>
public interface ITensorBackend
{
    /// <summary>Gets the device type this backend targets.</summary>
    DeviceType Device { get; }

    /// <summary>
    /// Creates a tensor with the given shape, copying data from <paramref name="data"/>.
    /// </summary>
    /// <param name="shape">Shape of the new tensor.</param>
    /// <param name="data">Source data; length must equal <see cref="TensorShape.ElementCount"/>.</param>
    /// <returns>A new tensor backed by pooled memory.</returns>
    /// <exception cref="ArgumentException">Thrown when data length mismatches shape.</exception>
    ITensor<float> Create(TensorShape shape, ReadOnlySpan<float> data);

    /// <summary>
    /// Creates an uninitialised tensor of the given shape (contents undefined).
    /// Useful when the caller will fill the tensor immediately.
    /// </summary>
    ITensor<float> CreateUninitialized(TensorShape shape);

    /// <summary>
    /// Wraps externally-owned memory as a tensor without copying (best-effort zero-copy).
    /// The caller must ensure the memory outlives the returned tensor.
    /// </summary>
    /// <param name="memory">Source memory; length must equal <see cref="TensorShape.ElementCount"/>.</param>
    /// <param name="shape">Shape the memory represents.</param>
    ITensor<float> FromMemory(ReadOnlyMemory<float> memory, TensorShape shape);

    /// <summary>
    /// Multiplies two matrices (or batched matrices). Inner dimensions must match.
    /// </summary>
    /// <returns>
    /// <see cref="Result{TSuccess,TError}.Success"/> with the result tensor, or
    /// <see cref="Result{TSuccess,TError}.Failure"/> with an error message on shape mismatch.
    /// </returns>
    Result<ITensor<float>, string> MatMul(ITensor<float> a, ITensor<float> b);

    /// <summary>
    /// Adds two tensors element-wise. Shapes must be identical (no broadcasting).
    /// </summary>
    /// <returns>
    /// <see cref="Result{TSuccess,TError}.Success"/> with the result tensor, or
    /// <see cref="Result{TSuccess,TError}.Failure"/> with an error message on shape mismatch.
    /// </returns>
    Result<ITensor<float>, string> Add(ITensor<float> a, ITensor<float> b);
}
