using FluentAssertions;
using Ouroboros.Agent.Cognition.Planning;
using Xunit;

namespace Ouroboros.Tests.Cognition.Planning;

[Trait("Category", "Unit")]
public class GovernanceOutcomeTests
{
    [Theory]
    [InlineData(GovernanceOutcome.Approved)]
    [InlineData(GovernanceOutcome.ApprovedByHuman)]
    [InlineData(GovernanceOutcome.DeniedByEthics)]
    [InlineData(GovernanceOutcome.DeniedBySafety)]
    [InlineData(GovernanceOutcome.DeniedByHuman)]
    [InlineData(GovernanceOutcome.TimedOut)]
    public void AllValues_AreDefined(GovernanceOutcome outcome)
    {
        Enum.IsDefined(outcome).Should().BeTrue();
    }

    [Fact]
    public void HasSixValues()
    {
        Enum.GetValues<GovernanceOutcome>().Should().HaveCount(6);
    }
}

[Trait("Category", "Unit")]
public class ExecutionModeGovernanceTypesTests
{
    [Theory]
    [InlineData(ExecutionMode.Automatic)]
    [InlineData(ExecutionMode.RequiresApproval)]
    [InlineData(ExecutionMode.ToolDelegation)]
    [InlineData(ExecutionMode.HumanDelegation)]
    public void AllValues_AreDefined(ExecutionMode mode)
    {
        Enum.IsDefined(mode).Should().BeTrue();
    }

    [Fact]
    public void HasFourValues()
    {
        Enum.GetValues<ExecutionMode>().Should().HaveCount(4);
    }
}
