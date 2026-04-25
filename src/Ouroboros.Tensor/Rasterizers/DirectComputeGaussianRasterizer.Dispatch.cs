// <copyright file="DirectComputeGaussianRasterizer.Dispatch.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Ouroboros.Tensor.Abstractions;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;
using D3DRange = Silk.NET.Direct3D12.Range;

namespace Ouroboros.Tensor.Rasterizers;

/// <summary>
/// Partial — D3D12 command-list recording for the four-stage rasterizer
/// pipeline (upload → project → tile-assign → tile-sort → tile-raster →
/// readback). Split from the primary partial to keep both files under the
/// 500-LOC project convention.
/// </summary>
/// <remarks>
/// Phase 188.1.1-03-phase2: this file holds the production command-list
/// recording body — PSO cache, root signature, buffer lifecycle,
/// per-frame dispatch + fence-synchronized readback. All COM resources
/// are wrapped in <c>ComPtr&lt;T&gt;</c> and disposed through the single
/// <see cref="ReleaseGpuResources"/> hook invoked from the primary
/// partial's <c>Dispose</c>.
/// </remarks>
public sealed partial class DirectComputeGaussianRasterizer
{
    private const int TileSize = 16;
    private const int MaxPerTile = 512;
    private const uint ReadbackPitchAlignment = 256u; // D3D12_TEXTURE_DATA_PITCH_ALIGNMENT

    // PSO + root signature cache (created once on first dispatch).
    private ComPtr<ID3D12RootSignature> _rsCompute;
    private ComPtr<ID3D12PipelineState> _psoProject;
    private ComPtr<ID3D12PipelineState> _psoTileAssign;
    private ComPtr<ID3D12PipelineState> _psoTileSort;
    private ComPtr<ID3D12PipelineState> _psoTileRaster;
    private ComPtr<ID3D12CommandAllocator> _cmdAllocator;
    private ComPtr<ID3D12GraphicsCommandList> _cmdList;
    private ComPtr<ID3D12DescriptorHeap> _srvUavHeap;
    private uint _descriptorSize;
    private bool _pipelineBuilt;

    // Cached per-frame buffers (recreated when size grows).
    private int _cachedGaussianCount;
    private int _cachedWidth;
    private int _cachedHeight;
    private int _cachedTileCount;
    private uint _cachedRowPitchAligned;
    private ComPtr<ID3D12Resource> _bufPositions;   // upload N*3 float
    private ComPtr<ID3D12Resource> _bufScales;      // upload N*3 float
    private ComPtr<ID3D12Resource> _bufOpacities;   // upload N float
    private ComPtr<ID3D12Resource> _bufColors;      // upload N*3 float (packed to float4 on-GPU)
    private ComPtr<ID3D12Resource> _bufColorsPacked; // upload N*4 float (register t3 = StructuredBuffer<float3> → SRV stride 16 honors HLSL alignment rules)
    private ComPtr<ID3D12Resource> _bufProjected;   // default N*float4
    private ComPtr<ID3D12Resource> _bufProjColors;  // default N*float4
    private ComPtr<ID3D12Resource> _bufTileCounts;  // default tilesX*tilesY*uint
    private ComPtr<ID3D12Resource> _bufTileCountsZero; // upload heap, pre-filled with zeros; used to re-zero tileCounts each frame
    private ComPtr<ID3D12Resource> _bufTileLists;   // default tilesX*tilesY*MaxPerTile*uint
    private ComPtr<ID3D12Resource> _bufCbv;         // upload 64-byte frame constants (8 x uint root consts OR CBV payload, 256-aligned)
    private ComPtr<ID3D12Resource> _texOutput;      // default RGBA8 RWTexture2D
    private ComPtr<ID3D12Resource> _bufReadback;    // readback pitch-aligned copy destination

