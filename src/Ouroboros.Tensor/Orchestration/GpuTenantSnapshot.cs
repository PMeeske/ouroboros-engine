// <copyright file="GpuTenantSnapshot.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Orchestration;

/// <summary>
/// Read-only snapshot of a scheduler tenant's current state, suitable for
/// CLI dashboards and telemetry. Produced by <see cref="GpuSchedulerV2.GetTenantSnapshots"/>.
/// </summary>
/// <param name="TenantName">Registered tenant identifier.</param>
/// <param name="BasePriority">Baseline priority class.</param>
/// <param name="EffectivePriority">Current priority (may differ when inherited).</param>
/// <param name="State">Observed task state.</param>
/// <param name="VramBytes">Declared VRAM footprint.</param>
/// <param name="QueueDepth">Number of queued work items.</param>
/// <param name="EvictionPolicy">Policy used when VRAM pressure demands eviction.</param>
public sealed record GpuTenantSnapshot(
    string TenantName,
    GpuPriorityClass BasePriority,
    GpuPriorityClass EffectivePriority,
    GpuTaskState State,
    long VramBytes,
    int QueueDepth,
    EvictionPolicy EvictionPolicy);
