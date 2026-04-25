// <copyright file="DirectComputeGaussianRasterizer.Resources.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Tensor.Abstractions;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

namespace Ouroboros.Tensor.Rasterizers;

/// <summary>
/// Partial — lazy D3D12 resource lifecycle (root signature, PSO cache,
/// descriptor heap, per-frame buffers, SRV/UAV descriptor writes) for the
/// compute rasterizer. Extracted from <c>.Dispatch.cs</c> to keep every
/// partial comfortably under the 500-LOC project convention while
/// preserving the Phase 188.1.1 plan's "split allowed if needed" guidance.
/// </summary>
public sealed partial class DirectComputeGaussianRasterizer
{
    private unsafe void EnsurePipelineBuilt()
    {
        if (_pipelineBuilt)
        {
            return;
        }

        ComPtr<ID3D12Device> dev = _sharedDevice!.Device;

        // Root signature layout (compatible with all 4 shaders):
        //   [0] RootConstants b0 (8 x uint  — FrameConstants)
        //   [1] Descriptor table: SRV t0..t3 (4 slots)
        //   [2] Descriptor table: UAV u0..u1 (2 slots)
        // tile-raster uses RWTexture2D at u0 which fits the UAV range.
        DescriptorRange* ranges = stackalloc DescriptorRange[2];
        ranges[0] = new DescriptorRange
        {
            RangeType = DescriptorRangeType.Srv,
            NumDescriptors = 4u,
            BaseShaderRegister = 0u,
            RegisterSpace = 0u,
            OffsetInDescriptorsFromTableStart = 0u,
        };
        ranges[1] = new DescriptorRange
        {
            RangeType = DescriptorRangeType.Uav,
            NumDescriptors = 2u,
            BaseShaderRegister = 0u,
            RegisterSpace = 0u,

            // 188.1.1-03-phase2 gap-#2 fix: UAV range lives in its own standalone
            // descriptor table (root parameter 2). OffsetInDescriptorsFromTableStart
            // is relative to the TABLE's GPU handle base, not to a shared table
            // containing both SRV + UAV ranges. The per-pass caller sets the
            // table's base pointer directly to the UAV starting slot via
            // GpuOffset(N), so the in-range offset MUST be 0 — otherwise GPU
            // reads from slot N+4 instead of slot N. Every UAV dispatch across
            // project / tile-assign / tile-raster was reading the wrong slots
            // prior to this fix.
            OffsetInDescriptorsFromTableStart = 0u,
        };

        RootParameter* rootParams = stackalloc RootParameter[3];
        rootParams[0].ParameterType = RootParameterType.Type32BitConstants;
        rootParams[0].Anonymous.Constants = new RootConstants { ShaderRegister = 0u, RegisterSpace = 0u, Num32BitValues = 8u };
        rootParams[0].ShaderVisibility = ShaderVisibility.All;
        rootParams[1].ParameterType = RootParameterType.TypeDescriptorTable;
        rootParams[1].Anonymous.DescriptorTable = new RootDescriptorTable { NumDescriptorRanges = 1u, PDescriptorRanges = &ranges[0] };
        rootParams[1].ShaderVisibility = ShaderVisibility.All;
        rootParams[2].ParameterType = RootParameterType.TypeDescriptorTable;
        rootParams[2].Anonymous.DescriptorTable = new RootDescriptorTable { NumDescriptorRanges = 1u, PDescriptorRanges = &ranges[1] };
        rootParams[2].ShaderVisibility = ShaderVisibility.All;

        RootSignatureDesc rsDesc = new()
        {
            NumParameters = 3u,
            PParameters = rootParams,
            NumStaticSamplers = 0u,
            PStaticSamplers = null,
            Flags = RootSignatureFlags.None,
        };

        ComPtr<ID3D10Blob> blob = default;
        ComPtr<ID3D10Blob> err = default;
        try
        {
            using D3D12 api = D3D12.GetApi();
            int hr = api.SerializeRootSignature(&rsDesc, D3DRootSignatureVersion.Version1, ref blob, ref err);
            if (hr < 0 || blob.Handle == null)
            {
                throw new InvalidOperationException($"D3D12SerializeRootSignature failed (hr=0x{hr:X8}).");
            }

            Guid iidRs = ID3D12RootSignature.Guid;
            void* raw;
            hr = dev.CreateRootSignature(
                0u,
                blob.Handle->GetBufferPointer(),
                blob.Handle->GetBufferSize(),
                &iidRs,
                &raw);
            if (hr < 0 || raw == null)
            {
                throw new InvalidOperationException($"CreateRootSignature failed (hr=0x{hr:X8}).");
            }

            _rsCompute = new ComPtr<ID3D12RootSignature>((ID3D12RootSignature*)raw);
        }
        finally
        {
            err.Dispose();
            blob.Dispose();
        }

        _psoProject = CreatePso(dev, _loadedShaders!["gaussian_project"]);
        _psoTileAssign = CreatePso(dev, _loadedShaders!["gaussian_tile_assign"]);
        _psoTileSort = CreatePso(dev, _loadedShaders!["gaussian_tile_sort"]);
        _psoTileRaster = CreatePso(dev, _loadedShaders!["gaussian_tile_raster"]);

        // Command allocator + graphics command list (COMPUTE type).
        Guid iidAlloc = ID3D12CommandAllocator.Guid;
        void* rawAlloc;
        int hrA = dev.CreateCommandAllocator(CommandListType.Compute, &iidAlloc, &rawAlloc);
        if (hrA < 0 || rawAlloc == null)
        {
            throw new InvalidOperationException($"CreateCommandAllocator failed (hr=0x{hrA:X8}).");
        }

        _cmdAllocator = new ComPtr<ID3D12CommandAllocator>((ID3D12CommandAllocator*)rawAlloc);

        Guid iidList = ID3D12GraphicsCommandList.Guid;
        void* rawList;
        int hrL = dev.CreateCommandList(0u, CommandListType.Compute, _cmdAllocator, (ID3D12PipelineState*)null, &iidList, &rawList);
        if (hrL < 0 || rawList == null)
        {
            throw new InvalidOperationException($"CreateCommandList failed (hr=0x{hrL:X8}).");
        }

        _cmdList = new ComPtr<ID3D12GraphicsCommandList>((ID3D12GraphicsCommandList*)rawList);
        _cmdList.Close(); // created in recording state — close so reset is valid next dispatch

        // Shader-visible CBV/SRV/UAV descriptor heap: 13 persistent slots
        // (4 project SRVs, 2 project UAVs, 2 tile-assign UAVs, 1 raster UAV
        // texture, 4 tile-raster SRVs) + 3 headroom.
        DescriptorHeapDesc heapDesc = new()
        {
            Type = DescriptorHeapType.CbvSrvUav,
            NumDescriptors = 16u,
            Flags = DescriptorHeapFlags.ShaderVisible,
            NodeMask = 0u,
        };
        Guid iidHeap = ID3D12DescriptorHeap.Guid;
        void* rawHeap;
        int hrH = dev.CreateDescriptorHeap(&heapDesc, &iidHeap, &rawHeap);
        if (hrH < 0 || rawHeap == null)
        {
            throw new InvalidOperationException($"CreateDescriptorHeap failed (hr=0x{hrH:X8}).");
        }

        _srvUavHeap = new ComPtr<ID3D12DescriptorHeap>((ID3D12DescriptorHeap*)rawHeap);
        _descriptorSize = dev.GetDescriptorHandleIncrementSize(DescriptorHeapType.CbvSrvUav);

        _pipelineBuilt = true;
    }