    private async Task<FrameBuffer> DispatchInternalAsync(
        GaussianSet gaussians,
        CameraParams camera,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // If any of the four init-gate prerequisites wasn't met we fall
        // back; primary partial already catches and latches on any throw.
        if (_sharedDevice is null || !_sharedDevice.IsAvailable || _loadedShaders is null)
        {
            LatchCpu(reason: "device/shader prerequisites missing at dispatch time");
            return await _cpuFallback
                .RasterizeAsync(gaussians, camera, RasterizerRequirements.Cpu, cancellationToken)
                .ConfigureAwait(false);
        }

        var sw = Stopwatch.StartNew();
        _pendingTx = camera.ViewMatrix.Length >= 16 ? camera.ViewMatrix[12] : 0f;
        _pendingTy = camera.ViewMatrix.Length >= 16 ? camera.ViewMatrix[13] : 0f;
        EnsurePipelineBuilt();
        EnsureBuffersSized(gaussians.Count, camera.Width, camera.Height);
        UploadGaussians(gaussians);
        UploadFrameConstants(camera, gaussians.Count);
        RecordAndSubmit(gaussians.Count, camera.Width, camera.Height);
        ulong signalValue = (ulong)Interlocked.Increment(ref _fenceValue);
        SignalAfterSubmit(signalValue);
        await WaitOnFenceAsync(signalValue, cancellationToken).ConfigureAwait(false);
        byte[] rgba = MapAndCopyReadback(camera.Width, camera.Height);

        _wasGpuDispatched = true;
        sw.Stop();
        return new FrameBuffer(camera.Width, camera.Height, rgba, sw.ElapsedTicks);
    }

    private long _fenceValue;

    private unsafe void UploadGaussians(GaussianSet g)
    {
        CopyToUpload(_bufPositions, g.Positions.AsSpan(0, g.Count * 3));
        CopyToUpload(_bufScales,    g.Scales.AsSpan(0, g.Count * 3));
        CopyToUpload(_bufOpacities, g.Opacities.AsSpan(0, g.Count));
        CopyToUpload(_bufColors,    g.Colors.AsSpan(0, g.Count * 3));
    }

    private static unsafe void CopyToUpload(ComPtr<ID3D12Resource> buf, ReadOnlySpan<float> src)
    {
        D3DRange read = default; // empty read range ⇒ CPU never reads from this resource
        void* dst;
        int hr = buf.Handle->Map(0u, &read, &dst);
        if (hr < 0 || dst == null)
        {
            throw new InvalidOperationException($"Map upload buffer failed (hr=0x{hr:X8}).");
        }

        try
        {
            fixed (float* p = src)
            {
                Buffer.MemoryCopy(p, dst, src.Length * sizeof(float), src.Length * sizeof(float));
            }
        }
        finally
        {
            buf.Handle->Unmap(0u, (D3DRange*)null);
        }
    }

    private unsafe void UploadFrameConstants(CameraParams camera, int gaussianCount)
    {
        // Root constants path: we push constants per-pass inside RecordAndSubmit
        // (tile-assign has a different layout than project/raster). _bufCbv is
        // kept around for possible future CBV migration. No-op here by design.
        _ = camera;
        _ = gaussianCount;
    }

