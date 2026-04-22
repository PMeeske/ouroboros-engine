// <copyright file="DispatchWatchdogTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Ouroboros.Tensor.Orchestration;

namespace Ouroboros.Tests.Orchestration;

[Trait("Category", "Unit")]
public sealed class DispatchWatchdogTests : IDisposable
{
    private const long FourGb = 4L * 1024 * 1024 * 1024;
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly List<LogEntry> _logs = new();
    private readonly GpuSchedulerV2 _scheduler;

    public DispatchWatchdogTests()
    {
        _scheduler = new GpuSchedulerV2(FourGb, timeProvider: _timeProvider, logger: new ListLogger(_logs));
    }

    [Fact]
    public void FirstOverrun_LogsWarning_DoesNotDemote()
    {
        var profile = new GpuTenantProfile(
            "A", GpuPriorityClass.Normal, 0, TimeSpan.FromMilliseconds(10), true, EvictionPolicy.None);
        using var reg = _scheduler.RegisterTenant(profile);

        var watchdog = new DispatchWatchdog(_scheduler, _timeProvider, new ListLogger(_logs));
        watchdog.RecordDispatch("A", TimeSpan.FromMilliseconds(100));

        _logs.Should().ContainSingle(e => e.Message.Contains("overran"));
        _scheduler.GetTenantProfile("A")!.BasePriority.Should().Be(GpuPriorityClass.Normal);
        watchdog.Dispose();
    }

    [Fact]
    public void ThreeOverrunsIn60Seconds_DemotesByOneTier()
    {
        var profile = new GpuTenantProfile(
            "A", GpuPriorityClass.Background, 0, TimeSpan.FromMilliseconds(10), true, EvictionPolicy.None);
        using var reg = _scheduler.RegisterTenant(profile);

        var watchdog = new DispatchWatchdog(_scheduler, _timeProvider, new ListLogger(_logs));

        for (int i = 0; i < 3; i++)
        {
            watchdog.RecordDispatch("A", TimeSpan.FromMilliseconds(100));
        }

        _scheduler.GetTenantProfile("A")!.BasePriority.Should().Be(GpuPriorityClass.Idle);
        _logs.Should().Contain(e => e.Message.Contains("demoted Background -> Idle"));
        watchdog.Dispose();
    }

    [Fact]
    public void OverrunsOutsideWindow_DoNotCompound()
    {
        var profile = new GpuTenantProfile(
            "A", GpuPriorityClass.Normal, 0, TimeSpan.FromMilliseconds(10), true, EvictionPolicy.None);
        using var reg = _scheduler.RegisterTenant(profile);

        var watchdog = new DispatchWatchdog(_scheduler, _timeProvider, new ListLogger(_logs));

        // First overrun at T=0.
        watchdog.RecordDispatch("A", TimeSpan.FromMilliseconds(100));

        // Advance 61 seconds so the first overrun falls out of the window.
        _timeProvider.Advance(TimeSpan.FromSeconds(61));

        // Second and third overruns — only two are in the window at any time.
        watchdog.RecordDispatch("A", TimeSpan.FromMilliseconds(100));
        watchdog.RecordDispatch("A", TimeSpan.FromMilliseconds(100));

        _scheduler.GetTenantProfile("A")!.BasePriority.Should().Be(GpuPriorityClass.Normal);
        watchdog.Dispose();
    }

    [Fact]
    public async Task DemotedTenant_AutoRestoresAfterFiveMinutes()
    {
        var profile = new GpuTenantProfile(
            "A", GpuPriorityClass.Normal, 0, TimeSpan.FromMilliseconds(10), true, EvictionPolicy.None);
        using var reg = _scheduler.RegisterTenant(profile);

        var watchdog = new DispatchWatchdog(_scheduler, _timeProvider, new ListLogger(_logs));

        // Trigger demotion.
        for (int i = 0; i < 3; i++)
        {
            watchdog.RecordDispatch("A", TimeSpan.FromMilliseconds(100));
        }

        _scheduler.GetTenantProfile("A")!.BasePriority.Should().Be(GpuPriorityClass.Background);

        // Advance past the 5-minute cooldown.
        _timeProvider.Advance(TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(15)));

        // Give the restore loop a chance to tick (10 s interval).
        await Task.Delay(50).ConfigureAwait(false);

