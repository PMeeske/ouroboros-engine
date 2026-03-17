using FluentAssertions;
using Ouroboros.Pipeline.MultiAgent;

namespace Ouroboros.Tests.MultiAgent;

[Trait("Category", "Unit")]
public sealed class AgentMessageTests
{
    private readonly Guid _senderId = Guid.NewGuid();
    private readonly Guid _receiverId = Guid.NewGuid();

    [Fact]
    public void CreateRequest_WithValidParams_ReturnsRequestMessage()
    {
        // Act
        var message = AgentMessage.CreateRequest(_senderId, _receiverId, "test.topic", "payload");

        // Assert
        message.Id.Should().NotBeEmpty();
        message.SenderId.Should().Be(_senderId);
        message.ReceiverId.Should().Be(_receiverId);
        message.Type.Should().Be(MessageType.Request);
        message.Priority.Should().Be(MessagePriority.Normal);
        message.Topic.Should().Be("test.topic");
        message.Payload.Should().Be("payload");
        message.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        message.CorrelationId.Should().Be(message.Id);
    }

    [Fact]
    public void CreateRequest_WithNullTopic_ThrowsArgumentNullException()
    {
        Action act = () => AgentMessage.CreateRequest(_senderId, _receiverId, null!, "payload");
        act.Should().Throw<ArgumentNullException>().WithParameterName("topic");
    }

    [Fact]
    public void CreateRequest_WithNullPayload_ThrowsArgumentNullException()
    {
        Action act = () => AgentMessage.CreateRequest(_senderId, _receiverId, "topic", null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("payload");
    }

    [Fact]
    public void CreateResponse_WithValidRequest_ReturnsResponseMessage()
    {
        // Arrange
        var request = AgentMessage.CreateRequest(_senderId, _receiverId, "test.topic", "request-data");

        // Act
        var response = AgentMessage.CreateResponse(request, "response-data");

        // Assert
        response.Type.Should().Be(MessageType.Response);
        response.SenderId.Should().Be(_receiverId);
        response.ReceiverId.Should().Be(_senderId);
        response.Topic.Should().Be("test.topic");
        response.Payload.Should().Be("response-data");
        response.CorrelationId.Should().Be(request.CorrelationId);
        response.Priority.Should().Be(request.Priority);
    }

    [Fact]
    public void CreateResponse_WithNonRequestMessage_ThrowsArgumentException()
    {
        // Arrange
        var broadcast = AgentMessage.CreateBroadcast(_senderId, "topic", "data");

        // Act
        Action act = () => AgentMessage.CreateResponse(broadcast, "response");

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("request");
    }

    [Fact]
    public void CreateResponse_WithNullRequest_ThrowsArgumentNullException()
    {
        Action act = () => AgentMessage.CreateResponse(null!, "payload");
        act.Should().Throw<ArgumentNullException>().WithParameterName("request");
    }

    [Fact]
    public void CreateResponse_WithNullPayload_ThrowsArgumentNullException()
    {
        var request = AgentMessage.CreateRequest(_senderId, _receiverId, "topic", "data");
        Action act = () => AgentMessage.CreateResponse(request, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("payload");
    }

    [Fact]
    public void CreateBroadcast_WithValidParams_ReturnsBroadcastMessage()
    {
        // Act
        var message = AgentMessage.CreateBroadcast(_senderId, "events", "data");

        // Assert
        message.Type.Should().Be(MessageType.Broadcast);
        message.ReceiverId.Should().BeNull();
        message.Priority.Should().Be(MessagePriority.Normal);
        message.CorrelationId.Should().BeNull();
    }

    [Fact]
    public void CreateNotification_WithReceiver_ReturnsTargetedNotification()
    {
        // Act
        var message = AgentMessage.CreateNotification(_senderId, "notify", "info", _receiverId);

        // Assert
        message.Type.Should().Be(MessageType.Notification);
        message.ReceiverId.Should().Be(_receiverId);
        message.CorrelationId.Should().BeNull();
    }

    [Fact]
    public void CreateNotification_WithoutReceiver_ReturnsBroadcastNotification()
    {
        // Act
        var message = AgentMessage.CreateNotification(_senderId, "notify", "info");

        // Assert
        message.Type.Should().Be(MessageType.Notification);
        message.ReceiverId.Should().BeNull();
    }

    [Fact]
    public void CreateError_ReturnsHighPriorityErrorMessage()
    {
        // Act
        var message = AgentMessage.CreateError(_senderId, _receiverId, "errors", "Something went wrong");

        // Assert
        message.Type.Should().Be(MessageType.Error);
        message.Priority.Should().Be(MessagePriority.High);
        message.Payload.Should().Be("Something went wrong");
    }

    [Fact]
    public void CreateError_WithNullReceiver_ReturnsMessage()
    {
        // Act
        var message = AgentMessage.CreateError(_senderId, null, "errors", "error msg");

        // Assert
        message.ReceiverId.Should().BeNull();
    }

    [Fact]
    public void IsRequest_WhenTypeIsRequest_ReturnsTrue()
    {
        var message = AgentMessage.CreateRequest(_senderId, _receiverId, "topic", "data");
        message.IsRequest.Should().BeTrue();
    }

    [Fact]
    public void IsRequest_WhenTypeIsNotRequest_ReturnsFalse()
    {
        var message = AgentMessage.CreateBroadcast(_senderId, "topic", "data");
        message.IsRequest.Should().BeFalse();
    }

    [Fact]
    public void IsBroadcast_WhenTypeIsBroadcast_ReturnsTrue()
    {
        var message = AgentMessage.CreateBroadcast(_senderId, "topic", "data");
        message.IsBroadcast.Should().BeTrue();
    }

    [Fact]
    public void IsBroadcast_WhenReceiverIsNull_ReturnsTrue()
    {
        var message = AgentMessage.CreateNotification(_senderId, "topic", "data");
        message.IsBroadcast.Should().BeTrue();
    }

    [Fact]
    public void IsBroadcast_WhenHasReceiver_ReturnsFalse()
    {
        var message = AgentMessage.CreateRequest(_senderId, _receiverId, "topic", "data");
        message.IsBroadcast.Should().BeFalse();
    }
}
