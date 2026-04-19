// <copyright file="GpuTaskState.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Orchestration;

/// <summary>
/// Lifecycle state of a GPU task inside the FreeRTOS-style scheduler.
/// Modelled on FreeRTOS task states with two block subtypes (VRAM vs. generic resource)
/// so the eviction path (plan 03) can distinguish memory pressure from lock contention.
/// </summary>
public enum GpuTaskState
{
    /// <summary>Enqueued and eligible for dispatch.</summary>
    Ready,

    /// <summary>Currently executing on the GPU.</summary>
    Running,

    /// <summary>Blocked waiting for VRAM to become available (plan 03 eviction path).</summary>
    BlockedVram,

    /// <summary>Blocked on a non-memory resource (mutex, semaphore, external I/O).</summary>
    BlockedResource,

    /// <summary>Administratively paused; not a candidate for dispatch until resumed.</summary>
    Suspended,

    /// <summary>Work completed (success or failure). Terminal state.</summary>
    Done,
}
