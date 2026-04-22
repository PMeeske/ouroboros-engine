// <copyright file="EvictionCoordinator.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Orchestration;

/// <summary>
/// Orchestrates cooperative eviction across registered tenants. Victims are selected by
/// priority (lowest first) then LRU within the same priority class. A hysteresis window
/// (2x reload latency) prevents immediate re-admission of a freshly-evicted tenant.
/// </summary>
public sealed class EvictionCoordinator
{
    private readonly ConcurrentDictionary<string, IEvictionPolicy> _policies = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastUsedAt = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastEvictedAt = new(StringComparer.Ordinal);

    /// <summary>
    /// Registers an eviction policy for a tenant.
    /// </summary>
    /// <param name="policy">The policy to register.</param>
    public void RegisterPolicy(IEvictionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        _policies[policy.TenantName] = policy;
    }

    /// <summary>
    /// Unregisters the eviction policy for a tenant. Idempotent — safe to call even if
    /// the tenant was never registered.
    /// </summary>
    /// <param name="tenantName">Tenant to unregister.</param>
    public void UnregisterPolicy(string tenantName)
    {
        _policies.TryRemove(tenantName, out _);
        _lastUsedAt.TryRemove(tenantName, out _);
        _lastEvictedAt.TryRemove(tenantName, out _);
    }

    /// <summary>
    /// Records that a tenant has recently been dispatched. Used for LRU ordering within
    /// a priority class when picking eviction victims.
    /// </summary>
    /// <param name="tenantName">Tenant that was just scheduled.</param>
    public void RecordUsage(string tenantName)
    {
        _lastUsedAt[tenantName] = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Attempts to evict tenants until at least <paramref name="requiredBytes"/> have been
    /// reclaimed. Victims are chosen lowest-priority-first, then LRU within that priority.
    /// </summary>
    /// <param name="requiredBytes">Bytes that must be freed.</param>
    /// <param name="getPriority">Callback that resolves the current effective priority of a tenant.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Total bytes actually reclaimed (may be less than <paramref name="requiredBytes"/> if no more victims are available).</returns>
    public async Task<long> EvictUntilFitsAsync(
        long requiredBytes,
        Func<string, GpuPriorityClass?> getPriority,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(getPriority);

        if (requiredBytes <= 0)
        {
            return 0L;
        }

        var reclaimed = 0L;

        while (reclaimed < requiredBytes)
        {
            var victim = PickVictim(getPriority);
            if (victim is null)
            {
                break;
            }

            var policy = _policies[victim];
            var bytes = await policy.EvictAsync(cancellationToken).ConfigureAwait(false);
            reclaimed += bytes;
            _lastEvictedAt[victim] = DateTimeOffset.UtcNow;
        }

        return reclaimed;
    }

    private string? PickVictim(Func<string, GpuPriorityClass?> getPriority)
    {
        var now = DateTimeOffset.UtcNow;

        var candidates = new List<(string Tenant, GpuPriorityClass Priority, DateTimeOffset LastUsed, IEvictionPolicy Policy)>();

        foreach (var kv in _policies)
        {
            var policy = kv.Value;

            // Never evict tenants that explicitly forbid it or are not ready.
            if (!policy.CanEvictNow())
            {
                continue;
            }

            // Hysteresis: lockout period is 2x the estimated reload latency.
            if (_lastEvictedAt.TryGetValue(kv.Key, out var lastEvicted))
            {
                var lockout = TimeSpan.FromTicks(policy.EstimatedReloadLatency.Ticks * 2);
                if (now - lastEvicted < lockout)
                {
                    continue;
                }
            }

            var priority = getPriority(kv.Key) ?? GpuPriorityClass.Idle;
            var lastUsed = _lastUsedAt.TryGetValue(kv.Key, out var lu) ? lu : DateTimeOffset.MinValue;
            candidates.Add((kv.Key, priority, lastUsed, policy));
        }

        // Sort by priority ascending (lowest first), then by LRU (oldest first).
        var ordered = candidates
            .OrderBy(c => c.Priority)
            .ThenBy(c => c.LastUsed)
            .ToList();

        return ordered.FirstOrDefault().Tenant;
    }
}
