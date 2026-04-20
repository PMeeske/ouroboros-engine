using FluentAssertions;
using Ouroboros.Pipeline.MultiAgent;

namespace Ouroboros.Tests.MultiAgent;

[Trait("Category", "Unit")]
public sealed class SubscriptionTests
{
    private readonly Guid _agentId = Guid.NewGuid();

    private Subscription CreateSubscription(string? topicFilter = null)
    {
        return new Subscription(Guid.NewGuid(), _agentId, topicFilter, _ => Task.CompletedTask);
    }

    [Fact]
    public void Matches_WhenTargetedAtAgent_ReturnsTrue()
    {
        // Arrange
        var subscription = CreateSubscription();
        var message = AgentMessage.CreateRequest(Guid.NewGuid(), _agentId, "topic", "data");

        // Act & Assert
        subscription.Matches(message).Should().BeTrue();
    }

    [Fact]
    public void Matches_WhenBroadcast_ReturnsTrue()
    {
        // Arrange
        var subscription = CreateSubscription();
        var message = AgentMessage.CreateBroadcast(Guid.NewGuid(), "topic", "data");

        // Act & Assert
        subscription.Matches(message).Should().BeTrue();
    }

    [Fact]
    public void Matches_WhenTargetedAtOtherAgent_ReturnsFalse()
    {
        // Arrange
        var subscription = CreateSubscription();
        var message = AgentMessage.CreateRequest(Guid.NewGuid(), Guid.NewGuid(), "topic", "data");

        // Act & Assert
        subscription.Matches(message).Should().BeFalse();
    }

    [Fact]
    public void Matches_WithTopicFilter_MatchesCorrectTopic()
    {
        // Arrange
        var subscription = CreateSubscription("events");
        var matching = AgentMessage.CreateNotification(Guid.NewGuid(), "events", "data", _agentId);
        var nonMatching = AgentMessage.CreateNotification(Guid.NewGuid(), "other", "data", _agentId);

        // Act & Assert
        subscription.Matches(matching).Should().BeTrue();
        subscription.Matches(nonMatching).Should().BeFalse();
    }

    [Fact]
    public void Matches_WithTopicFilter_IsCaseInsensitive()
    {
        // Arrange
        var subscription = CreateSubscription("Events");
        var message = AgentMessage.CreateNotification(Guid.NewGuid(), "events", "data", _agentId);

        // Act & Assert
        subscription.Matches(message).Should().BeTrue();
    }

    [Fact]
    public void Matches_WithNullTopicFilter_MatchesAllTopics()
    {
        // Arrange
        var subscription = CreateSubscription(null);
        var message = AgentMessage.CreateNotification(Guid.NewGuid(), "any-topic", "data", _agentId);

        // Act & Assert
        subscription.Matches(message).Should().BeTrue();
    }

    [Fact]
    public void Matches_WithNullMessage_ThrowsArgumentNullException()
    {
        var subscription = CreateSubscription();
        Action act = () => subscription.Matches(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("message");
    }
}
