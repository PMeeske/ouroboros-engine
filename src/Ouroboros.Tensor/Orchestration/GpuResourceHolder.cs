// <copyright file="GpuResourceHolder.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Orchestration;

/// <summary>
/// Concrete implementation of <see cref="IGpuResourceLock"/> backed by the
/// <see cref="GpuSchedulerV2"/> priority inheritance hooks.
/// </summary>
public sealed class GpuResourceHolder : IGpuResourceLock
{
    private readonly GpuSchedulerV2 _scheduler;
    private readonly string _resourceName;
    private readonly object _lock = new();

    private string? _holder;
    private readonly List<Waiter> _waiters = new();

    private record Waiter(string TenantName, TaskCompletionSource<object?> Tcs, CancellationTokenRegistration CtReg);

    /// <summary>
    /// Initializes a new <see cref="GpuResourceHolder"/>.
    /// </summary>
    /// <param name="scheduler">Scheduler used for priority boost / restore.</param>
    /// <param name="resourceName">Resource name.</param>
    public GpuResourceHolder(GpuSchedulerV2 scheduler, string resourceName)
    {
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _resourceName = resourceName ?? throw new ArgumentNullException(nameof(resourceName));
    }

    /// <inheritdoc/>
    public string ResourceName => _resourceName;

    /// <inheritdoc/>
    public Task AcquireAsync(string tenantName, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_holder == tenantName)
            {
                // Idempotent acquire.
                return Task.CompletedTask;
            }

            if (_holder is null)
            {
                _holder = tenantName;
                return Task.CompletedTask;
            }

            var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            CancellationTokenRegistration reg = default;

            if (cancellationToken.CanBeCanceled)
            {
                reg = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                reg.Dispose();
                return Task.FromCanceled(cancellationToken);
            }

            _waiters.Add(new Waiter(tenantName, tcs, reg));

            var waiterProfile = _scheduler.GetTenantProfile(tenantName);
            var holderProfile = _scheduler.GetTenantProfile(_holder);

            if (waiterProfile is not null &&
                holderProfile is not null &&
                waiterProfile.EffectivePriority > holderProfile.EffectivePriority)
            {
                _scheduler.BoostTenantPriority(_holder, waiterProfile.EffectivePriority);
            }

            return tcs.Task;
        }
    }

    /// <inheritdoc/>
    public Task ReleaseAsync(string tenantName, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_holder != tenantName)
            {
                throw new InvalidOperationException(
                    $"Tenant '{tenantName}' does not hold resource '{_resourceName}'.");
            }

            _scheduler.RestoreTenantPriority(tenantName);

            // Pick highest-priority waiter (not FIFO).
            Waiter? next = null;
            GpuPriorityClass highestPriority = GpuPriorityClass.Idle;
            int nextIndex = -1;

            for (int i = 0; i < _waiters.Count; i++)
            {
                var w = _waiters[i];
                if (w.Tcs.Task.IsCompleted)
                {
                    continue; // cancelled or already satisfied
                }

                var profile = _scheduler.GetTenantProfile(w.TenantName);
                if (profile is null)
                {
                    continue;
                }

                if (profile.EffectivePriority > highestPriority || next is null)
                {
                    highestPriority = profile.EffectivePriority;
                    next = w;
                    nextIndex = i;
                }
            }

            if (next is not null && nextIndex >= 0)
            {
                _waiters.RemoveAt(nextIndex);
                next.CtReg.Dispose();
                _holder = next.TenantName;

                // Re-evaluate boost for the new holder against remaining waiters.
                var newHolderProfile = _scheduler.GetTenantProfile(_holder);
                if (newHolderProfile is not null)
                {
                    foreach (var w in _waiters)
                    {
                        if (w.Tcs.Task.IsCompleted)
                        {
                            continue;
                        }

                        var profile = _scheduler.GetTenantProfile(w.TenantName);
                        if (profile?.EffectivePriority > newHolderProfile.EffectivePriority)
                        {
                            _scheduler.BoostTenantPriority(_holder, profile.EffectivePriority);
                            break;
                        }
                    }
                }

                next.Tcs.TrySetResult(null);
            }
            else
            {
                _holder = null;
            }
        }

        return Task.CompletedTask;
    }
}
