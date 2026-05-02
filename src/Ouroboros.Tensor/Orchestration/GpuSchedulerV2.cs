// <copyright file="GpuSchedulerV2.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;

namespace Ouroboros.Tensor.Orchestration;

/// <summary>
/// FreeRTOS-style priority-preemptive GPU scheduler. Maintains one ready-queue per
/// <see cref="GpuPriorityClass"/> and a per-class round-robin cursor so equal-priority
/// tenants cannot monopolise the device.
/// </summary>
/// <remarks>
/// <para>
/// Dispatch loop parks on a <see cref="SemaphoreSlim"/> when every queue is empty
/// (tickless idle seed — plan 04 extends this with watchdog hooks).
/// </para>
/// <para>
/// Preemption granularity is between work items; a Realtime submission dispatches before
/// any queued Background item but cannot interrupt a work delegate that is already
/// running. This matches the <c>Preemptible = false</c> contract surfaced in
/// <see cref="GpuTenantProfile"/>.
/// </para>
/// </remarks>
public sealed class GpuSchedulerV2 : IGpuScheduler
{
    private static readonly GpuPriorityClass[] PriorityOrderHighToLow =
    {
        GpuPriorityClass.Realtime,
        GpuPriorityClass.Normal,
        GpuPriorityClass.Perception,
        GpuPriorityClass.Background,
        GpuPriorityClass.Idle,
    };

    private readonly long _totalVramBytes;
    private readonly ConcurrentDictionary<string, GpuTenantProfile> _tenants = new(StringComparer.Ordinal);

    // Per-priority-class dispatch state. Access is serialised under _classLock.
    private readonly object _classLock = new();

    // Plan 02: protects queue migration between priority classes so that
    // BoostTenantPriority / RestoreTenantPriority are atomic w.r.t. dispatch.
    private readonly object _queueMigrationLock = new();

    // Per-tenant FIFO queue of pending work.
    private readonly Dictionary<string, Queue<PendingWork>> _perTenantQueues = new(StringComparer.Ordinal);

    // Round-robin rotation order per priority class: a linked list of tenant names.
    // Front of the list is the next tenant to consider; after dispatch we rotate it to the back.
    private readonly Dictionary<GpuPriorityClass, LinkedList<string>> _rrOrder = new()
    {
        [GpuPriorityClass.Realtime] = new LinkedList<string>(),
        [GpuPriorityClass.Normal] = new LinkedList<string>(),
        [GpuPriorityClass.Perception] = new LinkedList<string>(),
        [GpuPriorityClass.Background] = new LinkedList<string>(),
        [GpuPriorityClass.Idle] = new LinkedList<string>(),
    };

    private readonly SemaphoreSlim _signal = new(0, int.MaxValue);
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly Task _dispatchTask;

    private readonly Meter _meter;
    private readonly Counter<long> _tasksCompletedCounter;
    private readonly Counter<long> _tasksFailedCounter;
    private readonly Histogram<double> _latencyHistogram;

    private long _completedCount;
    private long _failedCount;
    private long _estimatedUsedVram = 0;
    private int _queueDepth;
    private TimeSpan _lastLatency;
    private volatile bool _disposed;
    private string? _runningTenant;

    private readonly EvictionCoordinator _evictionCoordinator = new();
    private readonly DispatchWatchdog? _watchdog;
    private readonly ILogger? _logger;
    private readonly ConcurrentDictionary<string, GpuPriorityClass> _originalBasePriorities = new(StringComparer.Ordinal);

    // Phase 261 GPU-01 item 3: weak refs to every named GpuResourceHolder so
    // UnregisterCore can sweep them for the unregistering tenant. Weak refs
    // avoid keeping holders alive past their natural lifetime — the scheduler
    // does not own them.
    private readonly object _holderRegistryLock = new();
    private readonly List<WeakReference<GpuResourceHolder>> _resourceHolders = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="GpuSchedulerV2"/> class.
    /// Initializes a new <see cref="GpuSchedulerV2"/>.
    /// </summary>
    /// <param name="totalVramBytes">Total VRAM in bytes (used for metrics reporting only in plan 01).</param>
    /// <param name="meter">Optional meter for OpenTelemetry integration.</param>
    /// <param name="timeProvider">Optional time provider for watchdog/testability; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="logger">Optional logger for watchdog events; watchdog is disabled when null.</param>
    public GpuSchedulerV2(long totalVramBytes, Meter? meter = null, TimeProvider? timeProvider = null, ILogger? logger = null)
    {
        _totalVramBytes = totalVramBytes;
        _meter = meter ?? new Meter("Ouroboros.Tensor.GpuSchedulerV2");
        _tasksCompletedCounter = _meter.CreateCounter<long>("gpu.tasks.completed");
        _tasksFailedCounter = _meter.CreateCounter<long>("gpu.tasks.failed");
        _latencyHistogram = _meter.CreateHistogram<double>("gpu.task.latency_ms");

        var tp = timeProvider ?? TimeProvider.System;
        _logger = logger;
        if (logger is not null)
        {
            _watchdog = new DispatchWatchdog(this, tp, logger);
        }

        _dispatchTask = Task.Run(() => RunDispatchLoopAsync(_shutdownCts.Token));
    }

