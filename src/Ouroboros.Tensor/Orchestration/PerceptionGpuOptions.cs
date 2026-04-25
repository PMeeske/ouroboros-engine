// <copyright file="PerceptionGpuOptions.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Orchestration;

/// <summary>
/// Strongly-typed options for the perception GPU governance stack (Phase 235).
/// Bound from configuration section <c>Perception:GpuBudget</c>.
/// </summary>
/// <remarks>
/// Defaults are tuned for an RDNA 4 RX 9060 XT (16 GB VRAM) coexisting with a 3DGS
/// rasterizer + Kokoro TTS: the research synthesis ceilings perception at ~91 MB of
/// model weights across 5 ONNX sessions. <see cref="MaxVramMb"/> leaves a generous
/// headroom above that for working tensors and DirectML bookkeeping.
/// </remarks>
public sealed class PerceptionGpuOptions
{
    /// <summary>Configuration section path: <c>Perception:GpuBudget</c>.</summary>
    public const string SectionName = "Perception:GpuBudget";

    /// <summary>
    /// Gets or sets total perception-tier VRAM budget in megabytes. Every ONNX session loaded
    /// at the <see cref="GpuPriorityClass.Perception"/> tier reserves against
    /// this cap via <see cref="IPerceptionVramBudget.TryReserveAsync"/> before
    /// creation. Default <c>200</c>.
    /// </summary>
    public int MaxVramMb { get; set; } = 200;

    /// <summary>
    /// Gets or sets a value indicating whether when <see langword="true"/>, perception capture throttles its sample rate
    /// between <see cref="MinSampleRateHz"/> and <see cref="MaxSampleRateHz"/>
    /// based on VRAM pressure. When <see langword="false"/>, the pipeline stays
    /// at <see cref="MaxSampleRateHz"/> unless it is paused externally.
    /// Default <see langword="true"/>.
    /// </summary>
    public bool AdaptiveRateEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets minimum sample rate in Hz when the pipeline is throttled by VRAM pressure
    /// but not fully paused. Default <c>1</c>.
    /// </summary>
    public double MinSampleRateHz { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets maximum sample rate in Hz when no pressure is present. Default <c>5</c>.
    /// </summary>
    public double MaxSampleRateHz { get; set; } = 5.0;

    /// <summary>
    /// Gets or sets reserved-bytes threshold (in megabytes) above which the pipeline drops
    /// from <see cref="MaxSampleRateHz"/> to <see cref="MinSampleRateHz"/>.
    /// Once reservations reach <see cref="MaxVramMb"/>, the rate drops to 0
    /// (paused). Default <c>150</c>.
    /// </summary>
    public int PressureDropThresholdMb { get; set; } = 150;
}
