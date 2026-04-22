// <copyright file="IGpuScheduler.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Orchestration;

/// <summary>
/// v2 GPU scheduler surface introduced in Phase 196.5. Tenants register a
/// <see cref="GpuTenantProfile"/> and submit work tagged by tenant name; the scheduler
/// enforces strict priority preemption across <see cref="GpuPriorityClass"/> levels and
/// round-robin dispatch within equal priority.
/// </summary>
/// <remarks>
/// <para>
/// The legacy <see cref="GpuScheduler"/> class remains as a thin adapter that forwards
/// to this interface using synthetic per-priority tenants, so existing call sites
/// (Avatar pipeline, Kokoro, rasterizer) continue to compile unchanged.
/// </para>
/// <para>
/// Plan 02 (priority inheritance), plan 03 (cooperative eviction), plan 04 (watchdog +
/// tickless idle), and plan 05 (Ollama / DXGI reconciliation) extend this surface.
/// </para>
/// </remarks>
public interface IGpuScheduler : IDisposable
{
    /// <summary>Gets a snapshot of current scheduler metrics.</summary>
    GpuSchedulerMetrics CurrentMetrics { get; }

    /// <summary>
    /// Returns a snapshot of every registered tenant's current state.
    /// </summary>
    /// <returns>Immutable list of tenant snapshots.</returns>
    IReadOnlyList<GpuTenantSnapshot> GetTenantSnapshots();

    /// <summary>
    /// Registers a tenant with the scheduler. The returned <see cref="IDisposable"/>
    /// unregisters the tenant on dispose; any subsequent <see cref="ScheduleAsync{T}"/>
    /// call for that tenant throws <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <param name="profile">Tenant contract.</param>
    /// <returns>Handle that unregisters the tenant on dispose.</returns>
    IDisposable RegisterTenant(GpuTenantProfile profile);

    /// <summary>
    /// Registers a tenant-specific eviction policy with the scheduler. The policy is
    /// consulted when VRAM pressure requires cooperative unloading.
    /// </summary>
    /// <param name="policy">Eviction policy implementation.</param>
    void RegisterEvictionPolicy(IEvictionPolicy policy);

    /// <summary>
    /// Enqueues GPU work against a registered tenant. Work runs inside the scheduler's
    /// dispatch loop according to the tenant's current effective priority.
    /// </summary>
    /// <typeparam name="T">Return type of the work delegate.</typeparam>
    /// <param name="tenantName">Name of a previously registered tenant.</param>
    /// <param name="work">Work delegate; receives the dispatch cancellation token.</param>
    /// <param name="cancellationToken">Caller cancellation.</param>
    /// <returns>The result of <paramref name="work"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the tenant is not registered.</exception>
    Task<T> ScheduleAsync<T>(
        string tenantName,
        Func<CancellationToken, Task<T>> work,
        CancellationToken cancellationToken = default);
}
