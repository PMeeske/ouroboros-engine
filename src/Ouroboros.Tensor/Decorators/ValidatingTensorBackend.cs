// <copyright file="ValidatingTensorBackend.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Decorators;

/// <summary>
/// Decorator that validates tensor shapes and device compatibility before delegating to the inner
/// backend. Returns <see cref="Result{TSuccess,TError}.Failure"/> for invalid inputs rather than
/// propagating exceptions from the inner backend (R11, R15).
/// </summary>
public sealed class ValidatingTensorBackend : ITensorBackend
{
    private readonly ITensorBackend _inner;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidatingTensorBackend"/> class.
    /// Initializes a new <see cref="ValidatingTensorBackend"/> wrapping <paramref name="inner"/>.
    /// </summary>
    public ValidatingTensorBackend(ITensorBackend inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    /// <inheritdoc/>
    public DeviceType Device => _inner.Device;

    /// <inheritdoc/>
    public ITensor<float> Create(TensorShape shape, ReadOnlySpan<float> data)
    {
        var expected = (int)shape.ElementCount;
        if (data.Length != expected)
        {
            throw new ArgumentException(
                $"Data length {data.Length} does not match shape {shape} (expected {expected}).",
                nameof(data));
        }

        return _inner.Create(shape, data);
    }

    /// <inheritdoc/>
    public ITensor<float> CreateUninitialized(TensorShape shape) => _inner.CreateUninitialized(shape);

    /// <inheritdoc/>
    public ITensor<float> FromMemory(ReadOnlyMemory<float> memory, TensorShape shape)
    {
        var expected = (int)shape.ElementCount;
        if (memory.Length != expected)
        {
            throw new ArgumentException(
                $"Memory length {memory.Length} does not match shape {shape} (expected {expected}).",
                nameof(memory));
        }

        return _inner.FromMemory(memory, shape);
    }

    /// <inheritdoc/>
    public Result<ITensor<float>, string> MatMul(ITensor<float> a, ITensor<float> b)
    {
        var deviceCheck = CheckDevices(a, b);
        if (deviceCheck is not null)
        {
            return Result<ITensor<float>, string>.Failure(deviceCheck);
        }

        if (a.Shape.Rank < 2 || b.Shape.Rank < 2)
        {
            return Result<ITensor<float>, string>.Failure(
                $"MatMul requires at least rank-2 tensors. Got {a.Shape} and {b.Shape}.");
        }

        var aCols = a.Shape.Dimensions[^1];
        var bRows = b.Shape.Dimensions[^2];
        if (aCols != bRows)
        {
            return Result<ITensor<float>, string>.Failure(
                $"MatMul inner dimension mismatch: {a.Shape} × {b.Shape}. " +
                $"Column count of A ({aCols}) must equal row count of B ({bRows}).");
        }

        return _inner.MatMul(a, b);
    }

    /// <inheritdoc/>
    public Result<ITensor<float>, string> Add(ITensor<float> a, ITensor<float> b)
    {
        var deviceCheck = CheckDevices(a, b);
        if (deviceCheck is not null)
        {
            return Result<ITensor<float>, string>.Failure(deviceCheck);
        }

        if (!a.Shape.IsCompatibleWith(b.Shape))
        {
            return Result<ITensor<float>, string>.Failure(
                $"Add shape mismatch: {a.Shape} vs {b.Shape}.");
        }

        return _inner.Add(a, b);
    }

    private string? CheckDevices(ITensor<float> a, ITensor<float> b)
    {
        if (a.Device != _inner.Device)
        {
            return $"Tensor A is on {a.Device} but this backend targets {_inner.Device}.";
        }

        if (b.Device != _inner.Device)
        {
            return $"Tensor B is on {b.Device} but this backend targets {_inner.Device}.";
        }

        return null;
    }
}
