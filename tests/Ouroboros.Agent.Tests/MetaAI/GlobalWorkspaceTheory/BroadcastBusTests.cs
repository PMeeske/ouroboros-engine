#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using Ouroboros.Agent.MetaAI.GlobalWorkspaceTheory;

namespace Ouroboros.Tests.MetaAI.GlobalWorkspaceTheory;

[Trait("Category", "Unit")]
public sealed class BroadcastBusTests
{
    [Fact]
    public void Register_NewReceiver_ReturnsTrue()
    {
        var bus = new BroadcastBus();
        var receiver = new TestReceiver("R1");

        bool added = bus.Register(receiver);

        added.Should().BeTrue();
        bus.ReceiverCount.Should().Be(1);
    }

    [Fact]
    public void Register_DuplicateReceiver_ReturnsFalse()
    {
        var bus = new BroadcastBus();
        var receiver = new TestReceiver("R1");
        bus.Register(receiver);

        bool added = bus.Register(receiver);

        added.Should().BeFalse();
        bus.ReceiverCount.Should().Be(1);
    }

    [Fact]
    public void Register_NullReceiver_ThrowsArgumentNullException()
    {
        var bus = new BroadcastBus();

        Action act = () => bus.Register(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Unregister_Existing_ReturnsTrue()
    {
        var bus = new BroadcastBus();
        var receiver = new TestReceiver("R1");
        bus.Register(receiver);

        bool removed = bus.Unregister("R1");

        removed.Should().BeTrue();
        bus.ReceiverCount.Should().Be(0);
    }

    [Fact]
    public void Unregister_Missing_ReturnsFalse()
    {
        var bus = new BroadcastBus();

        bool removed = bus.Unregister("R1");

        removed.Should().BeFalse();
    }

    [Fact]
    public void Unregister_NullName_ThrowsArgumentNullException()
    {
        var bus = new BroadcastBus();

        Action act = () => bus.Unregister(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task BroadcastAsync_NoReceivers_CompletesSilently()
    {
        var bus = new BroadcastBus();
        var chunks = new List<WorkspaceChunk>();

        await bus.BroadcastAsync(chunks);
    }

    [Fact]
    public async Task BroadcastAsync_SingleReceiver_ReceivesChunks()
    {
        var bus = new BroadcastBus();
        var receiver = new TestReceiver("R1");
        bus.Register(receiver);

        var chunks = new List<WorkspaceChunk>
        {
            new(new Candidate("msg", 0.5, 0.5, 0.5, 0.5, "Src"), DateTime.UtcNow, 0.5)
        };

        await bus.BroadcastAsync(chunks);

        receiver.ReceivedChunks.Should().HaveCount(1);
        receiver.ReceivedChunks[0].Candidate.Content.Should().Be("msg");
    }

    [Fact]
    public async Task BroadcastAsync_MultipleReceivers_AllReceive()
    {
        var bus = new BroadcastBus();
        var r1 = new TestReceiver("R1");
        var r2 = new TestReceiver("R2");
        bus.Register(r1);
        bus.Register(r2);

        var chunks = new List<WorkspaceChunk>
        {
            new(new Candidate("msg", 0.5, 0.5, 0.5, 0.5, "Src"), DateTime.UtcNow, 0.5)
        };

        await bus.BroadcastAsync(chunks);

        r1.ReceivedChunks.Should().HaveCount(1);
        r2.ReceivedChunks.Should().HaveCount(1);
    }

    [Fact]
    public async Task BroadcastAsync_ReceiverThrows_OtherReceiversStillGetBroadcast()
    {
        var bus = new BroadcastBus();
        var good = new TestReceiver("Good");
        var bad = new ThrowingReceiver("Bad");
        bus.Register(good);
        bus.Register(bad);

        var chunks = new List<WorkspaceChunk>
        {
            new(new Candidate("msg", 0.5, 0.5, 0.5, 0.5, "Src"), DateTime.UtcNow, 0.5)
        };

        await bus.BroadcastAsync(chunks);

        good.ReceivedChunks.Should().HaveCount(1);
    }

    [Fact]
    public async Task BroadcastAsync_Cancellation_StopsEarly()
    {
        var bus = new BroadcastBus();
        var receiver = new SlowReceiver("Slow");
        bus.Register(receiver);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var chunks = new List<WorkspaceChunk>
        {
            new(new Candidate("msg", 0.5, 0.5, 0.5, 0.5, "Src"), DateTime.UtcNow, 0.5)
        };

        await bus.BroadcastAsync(chunks, cts.Token);

        receiver.ReceivedChunks.Should().BeEmpty();
    }

    [Fact]
    public async Task BroadcastAsync_OperationCanceledException_Rethrows()
    {
        var bus = new BroadcastBus();
        var receiver = new CancelingReceiver("Canceler");
        bus.Register(receiver);

        using var cts = new CancellationTokenSource();
        var chunks = new List<WorkspaceChunk>
        {
            new(new Candidate("msg", 0.5, 0.5, 0.5, 0.5, "Src"), DateTime.UtcNow, 0.5)
        };

        Func<Task> act = async () => await bus.BroadcastAsync(chunks, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
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

    private sealed class ThrowingReceiver : IBroadcastReceiver
    {
        public string ReceiverName { get; }

        public ThrowingReceiver(string name) => ReceiverName = name;

        public Task OnBroadcastAsync(IReadOnlyList<WorkspaceChunk> chunks, CancellationToken ct = default)
            => throw new InvalidOperationException("boom");
    }

    private sealed class SlowReceiver : IBroadcastReceiver
    {
        public string ReceiverName { get; }
        public List<WorkspaceChunk> ReceivedChunks { get; } = new();

        public SlowReceiver(string name) => ReceiverName = name;

        public async Task OnBroadcastAsync(IReadOnlyList<WorkspaceChunk> chunks, CancellationToken ct = default)
        {
            await Task.Delay(10, ct).ConfigureAwait(false);
            ReceivedChunks.AddRange(chunks);
        }
    }

    private sealed class CancelingReceiver : IBroadcastReceiver
    {
        public string ReceiverName { get; }

        public CancelingReceiver(string name) => ReceiverName = name;

        public Task OnBroadcastAsync(IReadOnlyList<WorkspaceChunk> chunks, CancellationToken ct = default)
            => throw new OperationCanceledException(ct);
    }
}