        _scheduler.GetTenantProfile("A")!.BasePriority.Should().Be(GpuPriorityClass.Normal);
        _logs.Should().Contain(e => e.Message.Contains("restored to Normal"));
        watchdog.Dispose();
    }

    [Fact]
    public void RealtimeTenant_DemotesToNormal_NotBelow()
    {
        var profile = new GpuTenantProfile(
            "RT", GpuPriorityClass.Realtime, 0, TimeSpan.FromMilliseconds(10), false, EvictionPolicy.None);
        using var reg = _scheduler.RegisterTenant(profile);

        var watchdog = new DispatchWatchdog(_scheduler, _timeProvider, new ListLogger(_logs));

        for (int i = 0; i < 3; i++)
        {
            watchdog.RecordDispatch("RT", TimeSpan.FromMilliseconds(100));
        }

        _scheduler.GetTenantProfile("RT")!.BasePriority.Should().Be(GpuPriorityClass.Normal);
        _logs.Should().Contain(e => e.Message.Contains("demoted Realtime -> Normal"));
        watchdog.Dispose();
    }

    [Fact]
    public void IdleTenant_DoesNotDemoteBelowIdle_LogsWarning()
    {
        var profile = new GpuTenantProfile(
            "IdleT", GpuPriorityClass.Idle, 0, TimeSpan.FromMilliseconds(10), true, EvictionPolicy.None);
        using var reg = _scheduler.RegisterTenant(profile);

        var watchdog = new DispatchWatchdog(_scheduler, _timeProvider, new ListLogger(_logs));

        for (int i = 0; i < 3; i++)
        {
            watchdog.RecordDispatch("IdleT", TimeSpan.FromMilliseconds(100));
        }

        _scheduler.GetTenantProfile("IdleT")!.BasePriority.Should().Be(GpuPriorityClass.Idle);
        _logs.Should().Contain(e => e.Message.Contains("already at Idle priority"));
        watchdog.Dispose();
    }

    [Fact]
    public async Task RunawayBackground_DoesNotStarveRealtime()
    {
        // Background tenant with very short MaxDispatchTime so every dispatch overruns.
        var bgProfile = new GpuTenantProfile(
            "BG", GpuPriorityClass.Background, 0, TimeSpan.FromMilliseconds(10), true, EvictionPolicy.None);
        var rtProfile = new GpuTenantProfile(
            "RT", GpuPriorityClass.Realtime, 0, TimeSpan.FromSeconds(5), false, EvictionPolicy.None);

        using var regBg = _scheduler.RegisterTenant(bgProfile);
        using var regRt = _scheduler.RegisterTenant(rtProfile);

        var observedOrder = new System.Collections.Concurrent.ConcurrentQueue<string>();
        var bgGate = new System.Threading.ManualResetEventSlim(false);
        var bgEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Enqueue one slow Background item that blocks on the gate.
        var bg1 = _scheduler.ScheduleAsync(
            "BG",
            async ct =>
            {
                observedOrder.Enqueue("BG1-start");
                bgEntered.TrySetResult();
                await Task.Run(() => bgGate.Wait(ct), ct).ConfigureAwait(false);
                observedOrder.Enqueue("BG1-end");
                return 0;
            });

        await bgEntered.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

        // Enqueue a Realtime item and a second Background item while BG1 is still running.
        var rt1 = _scheduler.ScheduleAsync(
            "RT",
            ct =>
            {
                observedOrder.Enqueue("RT1");
                return Task.FromResult(0);
            });

        var bg2 = _scheduler.ScheduleAsync(
            "BG",
            ct =>
            {
                observedOrder.Enqueue("BG2");
                return Task.FromResult(0);
            });

        // Release the gate so BG1 can finish.
        bgGate.Set();

        await Task.WhenAll(bg1, rt1, bg2).WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);

        var order = observedOrder.ToArray();

        // Realtime must run before the second Background item because Realtime > Background.
        var rtIndex = Array.IndexOf(order, "RT1");
        var bg2Index = Array.IndexOf(order, "BG2");
        rtIndex.Should().BeGreaterThan(-1, "Realtime work should have been observed");
        bg2Index.Should().BeGreaterThan(-1, "Background work 2 should have been observed");
        rtIndex.Should().BeLessThan(bg2Index, "Realtime must preempt queued Background work");
    }

    public void Dispose() => _scheduler.Dispose();

    private sealed record LogEntry(LogLevel Level, string Message);

    private sealed class ListLogger : ILogger
    {
        private readonly List<LogEntry> _entries;

        public ListLogger(List<LogEntry> entries) => _entries = entries;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _entries.Add(new LogEntry(logLevel, formatter(state, exception)));
        }
    }
}
