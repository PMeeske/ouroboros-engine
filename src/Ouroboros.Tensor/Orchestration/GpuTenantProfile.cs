// <copyright file="GpuTenantProfile.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Orchestration;

/// <summary>
/// Priority class for the FreeRTOS-style GPU scheduler. Higher integer values indicate
/// higher priority. Strict preemption applies across classes; equal-priority tenants
/// round-robin within their class.
/// </summary>
/// <remarks>
/// Phase 196.5 introduces a four-class priority model that replaces the advisory
/// <see cref="GpuTaskPriority"/> hints. The mapping is
/// <c>Idle &lt; Background &lt; Normal &lt; Realtime</c>.
/// </remarks>
public enum GpuPriorityClass
{
    /// <summary>Deferred work (training loops, offline analysis) — runs only when nothing else is ready.</summary>
    Idle = 0,

    /// <summary>Batch / background jobs (pre-computation, warm-up).</summary>
    Background = 1,

    /// <summary>Standard interactive inference (LLM generation, tool calls).</summary>
    Normal = 2,

    /// <summary>Real-time, deadline-sensitive work (avatar rasterizer, audio synthesis).</summary>
    Realtime = 3,
}

/// <summary>
/// Describes how a tenant releases GPU memory when pressure demands eviction.
/// Plan 03 (196.5-03) wires the adapters that honor each policy.
/// </summary>
public enum EvictionPolicy
{
    /// <summary>Never evicted (reserved for Realtime tenants such as the rasterizer).</summary>
    None,

    /// <summary>Tenant exposes a cooperative unload hook; eviction is negotiated, not forced.</summary>
    Cooperative,

    /// <summary>Tenant relies on D3D12 heap tier-2 demotion (ORT DirectML sessions, Kokoro).</summary>
    HardHeap,

    /// <summary>Tenant fully unloads its model on eviction (training, Ollama keep-alive=0).</summary>
    FullUnload,
}

/// <summary>
/// Per-tenant contract registered with the scheduler. Captures the tenant's base priority,
/// declared VRAM footprint, maximum expected dispatch time (used by the watchdog in plan 04),
/// preemption capability, and eviction policy.
/// </summary>
/// <param name="TenantName">Stable, unique identifier for the tenant (e.g., "Rasterizer", "Kokoro", "Ollama").</param>
/// <param name="BasePriority">Baseline priority class; <see cref="EffectivePriority"/> tracks transient boosts.</param>
/// <param name="VramBytes">Declared VRAM footprint in bytes (best-effort estimate).</param>
/// <param name="MaxDispatchTime">Expected per-dispatch wall time. Plan 04 demotes tenants that sustain overruns.</param>
/// <param name="Preemptible">
/// Whether the tenant tolerates mid-work preemption. <see langword="false"/> means
/// preemption can only occur between work items, not mid-token/mid-frame.
/// </param>
/// <param name="Eviction">Eviction policy invoked when another tenant needs VRAM.</param>
public sealed record GpuTenantProfile(
    string TenantName,
    GpuPriorityClass BasePriority,
    long VramBytes,
    TimeSpan MaxDispatchTime,
    bool Preemptible,
    EvictionPolicy Eviction)
{
    /// <summary>
    /// Gets the current effective priority. Defaults to <see cref="BasePriority"/>.
    /// Plan 02 (priority inheritance) temporarily raises this via
    /// <see cref="WithEffectivePriority(GpuPriorityClass)"/> when a higher-priority task
    /// blocks on a resource held by this tenant.
    /// </summary>
    public GpuPriorityClass EffectivePriority { get; init; } = BasePriority;

    /// <summary>
    /// Returns a copy of this profile with a different <see cref="EffectivePriority"/>.
    /// Used by plan 02's priority inheritance mechanism; every other field is preserved.
    /// </summary>
    /// <param name="effective">New effective priority class.</param>
    /// <returns>A new <see cref="GpuTenantProfile"/> with the boosted effective priority.</returns>
    public GpuTenantProfile WithEffectivePriority(GpuPriorityClass effective)
        => this with { EffectivePriority = effective };
}
