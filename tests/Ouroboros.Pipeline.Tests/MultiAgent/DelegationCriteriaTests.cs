using FluentAssertions;
using Ouroboros.Pipeline.MultiAgent;
using Ouroboros.Pipeline.Planning;

namespace Ouroboros.Tests.MultiAgent;

[Trait("Category", "Unit")]
public sealed class DelegationCriteriaTests
{
    private static Goal CreateTestGoal() => Goal.Atomic("Test goal");

    [Fact]
    public void FromGoal_WithValidGoal_ReturnsDefaultCriteria()
    {
        // Arrange
        var goal = CreateTestGoal();

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
    public void FromGoal_WithNullGoal_ThrowsArgumentNullException()
    {
        Action act = () => DelegationCriteria.FromGoal(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("goal");
    }

    [Fact]
    public void WithMinProficiency_SetsMinProficiency()
    {
        // Arrange
        var criteria = DelegationCriteria.FromGoal(CreateTestGoal());

        // Act
        var updated = criteria.WithMinProficiency(0.75);

        // Assert
        updated.MinProficiency.Should().Be(0.75);
        criteria.MinProficiency.Should().Be(0.0); // original unchanged
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void WithMinProficiency_WithInvalidValue_ThrowsArgumentOutOfRangeException(double value)
    {
        var criteria = DelegationCriteria.FromGoal(CreateTestGoal());
        Action act = () => criteria.WithMinProficiency(value);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("minProficiency");
    }

    [Fact]
    public void WithPreferredRole_SetsRole()
    {
        var criteria = DelegationCriteria.FromGoal(CreateTestGoal());
        var updated = criteria.WithPreferredRole(AgentRole.Coder);
        updated.PreferredRole.Should().Be(AgentRole.Coder);
    }

    [Fact]
    public void RequireCapability_AddsCapability()
    {
        var criteria = DelegationCriteria.FromGoal(CreateTestGoal());
        var updated = criteria.RequireCapability("coding").RequireCapability("testing");
        updated.RequiredCapabilities.Should().BeEquivalentTo(new[] { "coding", "testing" });
    }

    [Fact]
    public void RequireCapability_WithNullCapability_ThrowsArgumentNullException()
    {
        var criteria = DelegationCriteria.FromGoal(CreateTestGoal());
        Action act = () => criteria.RequireCapability(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("capability");
    }

    [Fact]
    public void WithAvailabilityPreference_SetsPreference()
    {
        var criteria = DelegationCriteria.FromGoal(CreateTestGoal());
        var updated = criteria.WithAvailabilityPreference(false);
        updated.PreferAvailable.Should().BeFalse();
    }
}