    private unsafe void RecordAndSubmit(int gaussianCount, int width, int height)
    {
        int tilesX = (width + TileSize - 1) / TileSize;
        int tilesY = (height + TileSize - 1) / TileSize;

        _cmdAllocator.Reset();
        _cmdList.Reset(_cmdAllocator, (ID3D12PipelineState*)null);

        ID3D12DescriptorHeap* heapPtr = _srvUavHeap.Handle;
        _cmdList.SetDescriptorHeaps(1u, &heapPtr);
        _cmdList.SetComputeRootSignature(_rsCompute);

        // Zero-init tile counters. ClearUnorderedAccessViewUint is gated to
        // ID3D12GraphicsCommandList10 in Silk.NET 2.23; we use a portable
        // CopyBufferRegion from a zero-filled upload buffer instead.
        // tileCounts was created in CopyDest state (or left there by the
        // previous dispatch's TransitionTileCountsBackToCopyDest below), so
        // the copy is valid without a leading barrier on dispatch 1; on
        // subsequent dispatches we rely on the trailing transition.
        _cmdList.CopyBufferRegion(_bufTileCounts.Handle, 0ul, _bufTileCountsZero.Handle, 0ul, (ulong)_cachedTileCount * 4ul);
        TransitionBuffer(_bufTileCounts, ResourceStates.CopyDest, ResourceStates.UnorderedAccess);

        // ---- PASS 1: gaussian_project ----
        // Root constants: (translateX, translateY, count, width, height, 0, 0, 0)
        uint* rc = stackalloc uint[8];
        float tx = Camera_TxFromView(width);
        float ty = Camera_TyFromView(height);
        rc[0] = BitConverter.SingleToUInt32Bits(tx);
        rc[1] = BitConverter.SingleToUInt32Bits(ty);
        rc[2] = (uint)gaussianCount;
        rc[3] = (uint)width;
        rc[4] = (uint)height;
        rc[5] = 0u;
        rc[6] = 0u;
        rc[7] = 0u;
        _cmdList.SetComputeRoot32BitConstants(0u, 8u, rc, 0u);
        _cmdList.SetComputeRootDescriptorTable(1u, GpuOffset(0)); // SRV table at slot 0..3
        _cmdList.SetComputeRootDescriptorTable(2u, GpuOffset(4)); // UAV table at slot 4..5 (projected/projColors)
        _cmdList.SetPipelineState(_psoProject);
        uint projectGroups = (uint)((gaussianCount + 63) / 64);
        _cmdList.Dispatch(projectGroups, 1u, 1u);

        UavBarrier(_bufProjected);
        UavBarrier(_bufProjColors);

        // ---- PASS 2: gaussian_tile_assign ----
        // Root constants: (count, tilesX, tilesY, width, height, 0, 0, 0)
        rc[0] = (uint)gaussianCount;
        rc[1] = (uint)tilesX;
        rc[2] = (uint)tilesY;
        rc[3] = (uint)width;
        rc[4] = (uint)height;
        rc[5] = 0u;
        rc[6] = 0u;
        rc[7] = 0u;
        _cmdList.SetComputeRoot32BitConstants(0u, 8u, rc, 0u);
        _cmdList.SetComputeRootDescriptorTable(1u, GpuOffset(9)); // SRV t0 = projected (slots 9..12)
        _cmdList.SetComputeRootDescriptorTable(2u, GpuOffset(6)); // UAV u0..u1 = tileCounts/tileLists
        _cmdList.SetPipelineState(_psoTileAssign);
        _cmdList.Dispatch(projectGroups, 1u, 1u);

        UavBarrier(_bufTileCounts);
        UavBarrier(_bufTileLists);

        // ---- PASS 3: gaussian_tile_sort (no-op syncpoint) ----
        _cmdList.SetPipelineState(_psoTileSort);
        _cmdList.Dispatch((uint)_cachedTileCount, 1u, 1u);
        UavBarrier(_bufTileLists);

        // ---- PASS 4: gaussian_tile_raster ----
        // Root constants: (tilesX, tilesY, width, height, 0, 0, 0, 0)
        rc[0] = (uint)tilesX;
        rc[1] = (uint)tilesY;
        rc[2] = (uint)width;
        rc[3] = (uint)height;
        rc[4] = 0u;
        rc[5] = 0u;
        rc[6] = 0u;
        rc[7] = 0u;
        _cmdList.SetComputeRoot32BitConstants(0u, 8u, rc, 0u);
        _cmdList.SetComputeRootDescriptorTable(1u, GpuOffset(9)); // SRV t0..t3 = projected/projColors/tileCounts/tileLists
        _cmdList.SetComputeRootDescriptorTable(2u, GpuOffset(8)); // UAV u0 = RWTexture2D output
        _cmdList.SetPipelineState(_psoTileRaster);
        _cmdList.Dispatch((uint)tilesX, (uint)tilesY, 1u);

        // Transition output texture -> CopySource, copy to readback buffer,
        // then transition back to UAV + tileCounts back to CopyDest so the
        // NEXT dispatch finds every resource in its starting state. All of
        // these barriers MUST be recorded before Close.
        TransitionTexture(_texOutput, ResourceStates.UnorderedAccess, ResourceStates.CopySource);
        CopyTextureToReadback(width, height);
        TransitionTexture(_texOutput, ResourceStates.CopySource, ResourceStates.UnorderedAccess);
        TransitionBuffer(_bufTileCounts, ResourceStates.UnorderedAccess, ResourceStates.CopyDest);

        _cmdList.Close();

        // Submit.
        ID3D12CommandList* raw = (ID3D12CommandList*)_cmdList.Handle;
        _sharedDevice!.ComputeQueue.ExecuteCommandLists(1u, &raw);
    }

