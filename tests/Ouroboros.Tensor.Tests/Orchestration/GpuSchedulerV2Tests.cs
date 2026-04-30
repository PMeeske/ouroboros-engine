// <copyright file="GpuSchedulerV2Tests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Orchestration;

[Trait("Category", "Unit")]
public sealed class GpuSchedulerV2Tests : IDisposable
{
    private const long FourGb = 4L * 1024 * 1024 * 1024;
    private readonly GpuSchedulerV2 _sut = new(totalVramBytes: FourGb);

    [Fact]
    public async Task StrictPreemption_RealtimeRunsBeforeQueuedBackground()
    {
        // Arrange — a slow-running tenant that holds the dispatch loop long enough
        // for us to enqueue the rest of the work before anything has drained.
        var bgProfile = new GpuTenantProfile(
            "Background-A", GpuPriorityClass.Background, 0, TimeSpan.FromSeconds(5), true, EvictionPolicy.None);
        var rtProfile = new GpuTenantProfile(
            "Realtime-A", GpuPriorityClass.Realtime, 0, TimeSpan.FromSeconds(5), false, EvictionPolicy.None);

        using var regBg = _sut.RegisterTenant(bgProfile);
        using var regRt = _sut.RegisterTenant(rtProfile);

        var observedOrder = new System.Collections.Concurrent.ConcurrentQueue<string>();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstBgEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Enqueue 1 slow background item that blocks on the gate so we can enqueue more.
        var bg1 = _sut.ScheduleAsync(
            "Background-A",
            async ct =>
            {
                observedOrder.Enqueue("BG1");
                firstBgEntered.TrySetResult();
                await gate.Task.ConfigureAwait(false);
                return 0;
            });

        // Wait until BG1 is actually running so we know the dispatcher has started draining.
        await firstBgEntered.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

        // Now enqueue 2 more background items and 1 realtime item.
        var bg2 = _sut.ScheduleAsync(
            "Background-A",
            ct => { observedOrder.Enqueue("BG2"); return Task.FromResult(0); });
        var bg3 = _sut.ScheduleAsync(
            "Background-A",
            ct => { observedOrder.Enqueue("BG3"); return Task.FromResult(0); });
        var rt1 = _sut.ScheduleAsync(
            "Realtime-A",
            ct => { observedOrder.Enqueue("RT1"); return Task.FromResult(0); });

        // Release the gate so BG1 can finish and the dispatcher can pick the next item.
        gate.SetResult();

        await Task.WhenAll(bg1, bg2, bg3, rt1).WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);

