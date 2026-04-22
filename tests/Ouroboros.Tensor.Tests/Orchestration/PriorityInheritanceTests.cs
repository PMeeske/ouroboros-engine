// <copyright file="PriorityInheritanceTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Orchestration;

[Trait("Category", "Unit")]
public sealed class PriorityInheritanceTests : IDisposable
{
    private const long FourGb = 4L * 1024 * 1024 * 1024;
    private readonly GpuSchedulerV2 _sut = new(totalVramBytes: FourGb);

    [Fact]
    public async Task MarsPathfinder_InversionResolved()
    {
        var normalProfile = new GpuTenantProfile(
            "Normal", GpuPriorityClass.Normal, 0, TimeSpan.FromSeconds(5), true, EvictionPolicy.None);
        var realtimeProfile = new GpuTenantProfile(
            "Realtime", GpuPriorityClass.Realtime, 0, TimeSpan.FromSeconds(5), false, EvictionPolicy.None);
        var backgroundProfile = new GpuTenantProfile(
            "Background", GpuPriorityClass.Background, 0, TimeSpan.FromSeconds(5), true, EvictionPolicy.None);

        using var regN = _sut.RegisterTenant(normalProfile);
        using var regR = _sut.RegisterTenant(realtimeProfile);
        using var regB = _sut.RegisterTenant(backgroundProfile);

        var resource = new GpuResourceHolder(_sut, "MarsPathfinder");
        var observedOrder = new System.Collections.Concurrent.ConcurrentQueue<string>();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var normalAcquired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Normal work acquires resource and holds until gate is released.
        var normalWork = _sut.ScheduleAsync("Normal", async ct =>
        {
            await resource.AcquireAsync("Normal", ct).ConfigureAwait(false);
            observedOrder.Enqueue("Normal-Acquired");
            normalAcquired.TrySetResult();
            await gate.Task.WaitAsync(ct).ConfigureAwait(false);
            await resource.ReleaseAsync("Normal").ConfigureAwait(false);
            observedOrder.Enqueue("Normal-Released");
            return 0;
        });

        // Wait until Normal has acquired.
        await normalAcquired.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

        // Schedule Realtime and Background work that both need the resource.
        var realtimeWork = _sut.ScheduleAsync("Realtime", async ct =>
        {
            await resource.AcquireAsync("Realtime", ct).ConfigureAwait(false);
            observedOrder.Enqueue("Realtime-Acquired");
            await resource.ReleaseAsync("Realtime").ConfigureAwait(false);
            observedOrder.Enqueue("Realtime-Released");
            return 0;
        });

        var backgroundWork = _sut.ScheduleAsync("Background", async ct =>
        {
            await resource.AcquireAsync("Background", ct).ConfigureAwait(false);
            observedOrder.Enqueue("Background-Acquired");
            await resource.ReleaseAsync("Background").ConfigureAwait(false);
            observedOrder.Enqueue("Background-Released");
            return 0;
        });

        // Give time for waiters to enqueue.
        await Task.Delay(100).ConfigureAwait(false);

        // Normal should be boosted to Realtime because Realtime is waiting.
        _sut.GetTenantProfile("Normal")!.EffectivePriority.Should().Be(GpuPriorityClass.Realtime);

        // Release the gate so Normal can finish.
        gate.SetResult();

        await Task.WhenAll(normalWork, realtimeWork, backgroundWork)
            .WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);

        var order = observedOrder.ToArray();
        order.Should().Equal(
            "Normal-Acquired",
            "Normal-Released",
            "Realtime-Acquired",
            "Realtime-Released",
            "Background-Acquired",
            "Background-Released");

