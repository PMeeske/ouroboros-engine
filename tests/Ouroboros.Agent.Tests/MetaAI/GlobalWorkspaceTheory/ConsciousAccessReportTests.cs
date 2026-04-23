#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using FluentAssertions;
using Ouroboros.Agent.MetaAI.GlobalWorkspaceTheory;
using Xunit;

namespace Ouroboros.Tests.MetaAI.GlobalWorkspaceTheory;

[Trait("Category", "Unit")]
public sealed class ConsciousAccessReportTests
{
    #region ConsciousAccessReport

    [Fact]
    public void Report_ToString_ContainsTickNumber()
    {
        var report = new ConsciousAccessReport
        {
            TickNumber = 42,
            Timestamp = DateTime.UtcNow,
            DurationMs = 23.5,
            Admitted = new List<AdmittedChunkInfo>(),
            Evicted = new List<EvictedChunkInfo>(),
            BroadcastReceiverCount = 0,
            Entropy = 0.34,
            Summary = "no changes"
        };

        string text = report.ToString();

        text.Should().Contain("Tick #42");
    }

    [Fact]
    public void Report_ToString_ContainsAdmittedChunks()
    {
        var report = new ConsciousAccessReport
        {
            TickNumber = 1,
            Timestamp = DateTime.UtcNow,
            DurationMs = 10.0,
            Admitted = new List<AdmittedChunkInfo>
            {
                new("User asked about weather", 0.91, "Chat")
            },
            Evicted = new List<EvictedChunkInfo>(),
            BroadcastReceiverCount = 7,
            Entropy = 0.34,
            Summary = "admitted 1 chunk(s)"
        };

        string text = report.ToString();

        text.Should().Contain("Admitted: \"User asked about weather\" (salience: 0.91, source: Chat)");
        text.Should().Contain("Broadcast: 7 receivers updated");
        text.Should().Contain("Entropy: 0.34");
    }

    [Fact]
    public void Report_ToString_ContainsEvictedChunks()
    {
        var report = new ConsciousAccessReport
        {
            TickNumber = 1,
            Timestamp = DateTime.UtcNow,
            DurationMs = 10.0,
            Admitted = new List<AdmittedChunkInfo>(),
            Evicted = new List<EvictedChunkInfo>
            {
                new("Background heartbeat", 0.12)
            },
            BroadcastReceiverCount = 0,
            Entropy = 0.0,
            Summary = "evicted 1 chunk(s)"
        };

        string text = report.ToString();

        text.Should().Contain("Evicted:  \"Background heartbeat\" (salience: 0.12)");
    }

    #endregion

    #region ConsciousAccessReportBuilder

    [Fact]
    public void Builder_BeginTick_SetsTickNumber()
    {
        var builder = new ConsciousAccessReportBuilder();
        builder.BeginTick(7, DateTime.UtcNow);

        ConsciousAccessReport report = builder.Build(DateTime.UtcNow.AddMilliseconds(10));

        report.TickNumber.Should().Be(7);
    }

    [Fact]
    public void Builder_WithAdmitted_AddsAdmittedInfo()
    {
        var builder = new ConsciousAccessReportBuilder();
        builder.BeginTick(1, DateTime.UtcNow);
        builder.WithAdmitted(new ScoredCandidate(new Candidate("x", 0.5, 0.5, 0.5, 0.5, "Src"), 0.75));

        ConsciousAccessReport report = builder.Build(DateTime.UtcNow.AddMilliseconds(5));

        report.Admitted.Should().HaveCount(1);
        report.Admitted[0].Content.Should().Be("x");
        report.Admitted[0].Salience.Should().Be(0.75);
        report.Admitted[0].Source.Should().Be("Src");
    }

    [Fact]
    public void Builder_WithEvicted_AddsEvictedInfo()
    {
        var builder = new ConsciousAccessReportBuilder();
        builder.BeginTick(1, DateTime.UtcNow);
        builder.WithEvicted(new ScoredCandidate(new Candidate("y", 0.1, 0.1, 0.1, 0.1, "Src"), 0.15));

        ConsciousAccessReport report = builder.Build(DateTime.UtcNow.AddMilliseconds(5));

        report.Evicted.Should().HaveCount(1);
        report.Evicted[0].Content.Should().Be("y");
        report.Evicted[0].Salience.Should().Be(0.15);
    }

