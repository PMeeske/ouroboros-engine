namespace Ouroboros.Tests.Pipeline.Council.Agents;

using Ouroboros.Pipeline.Council.Agents;

[Trait("Category", "Unit")]
public class OptimistAgentTests
{
    [Fact]
    public void Name_ReturnsOptimist()
    {
        var agent = new OptimistAgent();
        agent.Name.Should().Be("Optimist");
    }

    [Fact]
    public void Description_IsNotEmpty()
    {
        var agent = new OptimistAgent();
        agent.Description.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ExpertiseWeight_IsPointNine()
    {
        var agent = new OptimistAgent();
        agent.ExpertiseWeight.Should().Be(0.9);
    }

    [Fact]
    public void SystemPrompt_ContainsOptimistRole()
    {
        var agent = new OptimistAgent();
        agent.SystemPrompt.Should().Contain("Optimist");
    }
}
