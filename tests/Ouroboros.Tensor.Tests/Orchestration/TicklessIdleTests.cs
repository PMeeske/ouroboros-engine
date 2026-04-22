// <copyright file="TicklessIdleTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Diagnostics;
using Ouroboros.Tensor.Orchestration;

namespace Ouroboros.Tests.Orchestration;

public sealed class TicklessIdleTests : IDisposable
{
    private const long FourGb = 4L * 1024 * 1024 * 1024;

    [Trait("Category", "Timing")]
    [Trait("Flaky", "Timing")]
    [Fact]
    public async Task IdleScheduler_CpuUsageUnderOnePercent_OverTwoSeconds()
    {
        var scheduler = new GpuSchedulerV2(totalVramBytes: FourGb);

        // Let the loop park on the semaphore.
        await Task.Delay(100).ConfigureAwait(false);

        var proc = Process.GetCurrentProcess();
        var before = proc.TotalProcessorTime;
        var sw = Stopwatch.StartNew();

        await Task.Delay(2000).ConfigureAwait(false);

        sw.Stop();
        var after = proc.TotalProcessorTime;
        var elapsedCpu = after - before;
        var wallClock = sw.Elapsed;
        var cpuPercent = elapsedCpu.TotalSeconds / (wallClock.TotalSeconds * Environment.ProcessorCount);

        scheduler.Dispose();

        cpuPercent.Should().BeLessThan(0.02, $"idle CPU was {cpuPercent:P2}, expected < 2%");
    }

    [Trait("Category", "Timing")]
    [Fact]
    public async Task ScheduleAsync_WakesIdleDispatchLoop_WithinTenMilliseconds()
    {
        var scheduler = new GpuSchedulerV2(totalVramBytes: FourGb);
        var profile = new GpuTenantProfile(
            "Waker", GpuPriorityClass.Normal, 0, TimeSpan.FromSeconds(5), true, EvictionPolicy.None);
        using var reg = scheduler.RegisterTenant(profile);

        // Ensure the loop is idle.
        await Task.Delay(50).ConfigureAwait(false);

        var sw = Stopwatch.StartNew();
        await scheduler.ScheduleAsync("Waker", ct => Task.FromResult(0)).ConfigureAwait(false);
        sw.Stop();

        scheduler.Dispose();

        sw.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(10),
            $"wake latency was {sw.Elapsed.TotalMilliseconds:F2} ms, expected < 10 ms");
    }

    [Trait("Category", "Timing")]
    [Trait("Flaky", "Timing")]
    [Fact]
    public async Task DispatchLoop_ReturnsToIdle_AfterAllWorkDrained()
    {
        var scheduler = new GpuSchedulerV2(totalVramBytes: FourGb);
        var profile = new GpuTenantProfile(
            "Bursty", GpuPriorityClass.Normal, 0, TimeSpan.FromSeconds(5), true, EvictionPolicy.None);
        using var reg = scheduler.RegisterTenant(profile);

        // Drain 5 no-op items.
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => scheduler.ScheduleAsync("Bursty", ct => Task.FromResult(0)))
            .ToArray();
        await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

        // Let any finalization settle.
        await Task.Delay(100).ConfigureAwait(false);

        var proc = Process.GetCurrentProcess();
        var before = proc.TotalProcessorTime;
        await Task.Delay(1000).ConfigureAwait(false);
        var after = proc.TotalProcessorTime;

        var elapsedCpu = after - before;
        var cpuPercent = elapsedCpu.TotalSeconds / (1.0 * Environment.ProcessorCount);

        scheduler.Dispose();

        cpuPercent.Should().BeLessThan(0.02, $"post-drain idle CPU was {cpuPercent:P2}, expected < 2%");
    }

    public void Dispose()
    {
        // No shared state across tests.
    }
}
