// <copyright file="IGaussianRasterizer.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Tensor.Orchestration;

namespace Ouroboros.Tensor.Abstractions;

/// <summary>
/// Adapter contract for a 3D Gaussian Splatting rasterizer. Implementations
/// dispatch through <see cref="GpuScheduler"/> using a caller-supplied
/// <see cref="GpuResourceRequirements"/> so different callers can request
/// different VRAM budgets against the same rasterizer.
/// </summary>
/// <remarks>
/// <para>
/// Introduced in Phase 188.1 (AVA-01). The CPU baseline
/// (<c>CpuGaussianRasterizer</c>) and the HLSL compute-shader implementation
/// (<c>DirectComputeGaussianRasterizer</c>, plan 03) both target this
/// contract. Plan 05's renderer refactor swaps the App-layer CPU call site
/// over to this interface without knowing which implementation is wired.
/// </para>
/// </remarks>
public interface IGaussianRasterizer
{
    /// <summary>
    /// Rasterizes <paramref name="gaussians"/> with the supplied
    /// <paramref name="camera"/> and returns an RGBA <see cref="FrameBuffer"/>.
    /// </summary>
    /// <param name="gaussians">Mesh-bound 3DGS state to project + blend.</param>
    /// <param name="camera">Camera parameters (view + projection + viewport).</param>
    /// <param name="requirements">
    /// GPU resource requirements for the scheduler. Pass
    /// <see cref="RasterizerRequirements.Realtime"/> for HLSL dispatches or
    /// <see cref="RasterizerRequirements.Cpu"/> for the CPU baseline.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An RGBA <see cref="FrameBuffer"/> with <c>camera.Width * camera.Height * 4</c> bytes.</returns>
    Task<FrameBuffer> RasterizeAsync(
        GaussianSet gaussians,
        CameraParams camera,
        GpuResourceRequirements requirements,
        CancellationToken cancellationToken = default);
}
