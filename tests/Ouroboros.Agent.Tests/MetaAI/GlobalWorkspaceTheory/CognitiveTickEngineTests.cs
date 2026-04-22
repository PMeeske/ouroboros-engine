#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using FluentAssertions;
using Moq;
using Ouroboros.Agent.MetaAI.GlobalWorkspaceTheory;
using Xunit;

namespace Ouroboros.Tests.MetaAI.GlobalWorkspaceTheory;

[Trait("Category", "Unit")]
public sealed class CognitiveTickEngineTests
{
    #region Lifecycle

    [Fact]
    public void Constructor_SetsProperties()
    {
        var workspace = new GwtWorkspace();
        var competition = new CompetitionEngine();
        var bus = new BroadcastBus();
        var producers = new List<ICandidateProducer>();
        var entropy = new EntropyCalculator();

        using var engine = new CognitiveTickEngine(workspace, competition, bus, producers, entropy);

        engine.CurrentTickNumber.Should().Be(0);
        engine.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void Constructor_NullWorkspace_ThrowsArgumentNullException()
    {
        Action act = () => new CognitiveTickEngine(
            null!, new CompetitionEngine(), new BroadcastBus(), Array.Empty<ICandidateProducer>(), new EntropyCalculator());

        act.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("workspace");
    }

    [Fact]
    public void Constructor_NullCompetition_ThrowsArgumentNullException()
    {
        Action act = () => new CognitiveTickEngine(
            new GwtWorkspace(), null!, new BroadcastBus(), Array.Empty<ICandidateProducer>(), new EntropyCalculator());

        act.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("competition");
    }

    [Fact]
    public void Constructor_NullBus_ThrowsArgumentNullException()
    {
        Action act = () => new CognitiveTickEngine(
            new GwtWorkspace(), new CompetitionEngine(), null!, Array.Empty<ICandidateProducer>(), new EntropyCalculator());

        act.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("broadcastBus");
    }

    [Fact]
    public void Constructor_NullProducers_ThrowsArgumentNullException()
    {
        Action act = () => new CognitiveTickEngine(
            new GwtWorkspace(), new CompetitionEngine(), new BroadcastBus(), null!, new EntropyCalculator());

        act.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("producers");
    }

    [Fact]
    public void Constructor_NullEntropyCalculator_ThrowsArgumentNullException()
    {
        Action act = () => new CognitiveTickEngine(
            new GwtWorkspace(), new CompetitionEngine(), new BroadcastBus(), Array.Empty<ICandidateProducer>(), null!);

        act.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("entropyCalculator");
    }

    [Fact]
    public async Task StartAsync_SetsIsRunning()
    {
        using var engine = CreateEngine();

        await engine.StartAsync();

        engine.IsRunning.Should().BeTrue();
        await engine.StopAsync();
    }

    [Fact]
    public async Task StartAsync_DoubleStart_IsIdempotent()
    {
        using var engine = CreateEngine();

        await engine.StartAsync();
        await engine.StartAsync();

        engine.IsRunning.Should().BeTrue();
        await engine.StopAsync();
    }

    [Fact]
    public async Task StopAsync_SetsNotRunning()
    {
        using var engine = CreateEngine();
        await engine.StartAsync();

        await engine.StopAsync();

        engine.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task StopAsync_WithoutStart_DoesNotThrow()
    {
        using var engine = CreateEngine();

        await engine.StopAsync();

        engine.IsRunning.Should().BeFalse();
    }

    #endregion

    #region TickOnceAsync

    [Fact]
    public async Task TickOnceAsync_ProducesCandidates_AndBroadcasts()
    {
        var workspace = new GwtWorkspace(3);
        var bus = new BroadcastBus();
        var receiver = new TestReceiver("R1");
        bus.Register(receiver);

        var producer = new Mock<ICandidateProducer>();
        producer.Setup(p => p.ProduceCandidatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Candidate> { new("hello", 0.8, 0.8, 0.8, 0.8, "Chat") });

        using var engine = new CognitiveTickEngine(
            workspace, new CompetitionEngine(), bus, new[] { producer.Object }, new EntropyCalculator());

        await engine.TickOnceAsync();

        workspace.Chunks.Should().HaveCount(1);
        receiver.ReceivedChunks.Should().HaveCount(1);
        engine.CurrentTickNumber.Should().Be(1);
    }

    [Fact]
    public async Task TickOnceAsync_WithTickLogger_LogsReport()
    {
        var workspace = new GwtWorkspace();
        var logger = new StructuredTickLogger();

        var producer = new Mock<ICandidateProducer>();
        producer.Setup(p => p.ProduceCandidatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Candidate> { new("x", 0.5, 0.5, 0.5, 0.5, "Src") });

        using var engine = new CognitiveTickEngine(
            workspace, new CompetitionEngine(), new BroadcastBus(), new[] { producer.Object },
            new EntropyCalculator(), tickLogger: logger);

        await engine.TickOnceAsync();

        logger.BufferedReports.Should().HaveCount(1);
    }

    [Fact]
    public async Task TickOnceAsync_WithEventStore_PersistsTick()
    {
        var workspace = new GwtWorkspace();
        var store = new InMemoryTickEventStore();

        var producer = new Mock<ICandidateProducer>();
        producer.Setup(p => p.ProduceCandidatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Candidate> { new("x", 0.5, 0.5, 0.5, 0.5, "Src") });

        using var engine = new CognitiveTickEngine(
            workspace, new CompetitionEngine(), new BroadcastBus(), new[] { producer.Object },
            new EntropyCalculator(), eventStore: store);

        await engine.TickOnceAsync();

        IReadOnlyList<TickEvent> events = await store.ReadRangeAsync(0, 100);
        events.Should().HaveCount(1);
        events[0].TickNumber.Should().Be(1);
    }

    [Fact]
    public async Task TickOnceAsync_WithDriveInfluencer_UsesAdjustedSalience()
    {
        var workspace = new GwtWorkspace(2);
        var scorer = new SalienceScorer();
        var entropyCalc = new EntropyCalculator();
        var drive = new IntrinsicDrive { ExplorationBonus = 0.5 };
        var influencer = new DriveInfluencer(scorer, entropyCalc, drive);

        var producer = new Mock<ICandidateProducer>();
        producer.Setup(p => p.ProduceCandidatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Candidate> { new("x", 0.1, 0.1, 0.1, 0.1, "Src") });

        using var engine = new CognitiveTickEngine(
            workspace, new CompetitionEngine(), new BroadcastBus(), new[] { producer.Object },
            entropyCalc, driveInfluencer: influencer);

        await engine.TickOnceAsync();

        workspace.Chunks.Should().HaveCount(1);
    }

    [Fact]
    public async Task TickOnceAsync_NoCandidates_Works()
    {
        var workspace = new GwtWorkspace();
        var producer = new Mock<ICandidateProducer>();
        producer.Setup(p => p.ProduceCandidatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Candidate>());

        using var engine = new CognitiveTickEngine(
            workspace, new CompetitionEngine(), new BroadcastBus(), new[] { producer.Object }, new EntropyCalculator());

        await engine.TickOnceAsync();

        workspace.Chunks.Should().BeEmpty();
    }

    [Fact]
    public async Task TickOnceAsync_ProducerThrows_Continues()
    {
        var workspace = new GwtWorkspace();
        var producer = new Mock<ICandidateProducer>();
        producer.Setup(p => p.ProduceCandidatesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("fail"));

        using var engine = new CognitiveTickEngine(
            workspace, new CompetitionEngine(), new BroadcastBus(), new[] { producer.Object }, new EntropyCalculator());

        Func<Task> act = async () => await engine.TickOnceAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task TickOnceAsync_RespectsCancellation()
    {
        using var engine = CreateEngine();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        Func<Task> act = async () => await engine.TickOnceAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region TickEvent

    [Fact]
    public void TickEvent_FromReportAndSnapshot_MapsCorrectly()
    {
        var report = new ConsciousAccessReport
        {
            TickNumber = 5,
            Timestamp = DateTime.UtcNow,
            DurationMs = 15.0,
            Admitted = new List<AdmittedChunkInfo>(),
            Evicted = new List<EvictedChunkInfo>(),
            BroadcastReceiverCount = 3,
            Entropy = 0.5,
            Summary = "test"
        };
        var snapshot = new WorkspaceSnapshot(new List<WorkspaceChunk>(), 5, DateTime.UtcNow);

        TickEvent tick = TickEvent.FromReportAndSnapshot(5, report, snapshot);

        tick.TickNumber.Should().Be(5);
        tick.BroadcastReceiverCount.Should().Be(3);
        tick.Entropy.Should().Be(0.5);
        tick.WorkspaceChunks.Should().BeEmpty();
    }

    #endregion

    #region Helpers

    private static CognitiveTickEngine CreateEngine()
    {
        return new CognitiveTickEngine(
            new GwtWorkspace(),
            new CompetitionEngine(),
            new BroadcastBus(),
            Array.Empty<ICandidateProducer>(),
            new EntropyCalculator());
    }

    private sealed class TestReceiver : IBroadcastReceiver
    {
        public string ReceiverName { get; }
        public List<WorkspaceChunk> ReceivedChunks { get; } = new();

        public TestReceiver(string name) => ReceiverName = name;

        public Task OnBroadcastAsync(IReadOnlyList<WorkspaceChunk> chunks, CancellationToken ct = default)
        {
            ReceivedChunks.AddRange(chunks);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryTickEventStore : ITickEventStore
    {
        private readonly List<TickEvent> _events = new();

        public Task AppendAsync(TickEvent tickEvent, CancellationToken ct = default)
        {
            _events.Add(tickEvent);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<TickEvent>> ReadRangeAsync(long fromTick, long toTick, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<TickEvent>>(
                _events.Where(e => e.TickNumber >= fromTick && e.TickNumber <= toTick)
                       .OrderBy(e => e.TickNumber)
                       .ToList());
        }
    }

    #endregion
}
