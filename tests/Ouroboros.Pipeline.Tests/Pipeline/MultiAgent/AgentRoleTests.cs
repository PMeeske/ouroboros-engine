namespace Ouroboros.Tests.Pipeline.MultiAgent;

using Ouroboros.Pipeline.MultiAgent;

[Trait("Category", "Unit")]
public class AgentRoleTests
{
    [Fact]
    public void EnumHasExpectedCount()
    {
        Enum.GetValues<AgentRole>().Should().HaveCount(6);
    }

    [Theory]
    [InlineData(AgentRole.Analyst)]
    [InlineData(AgentRole.Coder)]
    [InlineData(AgentRole.Reviewer)]
    [InlineData(AgentRole.Planner)]
    [InlineData(AgentRole.Executor)]
    [InlineData(AgentRole.Specialist)]
    public void AllValues_AreDefined(AgentRole role)
    {
        Enum.IsDefined(role).Should().BeTrue();
    }
}
