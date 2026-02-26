namespace Ouroboros.Tests.Pipeline.Council;

using Ouroboros.Pipeline.Council;

[Trait("Category", "Unit")]
public class CouncilDecisionEventTests
{
    [Fact]
    public void Create_GeneratesEventWithAutoId()
    {
        var topic = CouncilTopic.Simple("Test?");
        var decision = CouncilDecision.Failed("test");

        var evt = CouncilDecisionEvent.Create(topic, decision);

        evt.Id.Should().NotBe(Guid.Empty);
        evt.Topic.Should().BeSameAs(topic);
        evt.Decision.Should().BeSameAs(decision);
        evt.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }
}