    private unsafe void UavBarrier(ComPtr<ID3D12Resource> res)
    {
        ResourceBarrier b = default;
        b.Type = ResourceBarrierType.Uav;
        b.Flags = ResourceBarrierFlags.None;
        b.Anonymous.UAV = new ResourceUavBarrier { PResource = res.Handle };
        _cmdList.ResourceBarrier(1u, &b);
    }

    private unsafe void TransitionTexture(ComPtr<ID3D12Resource> res, ResourceStates before, ResourceStates after)
    {
        ResourceBarrier b = default;
        b.Type = ResourceBarrierType.Transition;
        b.Flags = ResourceBarrierFlags.None;
        b.Anonymous.Transition = new ResourceTransitionBarrier
        {
            PResource = res.Handle,
            Subresource = 0xFFFFFFFFu, // D3D12_RESOURCE_BARRIER_ALL_SUBRESOURCES
            StateBefore = before,
            StateAfter = after,
        };
        _cmdList.ResourceBarrier(1u, &b);
    }

    private unsafe void TransitionBuffer(ComPtr<ID3D12Resource> res, ResourceStates before, ResourceStates after)
        => TransitionTexture(res, before, after);

    private static unsafe void ZeroFillUploadBuffer(ComPtr<ID3D12Resource> buf, ulong byteCount)
    {
        D3DRange read = default;
        void* dst;
        int hr = buf.Handle->Map(0u, &read, &dst);
        if (hr < 0 || dst == null)
        {
            throw new InvalidOperationException($"Map zero upload buffer failed (hr=0x{hr:X8}).");
        }

        try
        {
            Unsafe.InitBlockUnaligned(dst, 0, (uint)byteCount);
        }
        finally
        {
            buf.Handle->Unmap(0u, (D3DRange*)null);
        }
    }

    private unsafe void CopyTextureToReadback(int width, int height)
    {
        // Source: subresource 0 of _texOutput.
        TextureCopyLocation src = default;
        src.PResource = _texOutput.Handle;
        src.Type = TextureCopyType.SubresourceIndex;
        src.Anonymous.SubresourceIndex = 0u;

        // Destination: placed-footprint view of _bufReadback.
        TextureCopyLocation dst = default;
        dst.PResource = _bufReadback.Handle;
        dst.Type = TextureCopyType.PlacedFootprint;
        dst.Anonymous.PlacedFootprint = new PlacedSubresourceFootprint
        {
            Offset = 0ul,
            Footprint = new SubresourceFootprint
            {
                Format = Format.FormatR8G8B8A8Unorm,
                Width = (uint)width,
                Height = (uint)height,
                Depth = 1u,
                RowPitch = _cachedRowPitchAligned,
            },
        };
        _cmdList.CopyTextureRegion(&dst, 0u, 0u, 0u, &src, (Box*)null);
    }

    private unsafe void SignalAfterSubmit(ulong signalValue)
    {
        int hr = _sharedDevice!.ComputeQueue.Signal(_sharedDevice.Fence, signalValue);
        if (hr < 0)
        {
            throw new InvalidOperationException($"CommandQueue.Signal failed (hr=0x{hr:X8}).");
        }
    }

    private async Task WaitOnFenceAsync(ulong signalValue, CancellationToken ct)
    {
        if (_sharedDevice!.Fence.GetCompletedValue() >= signalValue)
        {
            return;
        }

        using var evt = new ManualResetEventSlim(false);

        // Use a Win32 HANDLE-compatible event. ManualResetEventSlim isn't one,
        // so back it with a ManualResetEvent (WaitHandle → WaitHandle.Handle).
        using var winEvt = new ManualResetEvent(false);
        unsafe
        {
            int hr = _sharedDevice.Fence.SetEventOnCompletion(signalValue, winEvt.SafeWaitHandle.DangerousGetHandle().ToPointer());
            if (hr < 0)
            {
                throw new InvalidOperationException($"Fence.SetEventOnCompletion failed (hr=0x{hr:X8}).");
            }
        }

        // Wait honoring cancellation. Task.Run offloads the blocking wait so
        // we never block the dispatcher thread.
        await Task.Run(
            () =>
        {
            int idx = WaitHandle.WaitAny(new[] { winEvt, ct.WaitHandle });
            if (idx == 1)
            {
                ct.ThrowIfCancellationRequested();
            }
        }, ct).ConfigureAwait(false);
    }

