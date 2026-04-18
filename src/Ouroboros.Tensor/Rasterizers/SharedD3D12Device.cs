// <copyright file="SharedD3D12Device.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Ouroboros.Tensor.Abstractions;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

namespace Ouroboros.Tensor.Rasterizers;

/// <summary>
/// Disposable wrapper around an <c>ID3D12Device</c> plus a single compute
/// <c>ID3D12CommandQueue</c> and <c>ID3D12Fence</c>, resolved from
/// <see cref="IVramLayout.AdapterLuid"/> via a single
/// <c>IDXGIFactory4.EnumAdapterByLuid</c> call — no second DXGI enumeration
/// beyond the one performed by <c>DxgiVramLayoutProvider</c>.
/// </summary>
/// <remarks>
/// <para>
/// Phase 188.1.1 (AVA-02-1) infrastructure. Owned by the
/// <c>DirectComputeGaussianRasterizer</c> DI singleton; a single instance
/// is registered once per service collection via
/// <c>GaussianRasterizerExtensions.AddDirectComputeGaussianRasterizer</c>.
/// </para>
/// <para>
/// The factory <see cref="TryCreate"/> never throws. Device creation can
/// fail for any of the following reasons and all are treated as a signal
/// that <see cref="IsAvailable"/> should be false and the caller should
/// latch its CPU fallback:
/// <list type="bullet">
///   <item>Non-Windows host (<see cref="DllNotFoundException"/>).</item>
///   <item>No D3D12 feature-level 11.0 adapter matching the LUID.</item>
///   <item><see cref="IVramLayout.AdapterLuid"/> is <c>0UL</c> (in-code preset).</item>
///   <item>Driver missing / COM failure / access denied.</item>
/// </list>
/// </para>
/// </remarks>
public sealed class SharedD3D12Device : IDisposable
{
    private readonly ILogger<SharedD3D12Device>? _logger;
    private ComPtr<ID3D12Device> _device;
    private ComPtr<ID3D12CommandQueue> _computeQueue;
    private ComPtr<ID3D12Fence> _fence;
    private int _disposed;

    private SharedD3D12Device(ILogger<SharedD3D12Device>? logger)
    {
        _logger = logger;
    }

    /// <summary>Gets a value indicating whether the underlying D3D12 device, queue, and fence are live.</summary>
    public bool IsAvailable { get; private set; }

    /// <summary>Gets the LUID this device was bound to (mirrors <see cref="IVramLayout.AdapterLuid"/>).</summary>
    public ulong ResolvedAdapterLuid { get; private set; }

    /// <summary>Gets the <c>ID3D12Device</c> ComPtr. Throws when <see cref="IsAvailable"/> is false.</summary>
    public ComPtr<ID3D12Device> Device
    {
        get
        {
            ThrowIfUnavailable();
            return _device;
        }
    }

    /// <summary>Gets the compute <c>ID3D12CommandQueue</c> ComPtr. Throws when <see cref="IsAvailable"/> is false.</summary>
    public ComPtr<ID3D12CommandQueue> ComputeQueue
    {
        get
        {
            ThrowIfUnavailable();
            return _computeQueue;
        }
    }

    /// <summary>Gets the <c>ID3D12Fence</c> ComPtr. Throws when <see cref="IsAvailable"/> is false.</summary>
    public ComPtr<ID3D12Fence> Fence
    {
        get
        {
            ThrowIfUnavailable();
            return _fence;
        }
    }

    /// <summary>
    /// Factory: never throws. Produces an instance with
    /// <see cref="IsAvailable"/> set to <see langword="false"/> on any failure
    /// and logs a single WARN describing the reason.
    /// </summary>
    /// <param name="layout">Injected VRAM layout. Its <see cref="IVramLayout.AdapterLuid"/> drives adapter lookup.</param>
    /// <param name="logger">Optional logger for failure diagnostics.</param>
    /// <returns>A disposable instance. Caller must check <see cref="IsAvailable"/> before accessing accessors.</returns>
    [SuppressMessage(
        "Design", "CA1031:Do not catch general exception types",
        Justification = "TryCreate is the designated fail-safe seam; every failure must degrade to IsAvailable=false without throwing.")]
    public static SharedD3D12Device TryCreate(
        IVramLayout layout,
        ILogger<SharedD3D12Device>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(layout);

        var instance = new SharedD3D12Device(logger);

        if (layout.AdapterLuid == 0UL)
        {
            logger?.LogWarning(
                "SharedD3D12Device: layout '{Id}' has sentinel LUID — D3D12 device unavailable, rasterizer will delegate to CPU baseline",
                layout.Id);
            return instance;
        }

        try
        {
            instance.CreateDeviceFromLuid(layout);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(
                ex,
                "SharedD3D12Device: D3D12 device creation failed for layout '{Id}' (LUID=0x{Luid:X16}) — {ExType}: {ExMessage} — latching CPU fallback",
                layout.Id, layout.AdapterLuid, ex.GetType().Name, ex.Message);
            instance.ReleaseHandles();
            instance.IsAvailable = false;
        }

        return instance;
    }