    private unsafe ComPtr<ID3D12PipelineState> CreatePso(ComPtr<ID3D12Device> dev, byte[] dxil)
    {
        fixed (byte* p = dxil)
        {
            ComputePipelineStateDesc desc = new()
            {
                PRootSignature = _rsCompute.Handle,
                CS = new ShaderBytecode { PShaderBytecode = p, BytecodeLength = (nuint)dxil.Length },
                NodeMask = 0u,
                CachedPSO = default,
                Flags = PipelineStateFlags.None,
            };
            Guid iid = ID3D12PipelineState.Guid;
            void* raw;
            int hr = dev.CreateComputePipelineState(&desc, &iid, &raw);
            if (hr < 0 || raw == null)
            {
                throw new InvalidOperationException($"CreateComputePipelineState failed (hr=0x{hr:X8}).");
            }

            return new ComPtr<ID3D12PipelineState>((ID3D12PipelineState*)raw);
        }
    }

    private void EnsureBuffersSized(int gaussianCount, int width, int height)
    {
        int tilesX = (width + TileSize - 1) / TileSize;
        int tilesY = (height + TileSize - 1) / TileSize;
        int tileCount = tilesX * tilesY;
        uint rowPitchAligned = AlignUp((uint)width * 4u, ReadbackPitchAlignment);

        bool grow = gaussianCount > _cachedGaussianCount
            || width != _cachedWidth
            || height != _cachedHeight;
        if (!grow)
        {
            return;
        }

        ReleasePerFrameBuffers();

        int newCap = NextPow2(Math.Max(gaussianCount, 4422));
        CreateBuffers(newCap, width, height, tileCount, rowPitchAligned);

        _cachedGaussianCount = newCap;
        _cachedWidth = width;
        _cachedHeight = height;
        _cachedTileCount = tileCount;
        _cachedRowPitchAligned = rowPitchAligned;

        WriteDescriptorTable(newCap, tileCount, width, height);
    }

