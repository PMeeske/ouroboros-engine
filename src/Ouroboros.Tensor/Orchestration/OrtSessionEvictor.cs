// <copyright file="OrtSessionEvictor.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Orchestration;

/// <summary>
/// Cooperative evictor for ONNX Runtime DirectML sessions. Disposes the underlying
/// <see cref="IDisposable"/> session and reports the declared VRAM footprint.
/// </summary>
/// <remarks>
/// This is a fallback until Phase 196.3 tier-2 heap demotion lands; once tier-2 is
/// available the session can be kept resident in CPU-visible memory instead of fully
/// disposed.
/// </remarks>
public sealed class OrtSessionEvictor : IEvictionPolicy
{
    private readonly IDisposable? _session;
    private readonly long _vramBytes;

    /// <inheritdoc/>
    public string TenantName { get; }

    /// <inheritdoc/>
    public TimeSpan EstimatedEvictionLatency => TimeSpan.FromMilliseconds(50);

    /// <inheritdoc/>
    public TimeSpan EstimatedReloadLatency => TimeSpan.FromSeconds(2);

    /// <summary>
    /// Initializes a new <see cref="OrtSessionEvictor"/>.
    /// </summary>
    /// <param name="tenantName">Tenant name.</param>
    /// <param name="session">Session to dispose on eviction. May be <see langword="null"/> before the session is lazily created.</param>
    /// <param name="vramBytes">Declared VRAM footprint in bytes.</param>
    public OrtSessionEvictor(string tenantName, IDisposable? session, long vramBytes)
    {
        TenantName = tenantName ?? throw new ArgumentNullException(nameof(tenantName));
        _session = session;
        _vramBytes = vramBytes;
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

        _session.Dispose();
        return Task.FromResult(_vramBytes);
    }
}
