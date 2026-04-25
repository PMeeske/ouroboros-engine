// <copyright file="CpuTensorBackend.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;

namespace Ouroboros.Tensor.Backends;

/// <summary>
/// CPU-resident tensor backend backed by <c>System.Numerics.Tensors.TensorPrimitives</c>
/// (part of the .NET 10 BCL — no extra NuGet dependency).
/// Uses pooled memory via <see cref="TensorMemoryPool"/> to minimise GC pressure (R02, R12).
/// </summary>
public sealed class CpuTensorBackend : ITensorBackend
{
    /// <summary>Singleton instance. Stateless and thread-safe (R19).</summary>
    public static readonly CpuTensorBackend Instance = new();

    /// <inheritdoc/>
    public DeviceType Device => DeviceType.Cpu;

    /// <inheritdoc/>
    public ITensor<float> Create(TensorShape shape, ReadOnlySpan<float> data)
        => TensorMemoryPool.RentAndFill(shape, data);

    /// <inheritdoc/>
    public ITensor<float> CreateUninitialized(TensorShape shape)
        => TensorMemoryPool.Rent<float>(shape);

    /// <inheritdoc/>
    /// <remarks>
    /// Zero-copy when the source is already a <see cref="ReadOnlyMemory{T}"/> over a managed array.
    /// </remarks>
    public ITensor<float> FromMemory(ReadOnlyMemory<float> memory, TensorShape shape)
        => new ReadOnlyMemoryTensor<float>(memory, shape);

    /// <inheritdoc/>
    /// <remarks>
    /// Performs naive O(n³) multiplication for correctness.
    /// High-throughput callers should consider batching or using a GPU backend (R02, R17).
    /// </remarks>
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Ownership transferred to returning Result.")]
    public Result<ITensor<float>, string> MatMul(ITensor<float> a, ITensor<float> b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        if (a.Shape.Rank < 2 || b.Shape.Rank < 2)
        {
            return Result<ITensor<float>, string>.Failure(
                $"MatMul requires at least rank-2 tensors, got {a.Shape} and {b.Shape}.");
        }

        var aRows = a.Shape.Dimensions[^2];
        var aCols = a.Shape.Dimensions[^1];
        var bRows = b.Shape.Dimensions[^2];
        var bCols = b.Shape.Dimensions[^1];

        if (aCols != bRows)
        {
            return Result<ITensor<float>, string>.Failure(
                $"MatMul shape mismatch: [{aRows}×{aCols}] × [{bRows}×{bCols}] — " +
                $"inner dimensions must match.");
        }

        var resultShape = TensorShape.Of(aRows, bCols);
        var result = TensorMemoryPool.Rent<float>(resultShape);
        try
        {
            var resultSpan = result.WritableMemory.Span;
            var aSpan = a.AsSpan();
            var bSpan = b.AsSpan();

            for (var i = 0; i < aRows; i++)
            {
                for (var j = 0; j < bCols; j++)
                {
                    var rowSlice = aSpan.Slice(i * aCols, aCols);
                    float dot = 0f;
                    for (var k = 0; k < aCols; k++)
                    {
                        dot += rowSlice[k] * bSpan[(k * bCols) + j];
                    }

                    resultSpan[(i * bCols) + j] = dot;
                }
            }

            return Result<ITensor<float>, string>.Success(result);
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Ownership transferred to returning Result.")]
    public Result<ITensor<float>, string> Add(ITensor<float> a, ITensor<float> b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        if (!a.Shape.IsCompatibleWith(b.Shape))
        {
            return Result<ITensor<float>, string>.Failure(
                $"Add shape mismatch: {a.Shape} vs {b.Shape}.");
        }

        var result = TensorMemoryPool.Rent<float>(a.Shape);
        try
        {
            TensorPrimitives.Add(a.AsSpan(), b.AsSpan(), result.WritableMemory.Span);
            return Result<ITensor<float>, string>.Success(result);
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }
}