    /// <summary>Gets the dispatch loop task (exposed for deterministic shutdown verification).</summary>
    public Task DispatchTask => _dispatchTask;

    /// <inheritdoc/>
    public GpuSchedulerMetrics CurrentMetrics => new(
        Volatile.Read(ref _queueDepth),
        Interlocked.Read(ref _estimatedUsedVram),
        _totalVramBytes,
        _totalVramBytes == 0 ? 0.0 : (double)Interlocked.Read(ref _estimatedUsedVram) / _totalVramBytes,
        _lastLatency,
        Interlocked.Read(ref _completedCount),
        Interlocked.Read(ref _failedCount));

    /// <inheritdoc/>
    public void RegisterEvictionPolicy(IEvictionPolicy policy)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(policy);
        _evictionCoordinator.RegisterPolicy(policy);
    }

    /// <inheritdoc/>
    public IDisposable RegisterTenant(GpuTenantProfile profile)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(profile);

        if (!_tenants.TryAdd(profile.TenantName, profile))
        {
            throw new InvalidOperationException(
                $"Tenant '{profile.TenantName}' is already registered.");
        }

        lock (_classLock)
        {
            _perTenantQueues[profile.TenantName] = new Queue<PendingWork>();
            _rrOrder[profile.BasePriority].AddLast(profile.TenantName);
        }

        return new Registration(this, profile.TenantName);
    }

    /// <inheritdoc/>
    public async Task<T> ScheduleAsync<T>(
        string tenantName,
        Func<CancellationToken, Task<T>> work,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(tenantName);
        ArgumentNullException.ThrowIfNull(work);

        if (!_tenants.TryGetValue(tenantName, out _))
        {
            throw new InvalidOperationException(
                $"Tenant '{tenantName}' is not registered with the scheduler.");
        }

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task RunBoxed(CancellationToken ct)
        {
            // Honor cancellation observed before dispatch — the dispatch loop gates
            // on this to avoid running work whose caller has already given up.
            if (ct.IsCancellationRequested)
            {
                tcs.TrySetCanceled(ct);
                return;
            }

            try
            {
                var result = await work(ct).ConfigureAwait(false);
                tcs.TrySetResult(result);
            }
            catch (OperationCanceledException oce)
            {
                tcs.TrySetCanceled(oce.CancellationToken);
                throw;
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
                throw;
            }
        }

        var pending = new PendingWork(tenantName, RunBoxed, cancellationToken);

        lock (_classLock)
        {
            // Re-check registration under lock in case of concurrent dispose.
            if (!_tenants.TryGetValue(tenantName, out _) ||
                !_perTenantQueues.TryGetValue(tenantName, out var q))
            {
                throw new InvalidOperationException(
                    $"Tenant '{tenantName}' was unregistered before work could be enqueued.");
            }

            q.Enqueue(pending);
            Interlocked.Increment(ref _queueDepth);
        }

        _signal.Release();

        // Link the caller's await to their CT so they give up waiting when cancelled
        // even if the work remains queued behind a long-running tenant. RunBoxed still
        // observes the same ct on dispatch and short-circuits to TrySetCanceled.
        using var registration = cancellationToken.Register(
            static state =>
            {
                var (typedTcs, ct) = ((TaskCompletionSource<T>, CancellationToken))state!;
                typedTcs.TrySetCanceled(ct);
            },
            (tcs, cancellationToken));

        return await tcs.Task.ConfigureAwait(false);
    }

    /// <summary>
    /// Diagnostic hook: returns the current state of the tenant's most recently dispatched
    /// work, or <see cref="GpuTaskState.Ready"/> if the tenant has queued but un-dispatched
    /// work, or <see cref="GpuTaskState.Done"/> if nothing is queued or running.
    /// </summary>
    /// <param name="tenantName">Tenant to inspect.</param>
    /// <returns>Observed task state.</returns>
    public GpuTaskState GetTenantState(string tenantName)
    {
        lock (_classLock)
        {
            if (_runningTenant == tenantName)
            {
                return GpuTaskState.Running;
            }

            if (_perTenantQueues.TryGetValue(tenantName, out var q) && q.Count > 0)
            {
                return GpuTaskState.Ready;
            }

            return GpuTaskState.Done;
        }
    }

    /// <summary>
    /// Returns a snapshot of every registered tenant's current state.
    /// Suitable for CLI <c>status</c> rendering and telemetry dashboards.
    /// </summary>
    /// <returns>Immutable list of tenant snapshots.</returns>
    public IReadOnlyList<GpuTenantSnapshot> GetTenantSnapshots()
    {
        var snapshots = new List<GpuTenantSnapshot>(_tenants.Count);

        lock (_classLock)
        {
            foreach (var kvp in _tenants)
            {
                var profile = kvp.Value;
                var state = GetTenantState(kvp.Key);
                int queueDepth = _perTenantQueues.TryGetValue(kvp.Key, out var q) ? q.Count : 0;

                snapshots.Add(new GpuTenantSnapshot(
                    profile.TenantName,
                    profile.BasePriority,
                    profile.EffectivePriority,
                    state,
                    profile.VramBytes,
                    queueDepth,
                    profile.Eviction));
            }
        }

        return snapshots;
    }

    /// <summary>
    /// Returns the registered profile for a tenant, or <see langword="null"/> if the tenant
    /// is not registered.
    /// </summary>
    /// <param name="tenantName">Tenant to look up.</param>
    /// <returns>The current <see cref="GpuTenantProfile"/>.</returns>
    public GpuTenantProfile? GetTenantProfile(string tenantName)
    {
        _tenants.TryGetValue(tenantName, out var profile);
        return profile;
    }

    /// <summary>
    /// Temporarily raises a tenant's <see cref="GpuTenantProfile.EffectivePriority"/> to
    /// <paramref name="targetPriority"/> and migrates its dispatch queue accordingly.
    /// </summary>
    /// <param name="tenantName">Tenant to boost.</param>
    /// <param name="targetPriority">Priority to inherit.</param>
    public void BoostTenantPriority(string tenantName, GpuPriorityClass targetPriority)
    {
        lock (_queueMigrationLock)
        {
            lock (_classLock)
            {
                if (!_tenants.TryGetValue(tenantName, out var profile))
                {
                    return;
                }

                var currentEffective = profile.EffectivePriority;
                if (currentEffective == targetPriority)
                {
                    return;
                }

                var updated = profile.WithEffectivePriority(targetPriority);
                _tenants[tenantName] = updated;

                if (_rrOrder.TryGetValue(currentEffective, out var oldList))
                {
                    oldList.Remove(tenantName);
                }

                if (_rrOrder.TryGetValue(targetPriority, out var newList))
                {
                    newList.AddLast(tenantName);
                }
            }
        }
    }

    /// <summary>
    /// Restores a tenant's <see cref="GpuTenantProfile.EffectivePriority"/> to its
    /// <see cref="GpuTenantProfile.BasePriority"/> and migrates its dispatch queue back.
    /// </summary>
    /// <param name="tenantName">Tenant to restore.</param>
    public void RestoreTenantPriority(string tenantName)
    {
        lock (_queueMigrationLock)
        {
            lock (_classLock)
            {
                if (!_tenants.TryGetValue(tenantName, out var profile))
                {
                    return;
                }

                var currentEffective = profile.EffectivePriority;
                var basePriority = profile.BasePriority;

                if (currentEffective == basePriority)
                {
                    return;
                }

                var updated = profile.WithEffectivePriority(basePriority);
                _tenants[tenantName] = updated;

                if (_rrOrder.TryGetValue(currentEffective, out var oldList))
                {
                    oldList.Remove(tenantName);
                }

                if (_rrOrder.TryGetValue(basePriority, out var newList))
                {
                    newList.AddLast(tenantName);
                }
            }
        }
    }

    /// <summary>
    /// Demotes a tenant's <see cref="GpuTenantProfile.BasePriority"/> to <paramref name="newBase"/>
    /// and migrates its dispatch queue atomically. The original base priority is remembered so
    /// that <see cref="RestoreTenantBasePriority"/> can revert the change.
    /// </summary>
    /// <param name="tenantName">Tenant to demote.</param>
    /// <param name="newBase">New base priority class.</param>
    internal void SetTenantBasePriority(string tenantName, GpuPriorityClass newBase)
    {
        lock (_queueMigrationLock)
        {
            lock (_classLock)
            {
                if (!_tenants.TryGetValue(tenantName, out var profile))
                {
                    return;
                }

                var currentBase = profile.BasePriority;
                if (currentBase == newBase)
                {
                    return;
                }

                _originalBasePriorities.TryAdd(tenantName, currentBase);

                var updated = profile with { BasePriority = newBase };
                if (profile.EffectivePriority == currentBase)
                {
                    updated = updated with { EffectivePriority = newBase };
                }

                _tenants[tenantName] = updated;

                if (_rrOrder.TryGetValue(currentBase, out var oldList))
                {
                    oldList.Remove(tenantName);
                }

                if (_rrOrder.TryGetValue(newBase, out var newList))
                {
                    newList.AddLast(tenantName);
                }
            }
        }

        _signal.Release();
    }

    /// <summary>
    /// Restores a tenant's <see cref="GpuTenantProfile.BasePriority"/> to the value saved by
    /// the most recent <see cref="SetTenantBasePriority"/> call, then removes the saved value.
    /// </summary>
    /// <param name="tenantName">Tenant to restore.</param>
    internal void RestoreTenantBasePriority(string tenantName)
    {
        lock (_queueMigrationLock)
        {
            lock (_classLock)
            {
                if (!_tenants.TryGetValue(tenantName, out var profile))
                {
                    _originalBasePriorities.TryRemove(tenantName, out _);
                    return;
                }

                if (!_originalBasePriorities.TryRemove(tenantName, out var originalBase))
                {
                    return;
                }

                var currentBase = profile.BasePriority;
                if (currentBase == originalBase)
                {
                    return;
                }

                var updated = profile with { BasePriority = originalBase };
                if (profile.EffectivePriority == currentBase)
                {
                    updated = updated with { EffectivePriority = originalBase };
                }

                _tenants[tenantName] = updated;

                if (_rrOrder.TryGetValue(currentBase, out var oldList))
                {
                    oldList.Remove(tenantName);
                }

                if (_rrOrder.TryGetValue(originalBase, out var newList))
                {
                    newList.AddLast(tenantName);
                }
            }
        }

        _signal.Release();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _shutdownCts.Cancel();
        _signal.Release();

        try
        {
            _dispatchTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException)
        {
            // Expected during cancellation.
        }

        _watchdog?.Dispose();
        _signal.Dispose();
        _shutdownCts.Dispose();
        _meter.Dispose();
    }

    private async Task RunDispatchLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await _signal.WaitAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            // Drain every ready work item currently visible. Fire-and-forget execution:
            // the dispatcher is a SCHEDULER, not a serialiser. Awaiting ExecuteAsync here
            // would block the dispatch thread for the full duration of whatever task is
            // currently executing (an LLM call can take 30+ seconds), starving every
            // other queued tenant including Realtime rasterizer frames — observed as a
            // combined avatar + chat freeze on 2026-04-19 when GpuSchedulerV2 first
            // landed. Caller completion is signalled via the tcs in RunBoxed (see
            // ScheduleAsync); failures are isolated inside ExecuteAsync. Priority
            // ordering (Realtime drains before Normal) is preserved because
            // TryDequeueHighestPriority picks the next task by class order.
            //
            // Plan 04 extends this with the per-tenant watchdog + preemption points;
            // plan 03 adds cooperative eviction on VRAM pressure.
            while (!ct.IsCancellationRequested && TryDequeueHighestPriority(out var work))
            {
                _ = ExecuteAsync(work);
            }
        }
    }

    private bool TryDequeueHighestPriority(out PendingWork work)
    {
        lock (_queueMigrationLock)
        {
            lock (_classLock)
            {
                foreach (var cls in PriorityOrderHighToLow)
                {
                    var order = _rrOrder[cls];
                    if (order.Count == 0)
                    {
                        continue;
                    }

                    // Scan the rotation order for the next tenant with queued work.
                    // Stop after one full lap so we don't spin.
                    var inspected = 0;
                    var total = order.Count;
                    while (inspected < total)
                    {
                        var node = order.First;
                        if (node is null)
                        {
                            break;
                        }

                        var tenantName = node.Value;

                        // Rotate: move this tenant to the back regardless of whether we dispatch it,
                        // so the next lookup starts with a different tenant (fair round-robin).
                        order.RemoveFirst();
                        order.AddLast(tenantName);
                        inspected++;

                        if (_perTenantQueues.TryGetValue(tenantName, out var q) && q.Count > 0)
                        {
                            work = q.Dequeue();
                            Interlocked.Decrement(ref _queueDepth);
                            _runningTenant = tenantName;
                            _evictionCoordinator.RecordUsage(tenantName);
                            return true;
                        }
                    }
                }

                work = default!;
                return false;
            }
        }
    }

    private async Task ExecuteAsync(PendingWork work)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            // RunBoxed honors cancellation internally (and completes the caller's tcs);
            // we always invoke it so the caller's await never hangs.
            await work.Run(work.Ct).ConfigureAwait(false);

            sw.Stop();
            _lastLatency = sw.Elapsed;
            _latencyHistogram.Record(sw.Elapsed.TotalMilliseconds);
            _tasksCompletedCounter.Add(1);
            Interlocked.Increment(ref _completedCount);
        }
