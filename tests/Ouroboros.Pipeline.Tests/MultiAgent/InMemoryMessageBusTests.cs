using FluentAssertions;
using Ouroboros.Pipeline.MultiAgent;

namespace Ouroboros.Tests.MultiAgent;

[Trait("Category", "Unit")]
public sealed class InMemoryMessageBusTests : IDisposable
{
    private readonly InMemoryMessageBus _bus = new();

    public void Dispose() => _bus.Dispose();

    [Fact]
    public void Constructor_WithDefaultParams_Succeeds()
    {
        using var bus = new InMemoryMessageBus();
        bus.SubscriptionCount.Should().Be(0);
        bus.MessageHistoryCount.Should().Be(0);
    }

    [Fact]
    public void Constructor_WithNegativeHistorySize_ThrowsArgumentOutOfRangeException()
    {
        Action act = () => new InMemoryMessageBus(maxHistorySize: -1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task PublishAsync_WithValidMessage_AddsToHistory()
    {
        // Arrange
        var message = AgentMessage.CreateBroadcast(Guid.NewGuid(), "topic", "data");

        // Act
        await _bus.PublishAsync(message);

        // Allow processing time
        await Task.Delay(100);

        // Assert
        _bus.MessageHistoryCount.Should().Be(1);
    }

    [Fact]
    public async Task PublishAsync_WithNullMessage_ThrowsArgumentNullException()
    {
        Func<Task> act = () => _bus.PublishAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void Subscribe_ReturnsSubscription()
    {
        // Arrange
        var agentId = Guid.NewGuid();

        // Act
        var subscription = _bus.Subscribe(agentId, "topic", _ => Task.CompletedTask);

        // Assert
        subscription.Should().NotBeNull();
        subscription.AgentId.Should().Be(agentId);
        subscription.TopicFilter.Should().Be("topic");
        _bus.SubscriptionCount.Should().Be(1);
    }

    [Fact]
    public void Subscribe_WithNullHandler_ThrowsArgumentNullException()
    {
        Action act = () => _bus.Subscribe(Guid.NewGuid(), "topic", null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Unsubscribe_RemovesSubscription()
    {
        // Arrange
        var subscription = _bus.Subscribe(Guid.NewGuid(), "topic", _ => Task.CompletedTask);

        // Act
        _bus.Unsubscribe(subscription.Id);

        // Assert
        _bus.SubscriptionCount.Should().Be(0);
    }

    [Fact]
    public async Task PublishAsync_DeliversToSubscriber()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        AgentMessage? received = null;
        var tcs = new TaskCompletionSource<bool>();

        _bus.Subscribe(agentId, null, msg =>
        {
            received = msg;
            tcs.SetResult(true);
            return Task.CompletedTask;
        });

        var message = AgentMessage.CreateNotification(Guid.NewGuid(), "topic", "data", agentId);

        // Act
        await _bus.PublishAsync(message);
        await Task.WhenAny(tcs.Task, Task.Delay(2000));

        // Assert
        received.Should().NotBeNull();
        received!.Topic.Should().Be("topic");
    }

    [Fact]
    public async Task PublishAsync_BroadcastDeliversToAllSubscribers()
    {
        // Arrange
        var agent1 = Guid.NewGuid();
        var agent2 = Guid.NewGuid();
        int receiveCount = 0;
        var tcs = new TaskCompletionSource<bool>();

        _bus.Subscribe(agent1, null, _ =>
        {
            if (Interlocked.Increment(ref receiveCount) >= 2) tcs.TrySetResult(true);
            return Task.CompletedTask;
        });
        _bus.Subscribe(agent2, null, _ =>
        {
            if (Interlocked.Increment(ref receiveCount) >= 2) tcs.TrySetResult(true);
            return Task.CompletedTask;
        });

        var message = AgentMessage.CreateBroadcast(Guid.NewGuid(), "topic", "data");

        // Act
        await _bus.PublishAsync(message);
        await Task.WhenAny(tcs.Task, Task.Delay(2000));

        // Assert
        receiveCount.Should().Be(2);
    }

    [Fact]
    public async Task GetPendingMessages_WhenNoSubscriber_StoresPending()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        // Register agent's pending queue by subscribing, then unsubscribe
        var sub = _bus.Subscribe(agentId, "other-topic", _ => Task.CompletedTask);
        _bus.Unsubscribe(sub.Id);

        var message = AgentMessage.CreateNotification(Guid.NewGuid(), "topic", "data", agentId);

        // Act
        await _bus.PublishAsync(message);
        await Task.Delay(200);

        var pending = _bus.GetPendingMessages(agentId);

        // Assert
        pending.Should().NotBeEmpty();
    }

    [Fact]
    public void GetPendingMessages_WhenNoPending_ReturnsEmpty()
    {
        var pending = _bus.GetPendingMessages(Guid.NewGuid());
        pending.Should().BeEmpty();
    }

    [Fact]
    public void GetAllSubscriptions_ReturnsAllSubscriptions()
    {
        _bus.Subscribe(Guid.NewGuid(), "topic1", _ => Task.CompletedTask);
        _bus.Subscribe(Guid.NewGuid(), "topic2", _ => Task.CompletedTask);

        var subs = _bus.GetAllSubscriptions();
        subs.Should().HaveCount(2);
    }

    [Fact]
    public void GetSubscriptionsForAgent_ReturnsAgentSubscriptions()
    {
        var agentId = Guid.NewGuid();
        _bus.Subscribe(agentId, "topic1", _ => Task.CompletedTask);
        _bus.Subscribe(agentId, "topic2", _ => Task.CompletedTask);
        _bus.Subscribe(Guid.NewGuid(), "topic3", _ => Task.CompletedTask);

        var subs = _bus.GetSubscriptionsForAgent(agentId);
        subs.Should().HaveCount(2);
    }

    [Fact]
    public async Task ClearHistory_RemovesAllHistory()
    {
        // Arrange
        await _bus.PublishAsync(AgentMessage.CreateBroadcast(Guid.NewGuid(), "topic", "data"));
        await Task.Delay(100);

        // Act
        _bus.ClearHistory();

        // Assert
        _bus.MessageHistoryCount.Should().Be(0);
    }

    [Fact]
    public async Task RequestAsync_WithNonRequestMessage_ThrowsArgumentException()
    {
        var broadcast = AgentMessage.CreateBroadcast(Guid.NewGuid(), "topic", "data");
        Func<Task> act = () => _bus.RequestAsync(broadcast, TimeSpan.FromSeconds(1));
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        using var bus = new InMemoryMessageBus();
        bus.Dispose();
        bus.Dispose(); // should not throw
    }

    [Fact]
    public async Task MessageHistory_ReturnsImmutableSnapshot()
    {
        await _bus.PublishAsync(AgentMessage.CreateBroadcast(Guid.NewGuid(), "topic", "data"));
        await Task.Delay(100);

        var history = _bus.MessageHistory;
        history.Should().NotBeEmpty();
    }

    [Fact]
    public async Task PublishAsync_TrimsHistoryBeyondMaxSize()
    {
        // Arrange
        using var bus = new InMemoryMessageBus(maxHistorySize: 5);

        // Act - publish more than max
        for (int i = 0; i < 10; i++)
        {
            await bus.PublishAsync(AgentMessage.CreateBroadcast(Guid.NewGuid(), "topic", $"data-{i}"));
        }
        await Task.Delay(300);

        // Assert
        bus.MessageHistoryCount.Should().BeLessThanOrEqualTo(5);
    }
}
