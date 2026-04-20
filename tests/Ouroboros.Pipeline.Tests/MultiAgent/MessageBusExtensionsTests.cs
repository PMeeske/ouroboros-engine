using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Ouroboros.Pipeline.MultiAgent;

namespace Ouroboros.Tests.MultiAgent;

[Trait("Category", "Unit")]
public sealed class MessageBusExtensionsTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();

    #region TryPublishAsync

    [Fact]
    public async Task TryPublishAsync_WhenSuccessful_ReturnsSuccess()
    {
        // Arrange
        _bus.PublishAsync(Arg.Any<AgentMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var message = AgentMessage.CreateBroadcast(Guid.NewGuid(), "topic", "data");

        // Act
        var result = await _bus.TryPublishAsync(message);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task TryPublishAsync_WhenException_ReturnsFailure()
    {
        // Arrange
        _bus.PublishAsync(Arg.Any<AgentMessage>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Bus error"));
        var message = AgentMessage.CreateBroadcast(Guid.NewGuid(), "topic", "data");

        // Act
        var result = await _bus.TryPublishAsync(message);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task TryPublishAsync_WithNullBus_ThrowsArgumentNullException()
    {
        IMessageBus nullBus = null!;
        var message = AgentMessage.CreateBroadcast(Guid.NewGuid(), "topic", "data");
        Func<Task> act = () => nullBus.TryPublishAsync(message);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("bus");
    }

    [Fact]
    public async Task TryPublishAsync_WithNullMessage_ThrowsArgumentNullException()
    {
        Func<Task> act = () => _bus.TryPublishAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("message");
    }

    #endregion

    #region TryRequestAsync

    [Fact]
    public async Task TryRequestAsync_WhenSuccessful_ReturnsResponse()
    {
        // Arrange
        var senderId = Guid.NewGuid();
        var receiverId = Guid.NewGuid();
        var request = AgentMessage.CreateRequest(senderId, receiverId, "topic", "data");
        var response = AgentMessage.CreateResponse(request, "response-data");

        _bus.RequestAsync(Arg.Any<AgentMessage>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(response));

        // Act
        var result = await _bus.TryRequestAsync(request, TimeSpan.FromSeconds(5));

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task TryRequestAsync_WhenTimesOut_ReturnsFailure()
    {
        // Arrange
        var request = AgentMessage.CreateRequest(Guid.NewGuid(), Guid.NewGuid(), "topic", "data");

        _bus.RequestAsync(Arg.Any<AgentMessage>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("Timed out"));

        // Act
        var result = await _bus.TryRequestAsync(request, TimeSpan.FromSeconds(1));

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task TryRequestAsync_WithNullBus_ThrowsArgumentNullException()
    {
        IMessageBus nullBus = null!;
        var request = AgentMessage.CreateRequest(Guid.NewGuid(), Guid.NewGuid(), "topic", "data");
        Func<Task> act = () => nullBus.TryRequestAsync(request, TimeSpan.FromSeconds(1));
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("bus");
    }

    #endregion

    #region BroadcastAsync

    [Fact]
    public async Task BroadcastAsync_PublishesBroadcastMessage()
    {
        // Arrange
        _bus.PublishAsync(Arg.Any<AgentMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        await _bus.BroadcastAsync(Guid.NewGuid(), "events", "data");

        // Assert
        await _bus.Received(1).PublishAsync(
            Arg.Is<AgentMessage>(m => m.Type == MessageType.Broadcast && m.Topic == "events"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BroadcastAsync_WithNullBus_ThrowsArgumentNullException()
    {
        IMessageBus nullBus = null!;
        Func<Task> act = () => nullBus.BroadcastAsync(Guid.NewGuid(), "topic", "data");
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("bus");
    }

    [Fact]
    public async Task BroadcastAsync_WithNullTopic_ThrowsArgumentNullException()
    {
        Func<Task> act = () => _bus.BroadcastAsync(Guid.NewGuid(), null!, "data");
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("topic");
    }

    #endregion

    #region NotifyAsync

    [Fact]
    public async Task NotifyAsync_PublishesNotificationMessage()
    {
        // Arrange
        _bus.PublishAsync(Arg.Any<AgentMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var receiverId = Guid.NewGuid();

        // Act
        await _bus.NotifyAsync(Guid.NewGuid(), "status", "update", receiverId);

        // Assert
        await _bus.Received(1).PublishAsync(
            Arg.Is<AgentMessage>(m => m.Type == MessageType.Notification && m.Topic == "status"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyAsync_WithoutReceiver_PublishesBroadcastNotification()
    {
        // Arrange
        _bus.PublishAsync(Arg.Any<AgentMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        await _bus.NotifyAsync(Guid.NewGuid(), "status", "update");

        // Assert
        await _bus.Received(1).PublishAsync(
            Arg.Is<AgentMessage>(m => m.ReceiverId == null),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region SubscribeTyped

    [Fact]
    public void SubscribeTyped_SubscribesWithTypedHandler()
    {
        // Arrange
        _bus.Subscribe(Arg.Any<Guid>(), Arg.Any<string?>(), Arg.Any<Func<AgentMessage, Task>>())
            .Returns(callInfo =>
            {
                return new Subscription(Guid.NewGuid(), callInfo.ArgAt<Guid>(0),
                    callInfo.ArgAt<string?>(1), callInfo.ArgAt<Func<AgentMessage, Task>>(2));
            });

        var agentId = Guid.NewGuid();

        // Act
        var subscription = _bus.SubscribeTyped<string>(agentId, "topic",
            (msg, payload) => Task.CompletedTask);

        // Assert
        subscription.Should().NotBeNull();
        subscription.AgentId.Should().Be(agentId);
    }

    [Fact]
    public void SubscribeTyped_WithNullBus_ThrowsArgumentNullException()
    {
        IMessageBus nullBus = null!;
        Action act = () => nullBus.SubscribeTyped<string>(Guid.NewGuid(), "topic",
            (msg, payload) => Task.CompletedTask);
        act.Should().Throw<ArgumentNullException>().WithParameterName("bus");
    }

    [Fact]
    public void SubscribeTyped_WithNullHandler_ThrowsArgumentNullException()
    {
        Action act = () => _bus.SubscribeTyped<string>(Guid.NewGuid(), "topic", null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("handler");
    }

    #endregion
}
