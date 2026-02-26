namespace Ouroboros.Tests.Pipeline.MultiAgent;

using Ouroboros.Pipeline.MultiAgent;

[Trait("Category", "Unit")]
public class AgentMessageTests
{
    [Fact]
    public void CreateRequest_SetsCorrectTypeAndCorrelation()
    {
        var sender = Guid.NewGuid();
        var receiver = Guid.NewGuid();
        var msg = AgentMessage.CreateRequest(sender, receiver, "help", "payload");

        msg.Type.Should().Be(MessageType.Request);
        msg.SenderId.Should().Be(sender);
        msg.ReceiverId.Should().Be(receiver);
        msg.Topic.Should().Be("help");
        msg.IsRequest.Should().BeTrue();
        msg.CorrelationId.Should().Be(msg.Id);
    }

    [Fact]
    public void CreateResponse_CorrelatesWithRequest()
    {
        var sender = Guid.NewGuid();
        var receiver = Guid.NewGuid();
        var request = AgentMessage.CreateRequest(sender, receiver, "help", "request");
        var response = AgentMessage.CreateResponse(request, "response");

        response.Type.Should().Be(MessageType.Response);
        response.CorrelationId.Should().Be(request.CorrelationId);
        response.SenderId.Should().Be(receiver);
        response.ReceiverId.Should().Be(sender);
    }

    [Fact]
    public void CreateResponse_ThrowsForNonRequestMessage()
    {
        var broadcast = AgentMessage.CreateBroadcast(Guid.NewGuid(), "topic", "data");
        var act = () => AgentMessage.CreateResponse(broadcast, "reply");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateBroadcast_HasNullReceiver()
    {
        var msg = AgentMessage.CreateBroadcast(Guid.NewGuid(), "topic", "data");

        msg.Type.Should().Be(MessageType.Broadcast);
        msg.ReceiverId.Should().BeNull();
        msg.IsBroadcast.Should().BeTrue();
    }

    [Fact]
    public void CreateNotification_DoesNotExpectResponse()
    {
        var msg = AgentMessage.CreateNotification(Guid.NewGuid(), "update", "data");

        msg.Type.Should().Be(MessageType.Notification);
        msg.CorrelationId.Should().BeNull();
    }

    [Fact]
    public void CreateError_HasHighPriority()
    {
        var msg = AgentMessage.CreateError(Guid.NewGuid(), null, "errors", "Something failed");

        msg.Type.Should().Be(MessageType.Error);
        msg.Priority.Should().Be(MessagePriority.High);
    }
}