        // Assert: order should be BG1 (was mid-flight), RT1 (preempts queued BGs), BG2, BG3.
        var order = observedOrder.ToArray();
        order.Should().HaveCount(4);
        order[0].Should().Be("BG1");
        order[1].Should().Be("RT1"); // Strict preemption: Realtime jumps ahead of queued Background.
        order[2].Should().BeOneOf("BG2", "BG3");
        order[3].Should().BeOneOf("BG2", "BG3");
        order[2].Should().NotBe(order[3]);
    }

    [Fact]
    public async Task RoundRobin_WithinEqualPriority_AlternatesTenants()
    {
        // Arrange — three Normal-priority tenants, each with 2 items queued.
        var a = new GpuTenantProfile("N-A", GpuPriorityClass.Normal, 0, TimeSpan.FromSeconds(5), true, EvictionPolicy.None);
        var b = new GpuTenantProfile("N-B", GpuPriorityClass.Normal, 0, TimeSpan.FromSeconds(5), true, EvictionPolicy.None);
        var c = new GpuTenantProfile("N-C", GpuPriorityClass.Normal, 0, TimeSpan.FromSeconds(5), true, EvictionPolicy.None);

        using var regA = _sut.RegisterTenant(a);
        using var regB = _sut.RegisterTenant(b);
        using var regC = _sut.RegisterTenant(c);

        var order = new System.Collections.Concurrent.ConcurrentQueue<string>();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Preload a blocking first item from A so all subsequent enqueues happen
        // before any dispatch decisions are made — makes round-robin deterministic.
        var blocker = _sut.ScheduleAsync(
            "N-A",
            async ct =>
            {
                order.Enqueue("A");
                firstEntered.TrySetResult();
                await gate.Task.ConfigureAwait(false);
                return 0;
            });

        await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

        // Now enqueue the rest: 1 more for A, 2 for B, 2 for C.
        var t1 = _sut.ScheduleAsync("N-A", ct => { order.Enqueue("A"); return Task.FromResult(0); });
        var t2 = _sut.ScheduleAsync("N-B", ct => { order.Enqueue("B"); return Task.FromResult(0); });
        var t3 = _sut.ScheduleAsync("N-B", ct => { order.Enqueue("B"); return Task.FromResult(0); });
        var t4 = _sut.ScheduleAsync("N-C", ct => { order.Enqueue("C"); return Task.FromResult(0); });
        var t5 = _sut.ScheduleAsync("N-C", ct => { order.Enqueue("C"); return Task.FromResult(0); });

        gate.SetResult();

        await Task.WhenAll(blocker, t1, t2, t3, t4, t5).WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);

        // Assert — after the initial blocking A, the dispatcher should rotate A → B → C → A → B → C.
        // Because A's turn was consumed by the blocker, the next serve from A's queue happens
        // after one full rotation. The key property is: no tenant appears twice in a row
        // until all other non-empty tenants have been served.
        var arr = order.ToArray();
        arr.Should().HaveCount(6);
        arr[0].Should().Be("A"); // the blocker

        // Verify strict round-robin on the remaining five: no consecutive duplicates while
        // other tenants still have queued work.
        var remaining = new Dictionary<string, int> { ["A"] = 1, ["B"] = 2, ["C"] = 2 };
        for (int i = 1; i < arr.Length; i++)
        {
            remaining[arr[i]]--;
            if (i > 1 && arr[i] == arr[i - 1])
            {
                // Same tenant twice in a row is only allowed if every other tenant is exhausted.
                var othersHaveWork = remaining.Any(kv => kv.Key != arr[i] && kv.Value > 0);
                othersHaveWork.Should().BeFalse(
                    $"tenant {arr[i]} appeared twice in a row at index {i} while others still had work");
            }
        }

        remaining.Values.Should().AllBeEquivalentTo(0);
    }

    [Fact]
    public async Task StateTransitions_ReadyToRunningToDone()
    {
        var profile = new GpuTenantProfile(
            "StateTest", GpuPriorityClass.Normal, 0, TimeSpan.FromSeconds(5), true, EvictionPolicy.None);
        using var reg = _sut.RegisterTenant(profile);

        // Before any work queued: Done.
        _sut.GetTenantState("StateTest").Should().Be(GpuTaskState.Done);

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runningObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var work = _sut.ScheduleAsync(
            "StateTest",
            async ct =>
            {
                runningObserved.TrySetResult();
                await gate.Task.ConfigureAwait(false);
                return 0;
            });

        // Wait until the delegate is actually running.
        await runningObserved.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        _sut.GetTenantState("StateTest").Should().Be(GpuTaskState.Running);

        gate.SetResult();
        await work.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

        // Post-completion: back to Done.
        _sut.GetTenantState("StateTest").Should().Be(GpuTaskState.Done);
    }

    [Fact]
    public async Task UnregisterTenant_RemovesFromDispatch()
    {
        var profile = new GpuTenantProfile(
            "Ephemeral", GpuPriorityClass.Normal, 0, TimeSpan.FromSeconds(5), true, EvictionPolicy.None);
        var reg = _sut.RegisterTenant(profile);

        // Sanity check: we can schedule while registered.
        var r = await _sut.ScheduleAsync("Ephemeral", ct => Task.FromResult(7));
        r.Should().Be(7);

        // Unregister.
        reg.Dispose();

        // After dispose, new work throws.
        var act = async () => await _sut.ScheduleAsync("Ephemeral", ct => Task.FromResult(0));
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not registered*");
    }

    [Fact]
    public async Task Dispose_CancelsDispatchLoopCleanly()
    {
        var scheduler = new GpuSchedulerV2(totalVramBytes: FourGb);
        var profile = new GpuTenantProfile(
            "Disposable", GpuPriorityClass.Normal, 0, TimeSpan.FromSeconds(5), true, EvictionPolicy.None);
        using var reg = scheduler.RegisterTenant(profile);

        // Run a trivial item so the loop parks on the semaphore afterward.
        await scheduler.ScheduleAsync("Disposable", ct => Task.FromResult(0));

        // Give the loop a moment to park.
        await Task.Delay(50).ConfigureAwait(false);

        scheduler.Dispose();

        scheduler.DispatchTask.IsCompleted.Should().BeTrue(
            "dispatch loop should exit cleanly on dispose (no thread leak)");
    }

    public void Dispose() => _sut.Dispose();
}