        // After release, Normal should be restored.
        _sut.GetTenantProfile("Normal")!.EffectivePriority.Should().Be(GpuPriorityClass.Normal);
    }

    [Fact]
    public async Task Inheritance_DoesNotBoost_WhenWaiterLowerPriority()
    {
        var normalProfile = new GpuTenantProfile(
            "Normal", GpuPriorityClass.Normal, 0, TimeSpan.FromSeconds(5), true, EvictionPolicy.None);
        var backgroundProfile = new GpuTenantProfile(
            "Background", GpuPriorityClass.Background, 0, TimeSpan.FromSeconds(5), true, EvictionPolicy.None);

        using var regN = _sut.RegisterTenant(normalProfile);
        using var regB = _sut.RegisterTenant(backgroundProfile);

        var resource = new GpuResourceHolder(_sut, "NoBoost");

        await resource.AcquireAsync("Normal").ConfigureAwait(false);

        var bgAcquire = resource.AcquireAsync("Background");
        await Task.Delay(50).ConfigureAwait(false);

        // Background is lower priority than Normal — no boost should occur.
        _sut.GetTenantProfile("Normal")!.EffectivePriority.Should().Be(GpuPriorityClass.Normal);

        await resource.ReleaseAsync("Normal").ConfigureAwait(false);
        await bgAcquire.ConfigureAwait(false);
        await resource.ReleaseAsync("Background").ConfigureAwait(false);
    }

    [Fact]
    public async Task Inheritance_PicksHighestWaiter_OnRelease()
    {
        var normalProfile = new GpuTenantProfile(
            "Normal", GpuPriorityClass.Normal, 0, TimeSpan.FromSeconds(5), true, EvictionPolicy.None);
        var realtimeProfile = new GpuTenantProfile(
            "Realtime", GpuPriorityClass.Realtime, 0, TimeSpan.FromSeconds(5), false, EvictionPolicy.None);
        var backgroundProfile = new GpuTenantProfile(
            "Background", GpuPriorityClass.Background, 0, TimeSpan.FromSeconds(5), true, EvictionPolicy.None);

        using var regN = _sut.RegisterTenant(normalProfile);
        using var regR = _sut.RegisterTenant(realtimeProfile);
        using var regB = _sut.RegisterTenant(backgroundProfile);

        var resource = new GpuResourceHolder(_sut, "HighestWins");

        await resource.AcquireAsync("Normal").ConfigureAwait(false);

        var bgAcquire = resource.AcquireAsync("Background");
        var rtAcquire = resource.AcquireAsync("Realtime");

        await Task.Delay(50).ConfigureAwait(false);

        await resource.ReleaseAsync("Normal").ConfigureAwait(false);

        // Realtime should acquire first (highest priority waiter).
        rtAcquire.IsCompletedSuccessfully.Should().BeTrue();
        bgAcquire.IsCompleted.Should().BeFalse();

        await resource.ReleaseAsync("Realtime").ConfigureAwait(false);
        await bgAcquire.ConfigureAwait(false);
        await resource.ReleaseAsync("Background").ConfigureAwait(false);
    }

    [Fact]
    public async Task Inheritance_SingleHop_DocumentedLimitation()
    {
        // A < B < C in base priority so that each wait creates a boost,
        // but the boost must not walk transitively.
        var aProfile = new GpuTenantProfile(
            "A", GpuPriorityClass.Background, 0, TimeSpan.FromSeconds(5), true, EvictionPolicy.None);
        var bProfile = new GpuTenantProfile(
            "B", GpuPriorityClass.Normal, 0, TimeSpan.FromSeconds(5), true, EvictionPolicy.None);
        var cProfile = new GpuTenantProfile(
            "C", GpuPriorityClass.Realtime, 0, TimeSpan.FromSeconds(5), false, EvictionPolicy.None);

        using var regA = _sut.RegisterTenant(aProfile);
        using var regB = _sut.RegisterTenant(bProfile);
        using var regC = _sut.RegisterTenant(cProfile);

        var r1 = new GpuResourceHolder(_sut, "R1");
        var r2 = new GpuResourceHolder(_sut, "R2");

        // A holds R1.
        await r1.AcquireAsync("A").ConfigureAwait(false);

        // B holds R2.
        await r2.AcquireAsync("B").ConfigureAwait(false);

        // B tries to acquire R1 — blocked, boosts A to Normal.
        var bAcquireR1 = r1.AcquireAsync("B");
        await Task.Delay(50).ConfigureAwait(false);

        _sut.GetTenantProfile("A")!.EffectivePriority.Should().Be(GpuPriorityClass.Normal);
        _sut.GetTenantProfile("B")!.EffectivePriority.Should().Be(GpuPriorityClass.Normal);

        // C tries to acquire R2 — blocked, boosts B to Realtime (single hop from C to B).
        var cAcquireR2 = r2.AcquireAsync("C");
        await Task.Delay(50).ConfigureAwait(false);

        _sut.GetTenantProfile("B")!.EffectivePriority.Should().Be(GpuPriorityClass.Realtime);

        // A must NOT be boosted beyond Normal (single-hop limitation — no transitive walk).
        _sut.GetTenantProfile("A")!.EffectivePriority.Should().Be(GpuPriorityClass.Normal);

        // Cleanup.
        await r1.ReleaseAsync("A").ConfigureAwait(false);
        await bAcquireR1.ConfigureAwait(false);

        await r2.ReleaseAsync("B").ConfigureAwait(false);
        await cAcquireR2.ConfigureAwait(false);

        await r1.ReleaseAsync("B").ConfigureAwait(false);
        await r2.ReleaseAsync("C").ConfigureAwait(false);
    }

    public void Dispose() => _sut.Dispose();
}
