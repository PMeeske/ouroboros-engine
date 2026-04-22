// <copyright file="IEvictionPolicy.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Orchestration;

/// <summary>
/// Contract for per-tenant cooperative eviction. Each tenant registers an implementation
/// that knows how to release its own GPU memory and how long eviction / reload takes.
/// </summary>
/// <remarks>
/// Plan 03 (196.5-03) wires these policies into <see cref="EvictionCoordinator"/> so that
/// VRAM pressure is resolved by priority-then-LRU victim selection with hysteresis.
/// </remarks>
public interface IEvictionPolicy
{
    /// <summary>Stable tenant identifier matching the <see cref="GpuTenantProfile.TenantName"/>.</summary>
    string TenantName { get; }

    /// <summary>Expected wall time to evict (dispose session, unload model, etc.).</summary>
    TimeSpan EstimatedEvictionLatency { get; }

    /// <summary>Expected wall time to reload the evicted state back into VRAM.</summary>
    TimeSpan EstimatedReloadLatency { get; }

    /// <summary>
    /// Whether this tenant is currently eligible for eviction. Returns <see langword="false"/>
    /// when the tenant has no resident state (e.g. session not yet created) or must not be
    /// disturbed (e.g. a Realtime rasterizer frame is in flight).
    /// </summary>
    bool CanEvictNow();

    /// <summary>
    /// Execute the eviction. Must be idempotent — if the tenant is already evicted,
    /// returns 0 without throwing.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Bytes reclaimed (best-effort estimate).</returns>
    Task<long> EvictAsync(CancellationToken cancellationToken = default);
}
