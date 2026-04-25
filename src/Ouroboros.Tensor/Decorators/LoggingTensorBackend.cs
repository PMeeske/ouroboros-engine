// <copyright file="LoggingTensorBackend.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Decorators;

/// <summary>
/// Decorator that logs each tensor operation — name, input shapes, elapsed time, and outcome —
/// via <see cref="ILogger"/> (R11, R18). Does not alter the behaviour of the wrapped backend.
/// </summary>
public sealed class LoggingTensorBackend : ITensorBackend
{
    private readonly ITensorBackend _inner;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggingTensorBackend"/> class.
    /// Initializes a new <see cref="LoggingTensorBackend"/>.
    /// </summary>
    /// <param name="inner">The backend to wrap.</param>
    /// <param name="logger">Logger to write operation records to.</param>
    public LoggingTensorBackend(ITensorBackend inner, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(logger);
        _inner = inner;
        _logger = logger;
    }

    /// <inheritdoc/>
    public DeviceType Device => _inner.Device;

    /// <inheritdoc/>
    public ITensor<float> Create(TensorShape shape, ReadOnlySpan<float> data)
    {
        _logger.LogDebug("Create: shape={Shape}, elements={Count}", shape, data.Length);
        return _inner.Create(shape, data);
    }

    /// <inheritdoc/>
    public ITensor<float> CreateUninitialized(TensorShape shape)
    {
        _logger.LogDebug("CreateUninitialized: shape={Shape}", shape);
        return _inner.CreateUninitialized(shape);
    }

    /// <inheritdoc/>
    public ITensor<float> FromMemory(ReadOnlyMemory<float> memory, TensorShape shape)
    {
        _logger.LogDebug("FromMemory: shape={Shape}, memoryLength={Length}", shape, memory.Length);
        return _inner.FromMemory(memory, shape);
    }

    /// <inheritdoc/>
    public Result<ITensor<float>, string> MatMul(ITensor<float> a, ITensor<float> b)
    {
        _logger.LogDebug("MatMul: {ShapeA} × {ShapeB}", a.Shape, b.Shape);
        var sw = Stopwatch.StartNew();
        var result = _inner.MatMul(a, b);
        sw.Stop();

        if (result.IsSuccess)
        {
            _logger.LogDebug(
                "MatMul completed in {ElapsedMs}ms → {Shape}",
                sw.ElapsedMilliseconds, result.Value.Shape);
        }
        else
        {
            _logger.LogWarning(
                "MatMul failed in {ElapsedMs}ms: {Error}",
                sw.ElapsedMilliseconds, result.Error);
        }

        return result;
    }

    /// <inheritdoc/>
    public Result<ITensor<float>, string> Add(ITensor<float> a, ITensor<float> b)
    {
        _logger.LogDebug("Add: {ShapeA} + {ShapeB}", a.Shape, b.Shape);
        var sw = Stopwatch.StartNew();
        var result = _inner.Add(a, b);
        sw.Stop();

        if (result.IsSuccess)
        {
            _logger.LogDebug(
                "Add completed in {ElapsedMs}ms → {Shape}",
                sw.ElapsedMilliseconds, result.Value.Shape);
        }
        else
        {
            _logger.LogWarning(
                "Add failed in {ElapsedMs}ms: {Error}",
                sw.ElapsedMilliseconds, result.Error);
        }

        return result;
    }
}