    private unsafe void CreateBuffers(int gaussianCap, int width, int height, int tileCount, uint rowPitchAligned)
    {
        ComPtr<ID3D12Device> dev = _sharedDevice!.Device;

        // Upload-heap SRV inputs (StructuredBuffer). float3 is packed by HLSL
        // as a 12-byte stride; D3D12 accepts that provided the total size
        // is a multiple of 4. We oversize conservatively to avoid edge cases.
        _bufPositions = CreateBuffer(dev, HeapType.Upload, (ulong)gaussianCap * 3ul * 4ul, ResourceStates.GenericRead, ResourceFlags.None);
        _bufScales = CreateBuffer(dev, HeapType.Upload, (ulong)gaussianCap * 3ul * 4ul, ResourceStates.GenericRead, ResourceFlags.None);
        _bufOpacities = CreateBuffer(dev, HeapType.Upload, (ulong)gaussianCap * 4ul,       ResourceStates.GenericRead, ResourceFlags.None);
        _bufColors = CreateBuffer(dev, HeapType.Upload, (ulong)gaussianCap * 3ul * 4ul, ResourceStates.GenericRead, ResourceFlags.None);

        // Default-heap UAV targets for project/tile-assign.
        _bufProjected = CreateBuffer(dev, HeapType.Default, (ulong)gaussianCap * 16ul,  ResourceStates.UnorderedAccess, ResourceFlags.AllowUnorderedAccess);
        _bufProjColors = CreateBuffer(dev, HeapType.Default, (ulong)gaussianCap * 16ul,  ResourceStates.UnorderedAccess, ResourceFlags.AllowUnorderedAccess);
        _bufTileCounts = CreateBuffer(dev, HeapType.Default, (ulong)tileCount * 4ul,     ResourceStates.CopyDest,        ResourceFlags.AllowUnorderedAccess);
        _bufTileLists = CreateBuffer(dev, HeapType.Default, (ulong)tileCount * (ulong)MaxPerTile * 4ul, ResourceStates.UnorderedAccess, ResourceFlags.AllowUnorderedAccess);

        // Zero-filled upload source used each frame to re-clear tileCounts
        // via CopyBufferRegion. ClearUnorderedAccessViewUint is gated to
        // ID3D12GraphicsCommandList10 in Silk.NET 2.23; this two-state
        // dance works on the base interface.
        _bufTileCountsZero = CreateBuffer(dev, HeapType.Upload, (ulong)tileCount * 4ul, ResourceStates.GenericRead, ResourceFlags.None);
        ZeroFillUploadBuffer(_bufTileCountsZero, (ulong)tileCount * 4ul);

        // Frame-constants upload buffer reserved for a future CBV migration;
        // we push 8×uint root constants today so this buffer is unused but
        // allocated for symmetry.
        _bufCbv = CreateBuffer(dev, HeapType.Upload, 256ul, ResourceStates.GenericRead, ResourceFlags.None);

        // Output RGBA8 texture.
        ResourceDesc texDesc = new()
        {
            Dimension = ResourceDimension.Texture2D,
            Alignment = 0ul,
            Width = (ulong)width,
            Height = (uint)height,
            DepthOrArraySize = 1,
            MipLevels = 1,
            Format = Format.FormatR8G8B8A8Unorm,
            SampleDesc = new SampleDesc { Count = 1u, Quality = 0u },
            Layout = TextureLayout.LayoutUnknown,
            Flags = ResourceFlags.AllowUnorderedAccess,
        };
        HeapProperties defHeap = new()
        {
            Type = HeapType.Default,
            CPUPageProperty = CpuPageProperty.Unknown,
            MemoryPoolPreference = MemoryPool.Unknown,
            CreationNodeMask = 0u,
            VisibleNodeMask = 0u,
        };
        Guid iidRes = ID3D12Resource.Guid;
        void* rawTex;
        int hrT = dev.CreateCommittedResource(&defHeap, HeapFlags.None, &texDesc, ResourceStates.UnorderedAccess, (ClearValue*)null, &iidRes, &rawTex);
        if (hrT < 0 || rawTex == null)
        {
            throw new InvalidOperationException($"CreateCommittedResource(texture) failed (hr=0x{hrT:X8}).");
        }

        _texOutput = new ComPtr<ID3D12Resource>((ID3D12Resource*)rawTex);

        // Readback buffer sized to pitch-aligned rows.
        ulong readbackBytes = (ulong)rowPitchAligned * (ulong)height;
        _bufReadback = CreateBuffer(dev, HeapType.Readback, readbackBytes, ResourceStates.CopyDest, ResourceFlags.None);
    }

