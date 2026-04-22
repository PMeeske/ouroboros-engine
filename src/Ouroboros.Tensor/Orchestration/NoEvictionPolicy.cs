// <copyright file="NoEvictionPolicy.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Orchestration;

/// <summary>
/// Stub policy for tenants that must never be evicted (e.g. Realtime rasterizer).
/// <see cref="CanEvictNow"/> always returns <see langword="false"/> and
/// <see cref="EvictAsync"/> throws.
/// </summary>
public sealed class NoEvictionPolicy : IEvictionPolicy
{
    /// <inheritdoc/>
    public string TenantName { get; }

    /// <inheritdoc/>
    public TimeSpan EstimatedEvictionLatency => Timeout.InfiniteTimeSpan;

    /// <inheritdoc/>
    public TimeSpan EstimatedReloadLatency => Timeout.InfiniteTimeSpan;

    /// <summary>
    /// Initializes a new <see cref="NoEvictionPolicy"/>.
    /// </summary>
    /// <param name="tenantName">Tenant name.</param>
    public NoEvictionPolicy(string tenantName)
    {
        TenantName = tenantName ?? throw new ArgumentNullException(nameof(tenantName));
    }

    /// <inheritdoc/>
    public bool CanEvictNow() => false;

    /// <inheritdoc/>
    public Task<long> EvictAsync(CancellationToken cancellationToken = default)
        => throw new InvalidOperationException(
            $"Tenant '{TenantName}' is configured with NoEviction policy and cannot be evicted.");
}
