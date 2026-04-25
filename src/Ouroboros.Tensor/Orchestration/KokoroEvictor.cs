// <copyright file="KokoroEvictor.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Orchestration;

/// <summary>
/// Cooperative evictor for the Kokoro TTS pipeline. Behaviour is analogous to
/// <see cref="OrtSessionEvictor"/> but carries Kokoro-specific latency constants.
/// </summary>
public sealed class KokoroEvictor : IEvictionPolicy
{
    private readonly IDisposable? _session;
    private readonly long _vramBytes;

    /// <inheritdoc/>
    public string TenantName { get; }

    /// <inheritdoc/>
    public TimeSpan EstimatedEvictionLatency => TimeSpan.FromMilliseconds(50);

    /// <inheritdoc/>
    public TimeSpan EstimatedReloadLatency => TimeSpan.FromSeconds(1.5);

    /// <summary>
    /// Initializes a new instance of the <see cref="KokoroEvictor"/> class.
    /// Initializes a new <see cref="KokoroEvictor"/>.
    /// </summary>
    /// <param name="tenantName">Tenant name.</param>
    /// <param name="session">Kokoro session to dispose on eviction.</param>
    /// <param name="vramBytes">Declared VRAM footprint in bytes.</param>
    public KokoroEvictor(string tenantName, IDisposable? session, long vramBytes)
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