    private static unsafe ComPtr<ID3D12Resource> CreateBuffer(
        ComPtr<ID3D12Device> dev, HeapType heapType, ulong sizeInBytes, ResourceStates initialState, ResourceFlags flags)
    {
        HeapProperties hp = new()
        {
            Type = heapType,
            CPUPageProperty = CpuPageProperty.Unknown,
            MemoryPoolPreference = MemoryPool.Unknown,
            CreationNodeMask = 0u,
            VisibleNodeMask = 0u,
        };
        ResourceDesc desc = new()
        {
            Dimension = ResourceDimension.Buffer,
            Alignment = 0ul,
            Width = sizeInBytes,
            Height = 1u,
            DepthOrArraySize = 1,
            MipLevels = 1,
            Format = Format.FormatUnknown,
            SampleDesc = new SampleDesc { Count = 1u, Quality = 0u },
            Layout = TextureLayout.LayoutRowMajor,
            Flags = flags,
        };
        Guid iid = ID3D12Resource.Guid;
        void* raw;
        int hr = dev.CreateCommittedResource(&hp, HeapFlags.None, &desc, initialState, (ClearValue*)null, &iid, &raw);
        if (hr < 0 || raw == null)
        {
            throw new InvalidOperationException($"CreateCommittedResource(buffer,heap={heapType}) failed (hr=0x{hr:X8}).");
        }

        return new ComPtr<ID3D12Resource>((ID3D12Resource*)raw);
    }

