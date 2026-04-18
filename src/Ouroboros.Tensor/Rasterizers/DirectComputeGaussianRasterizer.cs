// <copyright file="DirectComputeGaussianRasterizer.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Ouroboros.Tensor.Abstractions;
using Ouroboros.Tensor.Orchestration;

namespace Ouroboros.Tensor.Rasterizers;

/// <summary>
/// HLSL compute-shader rasterizer surface. Implements
/// <see cref="IGaussianRasterizer"/> and is the production DI registration
/// for the C# 3DGS live-render path. Dispatches via
/// <see cref="GpuScheduler"/> with <see cref="RasterizerRequirements.Realtime"/>.
/// </summary>
/// <remarks>
/// <para>
/// Plan 188.1-03 (this plan) landed the adapter surface, the
/// <see cref="IOnnxExecutionProvider"/> promotion, and the DI wiring so
/// <see cref="GaussianAvatarRenderer"/> (plan 05) can inject a single
/// <see cref="IGaussianRasterizer"/> and let the backend change underneath.
/// </para>
/// <para>
/// <b>Scope note:</b> this implementation currently delegates to an inner
/// <see cref="CpuGaussianRasterizer"/> — the correctness baseline from plan
/// 188.1-02. HLSL compute-shader authoring (projection, tile assignment,
/// sort, alpha-blend) + <c>Silk.NET.Direct3D12</c> wiring ship as a
/// follow-up phase (188.1.1) so the live-render pipeline could ship today
/// with the CPU path. The adapter surface, the scheduler routing, the VRAM
/// budget registration, and the DI seam are all final; only the shader
/// dispatch body is a stub. Swap-in of real HLSL is a pure internal change.
/// </para>
/// </remarks>
public sealed class DirectComputeGaussianRasterizer : IGaussianRasterizer
{
    private readonly CpuGaussianRasterizer _cpuFallback;
    private readonly ILogger _logger;

    /// <summary>Creates a new rasterizer wrapping a shared-device fallback to CPU.</summary>
    /// <param name="scheduler">GPU scheduler for dispatch routing.</param>
    /// <param name="logger">Optional logger.</param>
    public DirectComputeGaussianRasterizer(
        GpuScheduler? scheduler = null,
        ILogger<DirectComputeGaussianRasterizer>? logger = null)
    {
        _logger = logger ?? NullLogger<DirectComputeGaussianRasterizer>.Instance;
        _cpuFallback = new CpuGaussianRasterizer(scheduler);
    }

    /// <inheritdoc />
    public Task<FrameBuffer> RasterizeAsync(
        GaussianSet gaussians,
        CameraParams camera,
        GpuResourceRequirements requirements,
        CancellationToken cancellationToken = default)
    {
        // TODO(188.1.1) — dispatch HLSL compute shader via Silk.NET.Direct3D12
        // on a SharedD3D12Device using IVramLayout.AdapterLuid. Until then the
        // adapter honors its contract via the CPU baseline so the full
        // render pipeline is functional end-to-end.
        return _cpuFallback.RasterizeAsync(gaussians, camera, requirements, cancellationToken);
    }
}
