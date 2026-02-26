namespace Ouroboros.Tests.Pipeline.MultiAgent;

using Ouroboros.Pipeline.MultiAgent;

[Trait("Category", "Unit")]
public class SubscriptionTests
{
    [Fact]
    public void Matches_ReturnsTrueForTargetedMessage()
    {
        var agentId = Guid.NewGuid();
        var sub = new Subscription(Guid.NewGuid(), agentId, null, _ => Task.CompletedTask);

        var msg = AgentMessage.CreateNotification(
            Guid.NewGuid(), "topic", "content", receiverId: agentId);

        sub.Matches(msg).Should().BeTrue();
    }

    [Fact]
    public void Matches_ReturnsTrueForBroadcast()
    {
        var agentId = Guid.NewGuid();
        var sub = new Subscription(Guid.NewGuid(), agentId, null, _ => Task.CompletedTask);

        var msg = AgentMessage.CreateBroadcast(Guid.NewGuid(), "topic", "content");

        sub.Matches(msg).Should().BeTrue();
    }

    [Fact]
    public void Matches_ReturnsFalseForDifferentTargetAgent()
    {
        var sub = new Subscription(Guid.NewGuid(), Guid.NewGuid(), null, _ => Task.CompletedTask);
        var msg = AgentMessage.CreateNotification(
            Guid.NewGuid(), "topic", "content", receiverId: Guid.NewGuid());

        sub.Matches(msg).Should().BeFalse();
    }

    [Fact]
    public void Matches_FiltersByTopic()
    {
        var agentId = Guid.NewGuid();
        var sub = new Subscription(Guid.NewGuid(), agentId, "specific-topic", _ => Task.CompletedTask);

        var match = AgentMessage.CreateNotification(
            Guid.NewGuid(), "specific-topic", "content", receiverId: agentId);
        var noMatch = AgentMessage.CreateNotification(
            Guid.NewGuid(), "other-topic", "content", receiverId: agentId);

        sub.Matches(match).Should().BeTrue();
        sub.Matches(noMatch).Should().BeFalse();
    }
}