    /// <summary>Releases the fence, compute queue, and device in reverse creation order. Idempotent.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        ReleaseHandles();
    }

    private unsafe void CreateDeviceFromLuid(IVramLayout layout)
    {
        DXGI? dxgi = null;
        D3D12? d3d12 = null;
        ComPtr<IDXGIFactory4> factory = default;
        ComPtr<IDXGIAdapter1> adapter = default;

        try
        {
            dxgi = DXGI.GetApi();
            int hr = dxgi.CreateDXGIFactory2(0u, out factory);
            if (hr < 0 || factory.Handle == null)
            {
                throw new InvalidOperationException($"CreateDXGIFactory2 failed (hr=0x{hr:X8}).");
            }

            var luid = new Luid
            {
                Low = (uint)(layout.AdapterLuid & 0xFFFFFFFFu),
                High = (int)(layout.AdapterLuid >> 32),
            };

            hr = factory.EnumAdapterByLuid(luid, out adapter);
            if (hr < 0 || adapter.Handle == null)
            {
                throw new InvalidOperationException(
                    $"EnumAdapterByLuid(0x{layout.AdapterLuid:X16}) failed (hr=0x{hr:X8}).");
            }

            d3d12 = D3D12.GetApi();

            // Silk.NET has a CreateDevice<T>(adapter, level, out ComPtr<T>) overload that
            // binds to D3D12's feature-level probe (ppDevice=null) and silently returns
            // S_OK with a null device — matching what we saw in the field on the RX 9060 XT
            // (hr=0x00000000, _device.Handle==null). Use the explicit IID + void** form
            // that the queue/fence creation below already uses so we get the real device.
            Guid deviceIid = ID3D12Device.Guid;
            void* rawDevice;
            hr = d3d12.CreateDevice((IUnknown*)adapter.Handle, D3DFeatureLevel.Level110, &deviceIid, &rawDevice);
            if (hr < 0 || rawDevice == null)
            {
                throw new InvalidOperationException(
                    $"D3D12CreateDevice at FEATURE_LEVEL_11_0 failed (hr=0x{hr:X8}, device={(rawDevice == null ? "null" : "set")}).");
            }
            _device = new ComPtr<ID3D12Device>((ID3D12Device*)rawDevice);

            CreateComputeQueue();
            CreateFence();

            ResolvedAdapterLuid = layout.AdapterLuid;
            IsAvailable = true;

            _logger?.LogInformation(
                "SharedD3D12Device: attached to adapter '{Description}' (LUID=0x{Luid:X16}) via IVramLayout — no second DXGI enumeration performed",
                layout.AdapterDescription, layout.AdapterLuid);
        }
        finally
        {
            adapter.Dispose();
            factory.Dispose();
            // d3d12 / dxgi are stateless API wrappers; disposing releases nothing that outlives the device.
            d3d12?.Dispose();
            dxgi?.Dispose();
        }
    }

    private unsafe void CreateComputeQueue()
    {
        var desc = new CommandQueueDesc
        {
            Type = CommandListType.Compute,
            Priority = (int)CommandQueuePriority.Normal,
            Flags = CommandQueueFlags.None,
            NodeMask = 0,
        };

        Guid iid = ID3D12CommandQueue.Guid;
        void* raw;
        int hr = _device.CreateCommandQueue(&desc, &iid, &raw);
        if (hr < 0 || raw == null)
        {
            throw new InvalidOperationException($"CreateCommandQueue(Compute) failed (hr=0x{hr:X8}).");
        }

        _computeQueue = new ComPtr<ID3D12CommandQueue>((ID3D12CommandQueue*)raw);
    }

    private unsafe void CreateFence()
    {
        Guid iid = ID3D12Fence.Guid;
        void* raw;
        int hr = _device.CreateFence(0UL, FenceFlags.None, &iid, &raw);
        if (hr < 0 || raw == null)
        {
            throw new InvalidOperationException($"CreateFence failed (hr=0x{hr:X8}).");
        }

        _fence = new ComPtr<ID3D12Fence>((ID3D12Fence*)raw);
    }

    private void ReleaseHandles()
    {
        // Reverse creation order: fence, queue, device.
        _fence.Dispose();
        _fence = default;
        _computeQueue.Dispose();
        _computeQueue = default;
        _device.Dispose();
        _device = default;
    }

    private void ThrowIfUnavailable()
    {
        if (!IsAvailable)
        {
            throw new InvalidOperationException(
                "SharedD3D12Device is unavailable — check IsAvailable before accessing the device.");
        }
    }
}
