namespace Ouroboros.Tests.Pipeline.Council;

using Ouroboros.Pipeline.Council;

[Trait("Category", "Unit")]
public class MinorityOpinionTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var concerns = new List<string> { "Concern1", "Concern2" };
        var opinion = new MinorityOpinion("SecurityCynic", "REJECT", "Too risky", concerns);

        opinion.AgentName.Should().Be("SecurityCynic");
        opinion.Position.Should().Be("REJECT");
        opinion.Rationale.Should().Be("Too risky");
        opinion.Concerns.Should().HaveCount(2);
    }
}
