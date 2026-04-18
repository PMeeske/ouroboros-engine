// <copyright file="RasterizerRequirements.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Tensor.Orchestration;

namespace Ouroboros.Tensor.Abstractions;

/// <summary>
/// Well-known <see cref="GpuResourceRequirements"/> presets for
/// <see cref="IGaussianRasterizer"/> dispatches. Mirrors the
/// <c>OnnxInference.RealtimeInferenceRequirements</c> static-field pattern
/// so callers can declare intent without hand-rolling byte counts.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Realtime"/> matches the <c>VramBucket.Rasterizer</c> budget in
/// <c>RX9060XT_16GB</c> (plan 01): 128 MiB covers projected-gaussian buffer,
/// per-tile index buffer, and output RGBA buffer for the mesh-bound head
/// checkpoint (~4422 gaussians). <see cref="Cpu"/> reports zero bytes —
/// the CPU baseline still routes through <c>GpuScheduler</c> for telemetry
/// parity but never trips the VRAM overcommit guard.
/// </para>
/// </remarks>
public static class RasterizerRequirements
{
    /// <summary>
    /// Realtime HLSL compute-shader raster — 128 MiB VRAM estimate matching
    /// the <c>VramBucket.Rasterizer</c> budget from <c>VramLayoutPresets</c>
    /// (RX9060XT_16GB). Consumed by the plan-03 D3D12 rasterizer.
    /// </summary>
    public static readonly GpuResourceRequirements Realtime =
        new(EstimatedVramBytes: 128L * 1024 * 1024, RequiresExclusiveAccess: false);

    /// <summary>
    /// CPU baseline raster — reports zero VRAM so the scheduler's overcommit
    /// guard passes unconditionally. The CPU path still goes through
    /// <see cref="GpuScheduler.ScheduleAsync"/> for latency/queue telemetry
    /// so the fallback and the realtime path share observability.
    /// </summary>
    public static readonly GpuResourceRequirements Cpu =
        new(EstimatedVramBytes: 0, RequiresExclusiveAccess: false);
}