    private unsafe void WriteDescriptorTable(int gaussianCap, int tileCount, int width, int height)
    {
        ComPtr<ID3D12Device> dev = _sharedDevice!.Device;
        CpuDescriptorHandle baseH = _srvUavHeap.GetCPUDescriptorHandleForHeapStart();

        // Slot 0..3 = SRVs for the project pass (positions/scales/opacities/colors).
        WriteSrv(dev, _bufPositions, gaussianCap, 12u,  Offset(baseH, 0));
        WriteSrv(dev, _bufScales,    gaussianCap, 12u,  Offset(baseH, 1));
        WriteSrv(dev, _bufOpacities, gaussianCap, 4u,   Offset(baseH, 2));
        WriteSrv(dev, _bufColors,    gaussianCap, 12u,  Offset(baseH, 3));

        // Slot 4..5 = UAVs for project (projected + projColors).
        WriteUav(dev, _bufProjected,  gaussianCap, 16u, Offset(baseH, 4));
        WriteUav(dev, _bufProjColors, gaussianCap, 16u, Offset(baseH, 5));

        // Slot 6..7 = tile-assign UAVs (tileCounts + tileLists).
        // Slot 8 = tile-raster RWTexture2D.
        WriteUav(dev, _bufTileCounts, tileCount,              4u, Offset(baseH, 6));
        WriteUav(dev, _bufTileLists,  tileCount * MaxPerTile, 4u, Offset(baseH, 7));
        WriteUavTex2D(dev, _texOutput, Offset(baseH, 8));

        // Slot 9..12 = tile-raster / tile-assign SRVs
        // (projected/projColors/tileCounts/tileLists) — written as a dedicated
        // block so no slot is rewritten mid-frame.
        WriteSrv(dev, _bufProjected,  gaussianCap, 16u, Offset(baseH, 9));
        WriteSrv(dev, _bufProjColors, gaussianCap, 16u, Offset(baseH, 10));
        WriteSrv(dev, _bufTileCounts, tileCount,              4u, Offset(baseH, 11));
        WriteSrv(dev, _bufTileLists,  tileCount * MaxPerTile, 4u, Offset(baseH, 12));
    }

    private static unsafe void WriteSrv(ComPtr<ID3D12Device> dev, ComPtr<ID3D12Resource> buf, int count, uint stride, CpuDescriptorHandle h)
    {
        ShaderResourceViewDesc d = new()
        {
            Format = Format.FormatUnknown,
            ViewDimension = SrvDimension.Buffer,
            Shader4ComponentMapping = 0x1688, // D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING
        };
        d.Anonymous.Buffer = new BufferSrv { FirstElement = 0ul, NumElements = (uint)count, StructureByteStride = stride, Flags = BufferSrvFlags.None };
        dev.CreateShaderResourceView(buf, &d, h);
    }

    private static unsafe void WriteUav(ComPtr<ID3D12Device> dev, ComPtr<ID3D12Resource> buf, int count, uint stride, CpuDescriptorHandle h)
    {
        UnorderedAccessViewDesc d = new()
        {
            Format = Format.FormatUnknown,
            ViewDimension = UavDimension.Buffer,
        };
        d.Anonymous.Buffer = new BufferUav { FirstElement = 0ul, NumElements = (uint)count, StructureByteStride = stride, CounterOffsetInBytes = 0ul, Flags = BufferUavFlags.None };
        dev.CreateUnorderedAccessView(buf, (ID3D12Resource*)null, &d, h);
    }

    private static unsafe void WriteUavTex2D(ComPtr<ID3D12Device> dev, ComPtr<ID3D12Resource> tex, CpuDescriptorHandle handle)
    {
        UnorderedAccessViewDesc d = new()
        {
            Format = Format.FormatR8G8B8A8Unorm,
            ViewDimension = UavDimension.Texture2D,
        };
        d.Anonymous.Texture2D = new Tex2DUav { MipSlice = 0u, PlaneSlice = 0u };
        dev.CreateUnorderedAccessView(tex, (ID3D12Resource*)null, &d, handle);
    }

    private CpuDescriptorHandle Offset(CpuDescriptorHandle start, int slot)
        => new() { Ptr = start.Ptr + (nuint)(slot * (int)_descriptorSize) };

    private GpuDescriptorHandle GpuOffset(int slot)
    {
        GpuDescriptorHandle start = _srvUavHeap.GetGPUDescriptorHandleForHeapStart();
        return new GpuDescriptorHandle { Ptr = start.Ptr + (ulong)(slot * (int)_descriptorSize) };
    }
}