    [Fact]
    public void Builder_WithBroadcastReceiverCount_SetsCount()
    {
        var builder = new ConsciousAccessReportBuilder();
        builder.BeginTick(1, DateTime.UtcNow);
        builder.WithBroadcastReceiverCount(12);

        ConsciousAccessReport report = builder.Build(DateTime.UtcNow.AddMilliseconds(5));

        report.BroadcastReceiverCount.Should().Be(12);
    }

    [Fact]
    public void Builder_WithEntropy_SetsEntropy()
    {
        var builder = new ConsciousAccessReportBuilder();
        builder.BeginTick(1, DateTime.UtcNow);
        builder.WithEntropy(0.55);

        ConsciousAccessReport report = builder.Build(DateTime.UtcNow.AddMilliseconds(5));

        report.Entropy.Should().Be(0.55);
    }

    [Fact]
    public void Builder_CalculatesDurationCorrectly()
    {
        var start = DateTime.UtcNow;
        var builder = new ConsciousAccessReportBuilder();
        builder.BeginTick(1, start);

        ConsciousAccessReport report = builder.Build(start.AddMilliseconds(42.0));

        report.DurationMs.Should().BeApproximately(42.0, 1.0);
    }

    [Fact]
    public void Builder_Summary_NoChanges_WhenEmpty()
    {
        var builder = new ConsciousAccessReportBuilder();
        builder.BeginTick(1, DateTime.UtcNow);

        ConsciousAccessReport report = builder.Build(DateTime.UtcNow);

        report.Summary.Should().Be("no changes");
    }

    [Fact]
    public void Builder_Summary_AdmittedAndEvicted()
    {
        var builder = new ConsciousAccessReportBuilder();
        builder.BeginTick(1, DateTime.UtcNow);
        builder.WithAdmitted(new ScoredCandidate(new Candidate("a", 0.5, 0.5, 0.5, 0.5, "Src"), 0.5));
        builder.WithEvicted(new ScoredCandidate(new Candidate("b", 0.1, 0.1, 0.1, 0.1, "Src"), 0.1));

        ConsciousAccessReport report = builder.Build(DateTime.UtcNow);

        report.Summary.Should().Be("admitted 1 chunk(s), evicted 1 chunk(s)");
    }

    [Fact]
    public void Builder_BeginTick_ClearsPreviousState()
    {
        var builder = new ConsciousAccessReportBuilder();
        builder.BeginTick(1, DateTime.UtcNow);
        builder.WithAdmitted(new ScoredCandidate(new Candidate("a", 0.5, 0.5, 0.5, 0.5, "Src"), 0.5));
        builder.Build(DateTime.UtcNow);

        builder.BeginTick(2, DateTime.UtcNow);
        ConsciousAccessReport report = builder.Build(DateTime.UtcNow);

        report.TickNumber.Should().Be(2);
        report.Admitted.Should().BeEmpty();
    }

    #endregion

    #region StructuredTickLogger

    [Fact]
    public async Task StructuredTickLogger_LogAsync_AddsToBuffer()
    {
        var logger = new StructuredTickLogger();
        var report = new ConsciousAccessReport
        {
            TickNumber = 1,
            Timestamp = DateTime.UtcNow,
            DurationMs = 10.0,
            Admitted = new List<AdmittedChunkInfo>(),
            Evicted = new List<EvictedChunkInfo>(),
            BroadcastReceiverCount = 0,
            Entropy = 0.0,
            Summary = "no changes"
        };

        await logger.LogAsync(report);

        logger.BufferedReports.Should().HaveCount(1);
    }

    [Fact]
    public async Task StructuredTickLogger_BufferRespectsMaxSize()
    {
        var logger = new StructuredTickLogger(maxBufferedReports: 3);

        for (int i = 0; i < 5; i++)
        {
            await logger.LogAsync(new ConsciousAccessReport
            {
                TickNumber = i,
                Timestamp = DateTime.UtcNow,
                DurationMs = 1.0,
                Admitted = new List<AdmittedChunkInfo>(),
                Evicted = new List<EvictedChunkInfo>(),
                BroadcastReceiverCount = 0,
                Entropy = 0.0,
                Summary = ""
            });
        }

        logger.BufferedReports.Should().HaveCount(3);
    }

    [Fact]
    public async Task StructuredTickLogger_NullReport_ThrowsArgumentNullException()
    {
        var logger = new StructuredTickLogger();

        Func<Task> act = async () => await logger.LogAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion
}
