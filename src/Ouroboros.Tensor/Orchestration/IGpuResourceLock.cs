// <copyright file="IGpuResourceLock.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Orchestration;

/// <summary>
/// Named GPU resource lock with priority inheritance.
/// </summary>
/// <remarks>
/// Plan 02 introduces single-hop priority inheritance: when a higher-priority tenant
/// blocks on <see cref="AcquireAsync"/>, the scheduler boosts the current holder's
/// <see cref="GpuTenantProfile.EffectivePriority"/>; on release the holder is restored
/// to <see cref="GpuTenantProfile.BasePriority"/>.
/// </remarks>
public interface IGpuResourceLock
{
    /// <summary>Gets the human-readable name of the resource.</summary>
    string ResourceName { get; }

    /// <summary>
    /// Acquires the resource. Returns immediately if the resource is free; otherwise
    /// enqueues the caller and blocks until the resource becomes available.
    /// </summary>
    /// <param name="tenantName">Tenant requesting the resource.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the resource is acquired.</returns>
    Task AcquireAsync(string tenantName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases the resource. Restores the holder's base priority and grants the
    /// resource to the highest-priority waiting tenant (not FIFO).
    /// </summary>
    /// <param name="tenantName">Tenant that currently holds the resource.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the release bookkeeping is done.</returns>
    /// <exception cref="InvalidOperationException">Thrown if <paramref name="tenantName"/> does not hold the resource.</exception>
    Task ReleaseAsync(string tenantName, CancellationToken cancellationToken = default);
}
