using Ouroboros.Pipeline.Council;

namespace Ouroboros.Tests.Council;

[Trait("Category", "Unit")]
public class CouncilDecisionEventTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var topic = CouncilTopic.Simple("Test question");
        var decision = CouncilDecision.Failed("test");
        var timestamp = DateTime.UtcNow;

        // Act
        var evt = new CouncilDecisionEvent(id, topic, decision, timestamp);

        // Assert
        evt.Id.Should().Be(id);
        evt.Topic.Should().Be(topic);
        evt.Decision.Should().Be(decision);
        evt.Timestamp.Should().Be(timestamp);
    }

    [Fact]
    public void Create_GeneratesNewIdAndTimestamp()
    {
        // Arrange
        var topic = CouncilTopic.Simple("Test question");
        var decision = CouncilDecision.Failed("test");
        var beforeCreate = DateTime.UtcNow;

        // Act
        var evt = CouncilDecisionEvent.Create(topic, decision);

        // Assert
        evt.Id.Should().NotBe(Guid.Empty);
        evt.Topic.Should().Be(topic);
        evt.Decision.Should().Be(decision);
        evt.Timestamp.Should().BeOnOrAfter(beforeCreate);
    }

    [Fact]
    public void Create_GeneratesUniqueIds()
    {
        // Arrange
        var topic = CouncilTopic.Simple("Test");
        var decision = CouncilDecision.Failed("test");

        // Act
        var evt1 = CouncilDecisionEvent.Create(topic, decision);
        var evt2 = CouncilDecisionEvent.Create(topic, decision);

        // Assert
        evt1.Id.Should().NotBe(evt2.Id);
    }

    [Fact]
    public void InheritsFromPipelineEvent_WithCouncilDecisionType()
    {
        // Arrange
        var topic = CouncilTopic.Simple("Test");
        var decision = CouncilDecision.Failed("test");

        // Act
        var evt = CouncilDecisionEvent.Create(topic, decision);

        // Assert
        evt.Should().BeAssignableTo<PipelineEvent>();
    }
}
