// <copyright file="DispatchWatchdog.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Ouroboros.Tensor.Orchestration;

/// <summary>
/// Per-tenant watchdog that tracks dispatch-time overruns against
/// <see cref="GpuTenantProfile.MaxDispatchTime"/>.
/// </summary>
/// <remarks>
/// <para>
/// First overrun is logged as a warning. Three overruns inside a rolling 60-second
/// window trigger a single-tier <see cref="GpuTenantProfile.BasePriority"/> demotion
/// that lasts for five minutes and then auto-restores.
/// </para>
/// <para>
/// Realtime tenants are protected from multi-tier demotion (they demote to Normal only).
/// Idle tenants cannot be demoted further; a distinct warning is logged instead.
/// </para>
/// </remarks>
public sealed class DispatchWatchdog : IDisposable
{
    private readonly GpuSchedulerV2 _scheduler;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, Queue<DateTimeOffset>> _overrunTimestamps = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, (GpuPriorityClass originalBase, DateTimeOffset demotedUntil)> _demoted = new(StringComparer.Ordinal);
    private readonly ITimer _restoreTimer;

    /// <summary>
    /// Initializes a new <see cref="DispatchWatchdog"/>.
    /// </summary>
    /// <param name="scheduler">Scheduler whose tenants are watched.</param>
    /// <param name="timeProvider">Time provider (usually <see cref="TimeProvider.System"/>).</param>
    /// <param name="logger">Logger for overrun and demotion events.</param>
    public DispatchWatchdog(GpuSchedulerV2 scheduler, TimeProvider timeProvider, ILogger logger)
    {
        _scheduler = scheduler;
        _timeProvider = timeProvider;
        _logger = logger;
        _restoreTimer = timeProvider.CreateTimer(OnRestoreTick, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
    }

    /// <summary>
    /// Records the elapsed wall time of a completed dispatch and evaluates demotion rules.
    /// </summary>
    /// <param name="tenantName">Tenant that executed the work.</param>
    /// <param name="elapsed">Wall time of the dispatch.</param>
    public void RecordDispatch(string tenantName, TimeSpan elapsed)
    {
        var profile = _scheduler.GetTenantProfile(tenantName);
        if (profile is null)
        {
            return;
        }

        var now = _timeProvider.GetUtcNow();
        var window = _overrunTimestamps.GetOrAdd(tenantName, _ => new Queue<DateTimeOffset>());

        lock (window)
        {
            // Prune stale entries (> 60 s old).
            while (window.Count > 0 && now - window.Peek() > TimeSpan.FromSeconds(60))
            {
                window.Dequeue();
            }

            if (elapsed <= profile.MaxDispatchTime)
            {
                return;
            }

            // Overrun — log first occurrence and every recurrence.
            _logger.LogWarning(
                "[Watchdog] {Tenant} dispatch overran: elapsed={ElapsedMs}ms max={MaxMs}ms",
                tenantName,
                elapsed.TotalMilliseconds,
                profile.MaxDispatchTime.TotalMilliseconds);

            window.Enqueue(now);

            if (window.Count >= 3 && !_demoted.ContainsKey(tenantName))
            {
                var currentBase = profile.BasePriority;
                var newBase = currentBase switch
                {
                    GpuPriorityClass.Realtime => GpuPriorityClass.Normal,
                    GpuPriorityClass.Normal => GpuPriorityClass.Background,
                    GpuPriorityClass.Background => GpuPriorityClass.Idle,
                    GpuPriorityClass.Idle => GpuPriorityClass.Idle,
                    _ => GpuPriorityClass.Idle,
                };

                if (currentBase == GpuPriorityClass.Idle)
                {
                    _logger.LogWarning(
                        "[Watchdog] {Tenant} already at Idle priority; cannot demote further after {N} overruns",
                        tenantName,
                        window.Count);
                    return;
                }

                _scheduler.SetTenantBasePriority(tenantName, newBase);
                var demotedUntil = now.Add(TimeSpan.FromMinutes(5));
                _demoted[tenantName] = (currentBase, demotedUntil);

                _logger.LogWarning(
                    "[Watchdog] {Tenant} demoted {From} -> {To} until {Until} after {N} overruns",
                    tenantName,
                    currentBase,
                    newBase,
                    demotedUntil,
                    window.Count);
            }
        }
    }

    /// <summary>
    /// Disposes the restore timer.
    /// </summary>
    public void Dispose()
    {
        _restoreTimer.Dispose();
    }

    private void OnRestoreTick(object? state)
    {
        var now = _timeProvider.GetUtcNow();
        foreach (var kvp in _demoted.ToArray())
        {
            var tenantName = kvp.Key;
            var (originalBase, demotedUntil) = kvp.Value;
            if (now >= demotedUntil)
            {
                _scheduler.RestoreTenantBasePriority(tenantName);
                _demoted.TryRemove(tenantName, out _);
                _logger.LogWarning(
                    "[Watchdog] {Tenant} restored to {OriginalBase} after demotion cooldown",
                    tenantName,
                    originalBase);
            }
        }
    }
}
