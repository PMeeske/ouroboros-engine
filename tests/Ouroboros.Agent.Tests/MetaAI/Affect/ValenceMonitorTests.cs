// <copyright file="ValenceMonitorTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Agent.MetaAI.Affect;

namespace Ouroboros.Tests.MetaAI.Affect;

[Trait("Category", "Unit")]
public class ValenceMonitorTests
{
    private readonly ValenceMonitor _monitor = new();

    [Fact]
    public void GetCurrentState_ReturnsDefaultState()
    {
        var state = _monitor.GetCurrentState();

        state.Should().NotBeNull();
        state.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void RecordSignal_StoresSignal()
    {
        _monitor.RecordSignal("source1", 0.5, SignalType.Stress);

        var signals = _monitor.GetRecentSignals(SignalType.Stress);
        signals.Should().HaveCount(1);
        signals[0].Source.Should().Be("source1");
    }

    [Fact]
    public void RecordSignal_ClampsValue()
    {
        _monitor.RecordSignal("s", 2.0, SignalType.Valence);

        var signals = _monitor.GetRecentSignals(SignalType.Valence);
        signals[0].Value.Should().Be(1.0);
    }

    [Fact]
    public void RecordSignal_NullSource_Throws()
    {
        var act = () => _monitor.RecordSignal(null!, 0.5, SignalType.Stress);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void UpdateConfidence_AdjustsOnSuccess()
    {
        var stateBefore = _monitor.GetCurrentState();
        _monitor.UpdateConfidence("task1", true);

        var state = _monitor.GetCurrentState();
        state.Confidence.Should().BeGreaterThanOrEqualTo(stateBefore.Confidence);
    }

    [Fact]
    public void UpdateConfidence_AdjustsOnFailure()
    {
        var stateBefore = _monitor.GetCurrentState();
        _monitor.UpdateConfidence("task1", false);

        var state = _monitor.GetCurrentState();
        state.Confidence.Should().BeLessThanOrEqualTo(stateBefore.Confidence);
    }

    [Fact]
    public void UpdateCuriosity_IncreasesWithNovelty()
    {
        var stateBefore = _monitor.GetCurrentState();
        _monitor.UpdateCuriosity(0.9, "novel-context");
        var stateAfter = _monitor.GetCurrentState();

        stateAfter.Curiosity.Should().BeGreaterThan(stateBefore.Curiosity);
    }

    [Fact]
    public void GetSignalHistory_ReturnsValues()
    {
        _monitor.RecordSignal("s", 0.5, SignalType.Stress);
        _monitor.RecordSignal("s", 0.7, SignalType.Stress);

        var history = _monitor.GetSignalHistory(SignalType.Stress);
        history.Should().HaveCount(2);
    }

    [Fact]
    public void GetRunningAverage_WithSignals_ReturnsAverage()
    {
        _monitor.RecordSignal("s", 0.4, SignalType.Stress);
        _monitor.RecordSignal("s", 0.6, SignalType.Stress);

        var avg = _monitor.GetRunningAverage(SignalType.Stress, 10);
        avg.Should().BeApproximately(0.5, 0.01);
    }

    [Fact]
    public void GetRunningAverage_NoSignals_ReturnsZero()
    {
        _monitor.GetRunningAverage(SignalType.Arousal).Should().Be(0.0);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        _monitor.RecordSignal("s", 0.8, SignalType.Stress);
        _monitor.Reset();

        _monitor.GetRecentSignals(SignalType.Stress).Should().BeEmpty();
        var state = _monitor.GetCurrentState();
        state.Stress.Should().Be(0.0);
    }

    [Fact]
    public void GetStateHistory_ReturnsRecordedStates()
    {
        _monitor.GetCurrentState();
        _monitor.GetCurrentState();

        var history = _monitor.GetStateHistory();
        history.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task DetectStressAsync_InsufficientData_ReturnsDefault()
    {
        _monitor.RecordSignal("s", 0.5, SignalType.Stress);

        var result = await _monitor.DetectStressAsync();

        result.Should().NotBeNull();
        result.Analysis.Should().Contain("Insufficient");
    }

    [Fact]
    public async Task DetectStressAsync_WithEnoughData_ReturnsAnalysis()
    {
        for (int i = 0; i < 16; i++)
        {
            _monitor.RecordSignal("s", Math.Sin(i * 0.5) * 0.5, SignalType.Stress);
        }

        var result = await _monitor.DetectStressAsync();

        result.Should().NotBeNull();
        result.Frequency.Should().BeGreaterThanOrEqualTo(0.0);
    }
}
