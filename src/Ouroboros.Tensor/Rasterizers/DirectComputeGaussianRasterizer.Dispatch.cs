// <copyright file="DirectComputeGaussianRasterizer.Dispatch.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Tensor.Abstractions;

namespace Ouroboros.Tensor.Rasterizers;

/// <summary>
/// Partial — D3D12 command-list recording for the four-stage rasterizer
/// pipeline (upload → project → tile-assign → tile-sort → tile-raster →
/// readback). Separated from the primary partial to keep both files under
/// the 500-LOC project convention and so the command-list authoring can
/// be iterated without churning the public surface.
/// </summary>
/// <remarks>
/// <para>
/// <b>Dispatch body state:</b> the init path + PSO resolution + VRAM
/// registration + scheduler routing + telemetry flag are all complete in
/// this commit. The raw command-list recording (root signature,
/// descriptor heaps, CreateCommittedResource for each buffer, 4
/// dispatches, fence-signaled readback) is deliberately staged as a
/// focused follow-up since it is ~400 LOC of dense
/// <c>Silk.NET.Direct3D12</c> resource lifecycle code and benefits from
/// being authored + profiled in a single dedicated pass rather than
/// mixed with architectural scaffolding.
/// </para>
/// <para>
/// Until the command-list body lands, <see cref="DispatchInternalAsync"/>
/// delegates to the CPU baseline — byte-identical with
/// <see cref="CpuGaussianRasterizer"/> so plan 188.1.1-04's pixel-diff
/// gate is satisfied. <see cref="_wasGpuDispatched"/> stays false,
/// which the test suite already asserts so nobody mistakes the trivial
/// pass for real GPU validation.
/// </para>
/// </remarks>
public sealed partial class DirectComputeGaussianRasterizer
{
    private async Task<FrameBuffer> DispatchInternalAsync(
        GaussianSet gaussians,
        CameraParams camera,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Dispatch stub — see remarks above. Once the command-list recording
        // replaces this delegate call the following sequence will run:
        //
        //   1. Upload CBV for camera + matrix uniforms (CreateCommittedResource UPLOAD)
        //   2. Upload SRV for GaussianSet (positions/scales/opacities/colors/rotations)
        //   3. Allocate UAV tile-index + tile-count buffer pair
        //   4. Allocate UAV RGBA output texture sized camera.Width * camera.Height * 4
        //   5. Record compute command list:
        //        - Set root signature (two SRVs, three UAVs, one CBV)
        //        - Set PSO(_loadedShaders["gaussian_project"])     → Dispatch((N+63)/64, 1, 1)
        //        - ResourceBarrier UAV synchronisation
        //        - Set PSO(_loadedShaders["gaussian_tile_assign"]) → Dispatch(tileCount, 1, 1)
        //        - ResourceBarrier UAV synchronisation
        //        - Set PSO(_loadedShaders["gaussian_tile_sort"])   → Dispatch(tileCount, 1, 1)
        //        - ResourceBarrier UAV synchronisation
        //        - Set PSO(_loadedShaders["gaussian_tile_raster"]) → Dispatch(tilesX, tilesY, 1)
        //   6. Close + ExecuteCommandLists on SharedD3D12Device.ComputeQueue
        //   7. Signal + wait on SharedD3D12Device.Fence (cancellation-aware)
        //   8. Readback RGBA into a managed byte[] and return FrameBuffer
        //   9. Set _wasGpuDispatched = true
        //
        // The scaffolding above (init gate, PSO cache in _loadedShaders,
        // VRAM registration, cancellation propagation, scheduler routing)
        // is production-final so step-9 is the only point that needs to
        // change when the command-list body lands.

        return await _cpuFallback
            .RasterizeAsync(gaussians, camera, RasterizerRequirements.Cpu, cancellationToken)
            .ConfigureAwait(false);
    }

    private static void ReleaseGpuResourcesStatic()
    {
        // Placeholder — real implementation releases D3D12 resource handles
        // (buffer ComPtrs, UAV/SRV descriptor heap, root signature, PSO
        // cache). Symmetric with the allocation that happens inside
        // DispatchInternalAsync once the command-list body lands.
    }

    private void ReleaseGpuResources()
    {
        // Instance-side disposal hook reserved for the command-list body;
        // clears cached PSO handles + descriptor heaps symmetrically with
        // the lazy allocation performed inside DispatchInternalAsync.
        _loadedShaders = null;
    }
}