#pragma warning disable CA1031 // Dispatch loop must isolate tenant faults: the exception
        // has already been propagated to the caller via the tcs closure in ScheduleAsync;
        // if we let it escape here, one misbehaving tenant would tear down the scheduler
        // thread and starve every other tenant.
        catch (Exception)
        {
            _tasksFailedCounter.Add(1);
            Interlocked.Increment(ref _failedCount);
        }
#pragma warning restore CA1031
        finally
        {
            sw.Stop();
            _watchdog?.RecordDispatch(work.TenantName, sw.Elapsed);
            lock (_classLock)
            {
                if (_runningTenant == work.TenantName)
                {
                    _runningTenant = null;
                }
            }
        }
    }

    /// <summary>
    /// Registers a <see cref="GpuResourceHolder"/> with the scheduler so that
    /// <see cref="UnregisterCore"/> can sweep it when a tenant unregisters
    /// (Phase 261 GPU-01 item 3 — prevents leaked holders / stranded waiters
    /// when a tenant's lifetime ends mid-flight). Held by weak reference so
    /// the registry never extends a holder's lifetime.
    /// </summary>
    /// <param name="holder">Holder to track.</param>
    internal void RegisterResourceHolder(GpuResourceHolder holder)
    {
        ArgumentNullException.ThrowIfNull(holder);

        lock (_holderRegistryLock)
        {
            // Compact dead refs opportunistically so the list does not grow unbounded.
            for (int i = _resourceHolders.Count - 1; i >= 0; i--)
            {
                if (!_resourceHolders[i].TryGetTarget(out _))
                {
                    _resourceHolders.RemoveAt(i);
                }
            }

            _resourceHolders.Add(new WeakReference<GpuResourceHolder>(holder));
        }
    }

    private void UnregisterCore(string tenantName)
    {
        if (!_tenants.TryRemove(tenantName, out var profile))
        {
            return;
        }

        lock (_classLock)
        {
            _perTenantQueues.Remove(tenantName);
            _rrOrder[profile.BasePriority].Remove(tenantName);
        }

        _evictionCoordinator.UnregisterPolicy(tenantName);

        // Phase 261 GPU-01 item 3: sweep every live GpuResourceHolder and force-release
        // anything still tied to this tenant. Without this, a tenant that disposes its
        // registration while still holding a named lock leaves the holder permanently
        // stuck — every future acquire blocks waiting for a tenant that can never call
        // ReleaseAsync because it was already disposed.
        lock (_holderRegistryLock)
        {
            for (int i = _resourceHolders.Count - 1; i >= 0; i--)
            {
                if (!_resourceHolders[i].TryGetTarget(out var holder))
                {
                    _resourceHolders.RemoveAt(i);
                    continue;
                }

                if (holder.ReleaseIfHeldBy(tenantName))
                {
                    _logger?.LogWarning(
                        "[GpuScheduler] Auto-released resource '{Resource}' on tenant unregister: {Tenant}",
                        holder.ResourceName,
                        tenantName);
                }
            }
        }
    }

    private readonly record struct PendingWork(
        string TenantName,
        Func<CancellationToken, Task> Run,
        CancellationToken Ct);

    private sealed class Registration : IDisposable
    {
        private readonly GpuSchedulerV2 _owner;
        private readonly string _tenantName;
        private int _disposed;

        public Registration(GpuSchedulerV2 owner, string tenantName)
        {
            _owner = owner;
            _tenantName = tenantName;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _owner.UnregisterCore(_tenantName);
            }
        }
    }
}