    private unsafe byte[] MapAndCopyReadback(int width, int height)
    {
        D3DRange readAll = new() { Begin = 0u, End = (nuint)((int)_cachedRowPitchAligned * height) };
        void* src;
        int hr = _bufReadback.Handle->Map(0u, &readAll, &src);
        if (hr < 0 || src == null)
        {
            throw new InvalidOperationException($"Map readback failed (hr=0x{hr:X8}).");
        }

        try
        {
            byte[] rgba = new byte[width * height * 4];
            int srcRowBytes = (int)_cachedRowPitchAligned;
            int dstRowBytes = width * 4;
            byte* srcBytes = (byte*)src;
            fixed (byte* dstPin = rgba)
            {
                for (int row = 0; row < height; row++)
                {
                    Buffer.MemoryCopy(srcBytes + (row * srcRowBytes), dstPin + (row * dstRowBytes), dstRowBytes, dstRowBytes);
                }
            }

            return rgba;
        }
        finally
        {
            D3DRange empty = default;
            _bufReadback.Handle->Unmap(0u, &empty);
        }
    }

    private void ReleaseGpuResources()
    {
        ReleasePerFrameBuffers();
        _srvUavHeap.Dispose();
        _srvUavHeap = default;
        _cmdList.Dispose();
        _cmdList = default;
        _cmdAllocator.Dispose();
        _cmdAllocator = default;
        _psoTileRaster.Dispose();
        _psoTileRaster = default;
        _psoTileSort.Dispose();
        _psoTileSort = default;
        _psoTileAssign.Dispose();
        _psoTileAssign = default;
        _psoProject.Dispose();
        _psoProject = default;
        _rsCompute.Dispose();
        _rsCompute = default;
        _loadedShaders = null;
        _pipelineBuilt = false;
    }

    private void ReleasePerFrameBuffers()
    {
        _bufReadback.Dispose();
        _bufReadback = default;
        _texOutput.Dispose();
        _texOutput = default;
        _bufCbv.Dispose();
        _bufCbv = default;
        _bufTileCountsZero.Dispose();
        _bufTileCountsZero = default;
        _bufTileLists.Dispose();
        _bufTileLists = default;
        _bufTileCounts.Dispose();
        _bufTileCounts = default;
        _bufProjColors.Dispose();
        _bufProjColors = default;
        _bufProjected.Dispose();
        _bufProjected = default;
        _bufColorsPacked.Dispose();
        _bufColorsPacked = default;
        _bufColors.Dispose();
        _bufColors = default;
        _bufOpacities.Dispose();
        _bufOpacities = default;
        _bufScales.Dispose();
        _bufScales = default;
        _bufPositions.Dispose();
        _bufPositions = default;
        _cachedGaussianCount = 0;
        _cachedWidth = 0;
        _cachedHeight = 0;
        _cachedTileCount = 0;
        _cachedRowPitchAligned = 0u;
    }

    // The camera translate xy extraction mirrors CpuGaussianRasterizer:88-89.
    // Kept as a couple of tiny helpers to avoid threading camera through the
    // recorder; the values come from CameraParams.ViewMatrix[12] / [13] which
    // RecordAndSubmit can fetch via a thread-local, but because the view
    // matrix isn't stored on the rasterizer instance we capture it in
    // DispatchInternalAsync by stamping _pendingTx / _pendingTy before
    // calling RecordAndSubmit. (Kept local since this is the only consumer.)
    private float _pendingTx;
    private float _pendingTy;

    private float Camera_TxFromView() => _pendingTx;

    private float Camera_TyFromView() => _pendingTy;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint AlignUp(uint value, uint alignment) => (value + (alignment - 1u)) & ~(alignment - 1u);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int NextPow2(int value)
    {
        int v = 1;
        while (v < value)
        {
            v <<= 1;
        }

        return v;
    }
}
