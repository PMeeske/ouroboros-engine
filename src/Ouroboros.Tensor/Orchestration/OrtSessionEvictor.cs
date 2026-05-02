// <copyright file="OrtSessionEvictor.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Tensor.Rasterizers;

namespace Ouroboros.Tensor.Orchestration;

/// <summary>
/// Cooperative evictor for ONNX Runtime DirectML sessions.
/// </summary>
/// <remarks>
/// <para>
/// Phase 261 (GPU-01 item 4) — eviction strategy is now tier-2 heap demotion when
/// the Phase 196.3 <see cref="SharedD3D12Device"/> is available, else legacy
/// <see cref="IDisposable.Dispose"/> on the session. Tier-2 demotion keeps the ORT
/// session live, lets D3D12 page heaps to system memory under pressure, and reloads
/// transparently on the next dispatch (≈100 ms reload vs ≈2 s for a full session
/// rebuild). This matches the <c>HardHeap</c> eviction policy contract documented in
/// CLAUDE.md (relies on D3D12 heap tier-2 demotion under pressure).
/// </para>
/// <para>
/// The C# <see cref="Microsoft.ML.OnnxRuntime.InferenceSession"/> binding does not
/// surface the underlying <c>IDMLDevice*</c> / <c>ID3D12Heap*</c> handles
/// (onnxruntime#9164 / #4941), so we cannot enumerate the session's pageable
/// resources to call <c>ID3D12Device.Evict</c> on them directly. Instead we rely
/// on D3D12's per-process LRU heap tier-2 demotion which kicks in automatically
/// when overcommit pressure rises — by *not* disposing the session, we let DML's
/// internal heaps flow through that mechanism. The reported reclaim is the
/// declared VRAM footprint because the demotion is deferred and asynchronous;
/// the scheduler treats it as advisory.
/// </para>
/// </remarks>
public sealed class OrtSessionEvictor : IEvictionPolicy
{
    private readonly IDisposable? _session;
    private readonly long _vramBytes;
    private readonly SharedD3D12Device? _sharedDevice;

    /// <inheritdoc/>
    public string TenantName { get; }

    /// <inheritdoc/>
    public TimeSpan EstimatedEvictionLatency => TimeSpan.FromMilliseconds(50);

    /// <inheritdoc/>
    public TimeSpan EstimatedReloadLatency =>
        _sharedDevice?.IsAvailable == true ? TimeSpan.FromMilliseconds(100) : TimeSpan.FromSeconds(2);

    /// <summary>
    /// Initializes a new <see cref="OrtSessionEvictor"/> with legacy FullUnload semantics
    /// (session disposed on eviction). Used when no <see cref="SharedD3D12Device"/> is
    /// available — e.g. test contexts or the CPU baseline.
    /// </summary>
    /// <param name="tenantName">Tenant name.</param>
    /// <param name="session">Session to dispose on eviction. May be <see langword="null"/> before the session is lazily created.</param>
    /// <param name="vramBytes">Declared VRAM footprint in bytes.</param>
    public OrtSessionEvictor(string tenantName, IDisposable? session, long vramBytes)
        : this(tenantName, session, vramBytes, sharedDevice: null)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="OrtSessionEvictor"/> with tier-2 heap demotion when
    /// <paramref name="sharedDevice"/> is available (Phase 196.3 shared D3D12 device
    /// is now live — Phase 261 GPU-01 item 4 swap). Falls back to FullUnload semantics
    /// if the shared device is null or unavailable.
    /// </summary>
    /// <param name="tenantName">Tenant name.</param>
    /// <param name="session">Session held resident under tier-2 demotion. May be <see langword="null"/>.</param>
    /// <param name="vramBytes">Declared VRAM footprint in bytes.</param>
    /// <param name="sharedDevice">Phase 196.3 shared D3D12 device for tier-2 heap demotion. <see langword="null"/> selects legacy FullUnload.</param>
    public OrtSessionEvictor(string tenantName, IDisposable? session, long vramBytes, SharedD3D12Device? sharedDevice)
    {
        TenantName = tenantName ?? throw new ArgumentNullException(nameof(tenantName));
        _session = session;
        _vramBytes = vramBytes;
        _sharedDevice = sharedDevice;
    }

    /// <inheritdoc/>
    public bool CanEvictNow() => _session is not null;

    /// <inheritdoc/>
    public Task<long> EvictAsync(CancellationToken cancellationToken = default)
    {
        if (_session is null)
        {
            return Task.FromResult(0L);
        }

        if (_sharedDevice?.IsAvailable == true)
        {
            // Tier-2 heap demotion path (Phase 261 GPU-01 item 4): the shared D3D12
            // device's per-process LRU will demote DML's session-owned heaps to
            // system memory under overcommit pressure. We deliberately do *not*
            // dispose the session — reload latency is ~100 ms (re-page on next
            // dispatch) instead of ~2 s (rebuild from disk). The reclaim figure
            // is advisory: the demotion is deferred to D3D12's pager.
            return Task.FromResult(_vramBytes);
        }

        // Legacy FullUnload fallback when no shared device is available.
        _session.Dispose();
        return Task.FromResult(_vramBytes);
    }
}
