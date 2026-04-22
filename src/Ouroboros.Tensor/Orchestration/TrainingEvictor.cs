// <copyright file="TrainingEvictor.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Orchestration;

/// <summary>
/// Cooperative evictor for training loops. Performs a full model unload via the
/// caller-supplied callback and reports the reclaimed footprint.
/// </summary>
public sealed class TrainingEvictor : IEvictionPolicy
{
    private readonly Func<CancellationToken, Task<long>> _unloadAsync;

    /// <inheritdoc/>
    public string TenantName { get; }

    /// <inheritdoc/>
    public TimeSpan EstimatedEvictionLatency => TimeSpan.FromMilliseconds(500);

    /// <inheritdoc/>
    public TimeSpan EstimatedReloadLatency => TimeSpan.FromSeconds(5);

    /// <summary>
    /// Initializes a new <see cref="TrainingEvictor"/>.
    /// </summary>
    /// <param name="tenantName">Tenant name.</param>
    /// <param name="unloadAsync">Async callback that unloads the training state and returns bytes reclaimed.</param>
    public TrainingEvictor(string tenantName, Func<CancellationToken, Task<long>> unloadAsync)
    {
        TenantName = tenantName ?? throw new ArgumentNullException(nameof(tenantName));
        _unloadAsync = unloadAsync ?? throw new ArgumentNullException(nameof(unloadAsync));
    }

    /// <inheritdoc/>
    public bool CanEvictNow() => true;

    /// <inheritdoc/>
    public Task<long> EvictAsync(CancellationToken cancellationToken = default)
        => _unloadAsync(cancellationToken);
}
