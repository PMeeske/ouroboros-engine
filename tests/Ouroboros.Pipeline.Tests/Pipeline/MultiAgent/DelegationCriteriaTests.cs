namespace Ouroboros.Tests.Pipeline.MultiAgent;

using Ouroboros.Pipeline.MultiAgent;
using Ouroboros.Pipeline.Planning;

[Trait("Category", "Unit")]
public class DelegationCriteriaTests
{
    [Fact]
    public void FromGoal_SetsDefaults()
    {
        var goal = Goal.Atomic("test goal");
        var criteria = DelegationCriteria.FromGoal(goal);

        criteria.Goal.Should().Be(goal);
        criteria.RequiredCapabilities.Should().BeEmpty();
        criteria.MinProficiency.Should().Be(0.0);
        criteria.PreferAvailable.Should().BeTrue();
        criteria.PreferredRole.Should().BeNull();
    }

    [Fact]
    public void FromGoal_ThrowsOnNull()
    {
        var act = () => DelegationCriteria.FromGoal(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WithMinProficiency_UpdatesValue()
    {
        var goal = Goal.Atomic("goal");
        var criteria = DelegationCriteria.FromGoal(goal).WithMinProficiency(0.8);

        criteria.MinProficiency.Should().Be(0.8);
    }

    [Fact]
    public void WithMinProficiency_ThrowsOnInvalidRange()
    {
        var criteria = DelegationCriteria.FromGoal(Goal.Atomic("goal"));

        var act = () => criteria.WithMinProficiency(-0.1);
        act.Should().Throw<ArgumentOutOfRangeException>();

        var act2 = () => criteria.WithMinProficiency(1.1);
        act2.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void WithPreferredRole_SetsRole()
    {
        var criteria = DelegationCriteria.FromGoal(Goal.Atomic("goal"))
            .WithPreferredRole(AgentRole.Planner);

        criteria.PreferredRole.Should().Be(AgentRole.Planner);
    }

    [Fact]
    public void RequireCapability_AddsCapability()
    {
        var criteria = DelegationCriteria.FromGoal(Goal.Atomic("goal"))
            .RequireCapability("coding");

        criteria.RequiredCapabilities.Should().Contain("coding");
    }

    [Fact]
    public void WithAvailabilityPreference_UpdatesFlag()
    {
        var criteria = DelegationCriteria.FromGoal(Goal.Atomic("goal"))
            .WithAvailabilityPreference(false);

        criteria.PreferAvailable.Should().BeFalse();
    }
}
