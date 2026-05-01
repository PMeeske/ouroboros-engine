// <copyright file="GpuSchedulerTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Orchestration;

[Trait("Category", "Unit")]
public sealed class GpuSchedulerTests : IDisposable
{
    private const long FourGb = 4L * 1024 * 1024 * 1024;
    private readonly GpuScheduler _sut = new(totalVramBytes: FourGb);

    // ── Scheduling ──────────────────────────────────────────────────────

    [Fact]
    public async Task ScheduleAsync_ExecutesWork_ReturnsResult()
    {
        var result = await _sut.ScheduleAsync(
            GpuTaskPriority.Normal, new GpuResourceRequirements(1024), () => 42);

        result.Should().Be(42);
    }

    [Fact]
    public async Task ScheduleAsync_AsyncWork_CompletesSuccessfully()
    {
        var result = await _sut.ScheduleAsync(
            GpuTaskPriority.Normal,
            new GpuResourceRequirements(1024),
            async () => { await Task.Delay(10); return "done"; });

        result.Should().Be("done");
    }

    // ── VRAM Accounting ─────────────────────────────────────────────────

    [Fact]
    public async Task ScheduleAsync_TracksVram_DuringExecution()
    {
        long vramDuringExecution = 0;

        await _sut.ScheduleAsync(
            GpuTaskPriority.Normal,
            new GpuResourceRequirements(1_000_000),
            () =>
            {
                vramDuringExecution = FourGb - _sut.EstimatedAvailableVram;
                return 0;
            });

        vramDuringExecution.Should().Be(1_000_000);
        _sut.EstimatedAvailableVram.Should().Be(FourGb, "VRAM should be fully released after task");
    }

    [Fact]
    public async Task ScheduleAsync_ReleasesVram_OnException()
    {
        // Act
        Func<Task<int>> throwingWork = () => throw new InvalidOperationException("boom");
        Func<Task> act = async () => await _sut.ScheduleAsync(
            GpuTaskPriority.Normal,
            new GpuResourceRequirements(1_000_000),
            throwingWork);
        await act.Should().ThrowAsync<InvalidOperationException>();

        // Assert
        _sut.EstimatedAvailableVram.Should().Be(FourGb);
    }

    [Fact]
    public async Task ScheduleAsync_RejectsOvercommit_ForLowPriority()
    {
        var act = () => _sut.ScheduleAsync(
            GpuTaskPriority.Background,
            new GpuResourceRequirements(FourGb + 1),
            () => 0);

        await act.Should().ThrowAsync<InsufficientMemoryException>();
    }

    [Fact]
    public async Task ScheduleAsync_AllowsOvercommit_ForHighPriority()
    {
        var result = await _sut.ScheduleAsync(
            GpuTaskPriority.High,
            new GpuResourceRequirements(FourGb + 1),
            () => 42);

        result.Should().Be(42);
    }

    // ── Backpressure ────────────────────────────────────────────────────

    [Fact]
    public async Task ScheduleAsync_WhenPaused_RejectsLowPriority()
    {
        _sut.PauseAccepting();

        var act = () => _sut.ScheduleAsync(
            GpuTaskPriority.Normal, new GpuResourceRequirements(1024), () => 0);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ScheduleAsync_WhenPaused_AcceptsHighPriority()
    {
        _sut.PauseAccepting();

        var result = await _sut.ScheduleAsync(
            GpuTaskPriority.High, new GpuResourceRequirements(1024), () => 42);

        result.Should().Be(42);
    }

    [Fact]
    public async Task ResumeAccepting_AllowsNormalPriorityAgain()
    {
        _sut.PauseAccepting();
        _sut.ResumeAccepting();

        var result = await _sut.ScheduleAsync(
            GpuTaskPriority.Normal, new GpuResourceRequirements(1024), () => 42);

        result.Should().Be(42);
    }

    // ── Metrics ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CurrentMetrics_ReflectsCompletedTasks()
    {
        await _sut.ScheduleAsync(GpuTaskPriority.Normal, new(1024), () => 0);
        await _sut.ScheduleAsync(GpuTaskPriority.Normal, new(1024), () => 0);

        _sut.CurrentMetrics.TotalTasksCompleted.Should().Be(2);
        _sut.CurrentMetrics.TotalTasksFailed.Should().Be(0);
    }

    [Fact]
    public async Task CurrentMetrics_TracksFailedTasks()
    {
        Func<Task<int>> throwingWork = () => throw new InvalidOperationException("fail");
        Func<Task> act = async () => await _sut.ScheduleAsync(
            GpuTaskPriority.Normal, new(1024),
            throwingWork);
        await act.Should().ThrowAsync<InvalidOperationException>();

        _sut.CurrentMetrics.TotalTasksFailed.Should().Be(1);
    }

    [Fact]
    public async Task CurrentMetrics_RecordsLatency()
    {
        await _sut.ScheduleAsync(
            GpuTaskPriority.Normal, new(1024),
            async () => { await Task.Delay(20); return 0; });

        _sut.CurrentMetrics.LastTaskLatency.Should().BeGreaterThan(TimeSpan.Zero);
    }

    // ── Dispose ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ScheduleAsync_ThrowsObjectDisposed_AfterDispose()
    {
        _sut.Dispose();

        var act = () => _sut.ScheduleAsync(
            GpuTaskPriority.Normal, new(1024), () => 0);

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    public void Dispose() => _sut.Dispose();
}
