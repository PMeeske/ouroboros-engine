namespace Ouroboros.Tests.Pipeline.MultiAgent;

using Ouroboros.Pipeline.MultiAgent;
using Ouroboros.Pipeline.Planning;

[Trait("Category", "Unit")]
public class DelegationCriteriaTests
{
    [Fact]
    public void FromGoal_SetsDefaults()
    {
        // Arrange
        var goal = Goal.Atomic("test goal");

        // Act
        var criteria = DelegationCriteria.FromGoal(goal);

        // Assert
        criteria.Goal.Should().Be(goal);
        criteria.RequiredCapabilities.Should().BeEmpty();
        criteria.MinProficiency.Should().Be(0.0);
        criteria.PreferAvailable.Should().BeTrue();
        criteria.PreferredRole.Should().BeNull();
    }

    [Fact]
    public void FromGoal_ThrowsOnNull()
    {
        // Act
        var act = () => DelegationCriteria.FromGoal(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WithMinProficiency_UpdatesValue()
    {
        // Arrange
        var criteria = DelegationCriteria.FromGoal(Goal.Atomic("goal"));

        // Act
        var updated = criteria.WithMinProficiency(0.8);

        // Assert
        updated.MinProficiency.Should().Be(0.8);
    }

    [Fact]
    public void WithMinProficiency_ThrowsOnNegativeValue()
    {
        // Arrange
        var criteria = DelegationCriteria.FromGoal(Goal.Atomic("goal"));

        // Act
        var act = () => criteria.WithMinProficiency(-0.1);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void WithMinProficiency_ThrowsOnValueAboveOne()
    {
        // Arrange
        var criteria = DelegationCriteria.FromGoal(Goal.Atomic("goal"));

        // Act
        var act = () => criteria.WithMinProficiency(1.1);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void WithPreferredRole_SetsRole()
    {
        // Arrange
        var criteria = DelegationCriteria.FromGoal(Goal.Atomic("goal"));

        // Act
        var updated = criteria.WithPreferredRole(AgentRole.Planner);

        // Assert
        updated.PreferredRole.Should().Be(AgentRole.Planner);
    }

    [Fact]
    public void RequireCapability_AddsCapability()
    {
        // Arrange
        var criteria = DelegationCriteria.FromGoal(Goal.Atomic("goal"));

        // Act
        var updated = criteria.RequireCapability("coding");

        // Assert
        updated.RequiredCapabilities.Should().Contain("coding");
    }

    [Fact]
    public void RequireCapability_ThrowsOnNull()
    {
        // Arrange
        var criteria = DelegationCriteria.FromGoal(Goal.Atomic("goal"));

        // Act
        var act = () => criteria.RequireCapability(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WithAvailabilityPreference_UpdatesFlag()
    {
        // Arrange
        var criteria = DelegationCriteria.FromGoal(Goal.Atomic("goal"));

        // Act
        var updated = criteria.WithAvailabilityPreference(false);

        // Assert
        updated.PreferAvailable.Should().BeFalse();
    }
}
