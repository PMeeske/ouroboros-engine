using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Ouroboros.Pipeline.MultiAgent;
using Xunit;

namespace Ouroboros.Tests.Pipeline.MultiAgent;

[Trait("Category", "Unit")]
public sealed class MessageBusExtensionsTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();

    private static AgentMessage CreateTestMessage()
        => AgentMessage.CreateNotification(Guid.NewGuid(), "test.topic", "payload");

    [Fact]
    public async Task TryPublishAsync_Success_ReturnsSuccess()
    {
        var message = CreateTestMessage();
        _bus.PublishAsync(message, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var result = await _bus.TryPublishAsync(message);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task TryPublishAsync_BusThrows_ReturnsFailure()
    {
        var message = CreateTestMessage();
        _bus.PublishAsync(message, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("bus down"));

        var result = await _bus.TryPublishAsync(message);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("bus down");
    }

    [Fact]
    public async Task TryPublishAsync_NullBus_Throws()
    {
        IMessageBus? nullBus = null;

        var act = () => nullBus!.TryPublishAsync(CreateTestMessage());

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task TryPublishAsync_NullMessage_Throws()
    {
        var act = () => _bus.TryPublishAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task TryRequestAsync_Success_ReturnsResponse()
    {
        var request = AgentMessage.CreateRequest(Guid.NewGuid(), Guid.NewGuid(), "req.topic", "data");
        var response = AgentMessage.CreateNotification(Guid.NewGuid(), "resp.topic", "reply");
        _bus.RequestAsync(request, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(response);

        var result = await _bus.TryRequestAsync(request, TimeSpan.FromSeconds(5));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task TryRequestAsync_Timeout_ReturnsFailure()
    {
        var request = AgentMessage.CreateRequest(Guid.NewGuid(), Guid.NewGuid(), "req.topic", "data");
        _bus.RequestAsync(request, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("timed out"));

        var result = await _bus.TryRequestAsync(request, TimeSpan.FromSeconds(1));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("timed out");
    }

    [Fact]
    public async Task TryRequestAsync_GenericException_ReturnsFailure()
    {
        var request = AgentMessage.CreateRequest(Guid.NewGuid(), Guid.NewGuid(), "req.topic", "data");
        _bus.RequestAsync(request, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));

        var result = await _bus.TryRequestAsync(request, TimeSpan.FromSeconds(1));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Request failed");
    }

    [Fact]
    public async Task BroadcastAsync_PublishesBroadcastMessage()
    {
        var senderId = Guid.NewGuid();
        _bus.PublishAsync(Arg.Any<AgentMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await _bus.BroadcastAsync(senderId, "broadcast.topic", "hello");

        await _bus.Received(1).PublishAsync(
            Arg.Is<AgentMessage>(m => m.Topic == "broadcast.topic"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyAsync_PublishesNotification()
    {
        var senderId = Guid.NewGuid();
        _bus.PublishAsync(Arg.Any<AgentMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await _bus.NotifyAsync(senderId, "notify.topic", "data");

        await _bus.Received(1).PublishAsync(
            Arg.Is<AgentMessage>(m => m.Topic == "notify.topic"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyAsync_WithReceiver_PublishesDirected()
    {
        var senderId = Guid.NewGuid();
        var receiverId = Guid.NewGuid();
        _bus.PublishAsync(Arg.Any<AgentMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await _bus.NotifyAsync(senderId, "notify.topic", "data", receiverId);

        await _bus.Received(1).PublishAsync(Arg.Any<AgentMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void SubscribeTyped_CallsSubscribe()
    {
        var agentId = Guid.NewGuid();
        _bus.Subscribe(agentId, "topic", Arg.Any<Func<AgentMessage, Task>>())
            .Returns(new Subscription(Guid.NewGuid(), agentId, "topic"));

        var sub = _bus.SubscribeTyped<string>(agentId, "topic", (_, _) => Task.CompletedTask);

        sub.Should().NotBeNull();
        _bus.Received(1).Subscribe(agentId, "topic", Arg.Any<Func<AgentMessage, Task>>());
    }

    [Fact]
    public async Task BroadcastAsync_NullTopic_Throws()
    {
        var act = () => _bus.BroadcastAsync(Guid.NewGuid(), null!, "payload");

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task NotifyAsync_NullTopic_Throws()
    {
        var act = () => _bus.NotifyAsync(Guid.NewGuid(), null!, "payload");

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
