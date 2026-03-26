// <copyright file="MetricsTensorBackend.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Decorators;

/// <summary>
/// Decorator that records latency and operation count metrics for each backend call via
/// <see cref="System.Diagnostics.Metrics"/> (R11, R18).
/// </summary>
/// <remarks>
/// Metrics are published to the <c>Ouroboros.Tensor</c> meter and can be collected by any
/// compatible consumer (OpenTelemetry, Prometheus exporter, dotnet-counters, etc.).
/// </remarks>
public sealed class MetricsTensorBackend : ITensorBackend
{
    /// <summary>The meter name used by this decorator.</summary>
    public const string MeterName = "Ouroboros.Tensor";

    private readonly ITensorBackend _inner;
    private readonly Histogram<double> _matMulLatency;
    private readonly Histogram<double> _addLatency;
    private readonly Counter<long> _matMulCount;
    private readonly Counter<long> _addCount;
    private readonly Counter<long> _matMulErrors;
    private readonly Counter<long> _addErrors;

    /// <summary>
    /// Initializes a new <see cref="MetricsTensorBackend"/> using an optionally shared
    /// <see cref="Meter"/>. If <paramref name="meter"/> is null, a default meter is created.
    /// </summary>
    public MetricsTensorBackend(ITensorBackend inner, Meter? meter = null)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;

        using var m = meter ?? new Meter(MeterName);
        _matMulLatency = m.CreateHistogram<double>("tensor.matmul.duration_ms", "ms",
            "MatMul operation latency in milliseconds");
        _addLatency = m.CreateHistogram<double>("tensor.add.duration_ms", "ms",
            "Add operation latency in milliseconds");
        _matMulCount = m.CreateCounter<long>("tensor.matmul.count",
            description: "Total MatMul operations attempted");
        _addCount = m.CreateCounter<long>("tensor.add.count",
            description: "Total Add operations attempted");
        _matMulErrors = m.CreateCounter<long>("tensor.matmul.errors",
            description: "Failed MatMul operations");
        _addErrors = m.CreateCounter<long>("tensor.add.errors",
            description: "Failed Add operations");
    }

    /// <inheritdoc/>
    public DeviceType Device => _inner.Device;

    /// <inheritdoc/>
    public ITensor<float> Create(TensorShape shape, ReadOnlySpan<float> data)
        => _inner.Create(shape, data);

    /// <inheritdoc/>
    public ITensor<float> CreateUninitialized(TensorShape shape)
        => _inner.CreateUninitialized(shape);

    /// <inheritdoc/>
    public ITensor<float> FromMemory(ReadOnlyMemory<float> memory, TensorShape shape)
        => _inner.FromMemory(memory, shape);

    /// <inheritdoc/>
    public Result<ITensor<float>, string> MatMul(ITensor<float> a, ITensor<float> b)
    {
        _matMulCount.Add(1);
        var sw = Stopwatch.StartNew();
        var result = _inner.MatMul(a, b);
        sw.Stop();
        _matMulLatency.Record(sw.Elapsed.TotalMilliseconds);
        if (!result.IsSuccess) _matMulErrors.Add(1);
        return result;
    }

    /// <inheritdoc/>
    public Result<ITensor<float>, string> Add(ITensor<float> a, ITensor<float> b)
    {
        _addCount.Add(1);
        var sw = Stopwatch.StartNew();
        var result = _inner.Add(a, b);
        sw.Stop();
        _addLatency.Record(sw.Elapsed.TotalMilliseconds);
        if (!result.IsSuccess) _addErrors.Add(1);
        return result;
    }
}
