// <copyright file="TensorBackendBuilder.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Decorators;

/// <summary>
/// Fluent builder for composing <see cref="ITensorBackend"/> decorators in a predictable order:
/// Validation → Caching → Logging → Metrics (outer-most last) (R11, R16).
/// </summary>
/// <example>
/// <code>
/// var backend = new TensorBackendBuilder(CpuTensorBackend.Instance)
///     .WithValidation()
///     .WithLogging(logger)
///     .WithMetrics()
///     .Build();
/// </code>
/// </example>
public sealed class TensorBackendBuilder
{
    private ITensorBackend _backend;

    /// <summary>
    /// Initializes the builder with a concrete backend as the innermost layer.
    /// </summary>
    public TensorBackendBuilder(ITensorBackend backend)
    {
        ArgumentNullException.ThrowIfNull(backend);
        _backend = backend;
    }

    /// <summary>
    /// Wraps the current backend with a <see cref="ValidatingTensorBackend"/> that checks
    /// shapes and device compatibility before dispatching (R15).
    /// </summary>
    public TensorBackendBuilder WithValidation()
    {
        _backend = new ValidatingTensorBackend(_backend);
        return this;
    }

    /// <summary>
    /// Wraps the current backend with a <see cref="LoggingTensorBackend"/> that records each
    /// operation name, shapes, latency and outcome (R18).
    /// </summary>
    public TensorBackendBuilder WithLogging(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _backend = new LoggingTensorBackend(_backend, logger);
        return this;
    }

    /// <summary>
    /// Wraps the current backend with a <see cref="MetricsTensorBackend"/> that publishes
    /// latency histograms and operation counters to the <c>Ouroboros.Tensor</c> meter (R18).
    /// </summary>
    /// <param name="meter">Optional pre-existing meter; a default is created when null.</param>
    public TensorBackendBuilder WithMetrics(Meter? meter = null)
    {
        _backend = new MetricsTensorBackend(_backend, meter);
        return this;
    }

    /// <summary>
    /// Wraps the current backend with a <see cref="CachingTensorBackend"/> that memoises
    /// identical <see cref="ITensorBackend.Create"/> calls (R11).
    /// </summary>
    public TensorBackendBuilder WithCaching()
    {
        _backend = new CachingTensorBackend(_backend);
        return this;
    }

    /// <summary>Returns the fully decorated backend.</summary>
    public ITensorBackend Build() => _backend;
}
