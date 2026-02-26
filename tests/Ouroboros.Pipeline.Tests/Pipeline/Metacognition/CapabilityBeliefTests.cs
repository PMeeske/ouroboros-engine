namespace Ouroboros.Tests.Pipeline.Metacognition;

using Ouroboros.Pipeline.Metacognition;

[Trait("Category", "Unit")]
public class CapabilityBeliefTests
{
    [Fact]
    public void Uninformative_HasMaxUncertainty()
    {
        var belief = CapabilityBelief.Uninformative("coding");

        belief.CapabilityName.Should().Be("coding");
        belief.Proficiency.Should().Be(0.5);
        belief.Uncertainty.Should().Be(1.0);
        belief.ValidationCount.Should().Be(0);
    }

    [Fact]
    public void Create_ClampsProficiencyAndUncertainty()
    {
        var belief = CapabilityBelief.Create("test", 1.5, -0.5);

        belief.Proficiency.Should().Be(1.0);
        belief.Uncertainty.Should().Be(0.0);
        belief.ValidationCount.Should().Be(1);
    }

    [Fact]
    public void WithBayesianUpdate_UpdatesBeliefBasedOnEvidence()
    {
        var belief = CapabilityBelief.Create("coding", 0.5, 0.5);
        var updated = belief.WithBayesianUpdate(1.0, 5);

        updated.Proficiency.Should().BeGreaterThan(0.5);
        updated.ValidationCount.Should().Be(6);
    }

    [Fact]
    public void GetCredibleInterval_ReturnsValidBounds()
    {
        var belief = CapabilityBelief.Create("coding", 0.7, 0.3);
        var (lower, expected, upper) = belief.GetCredibleInterval(0.95);

        lower.Should().BeLessThanOrEqualTo(expected);
        expected.Should().BeLessThanOrEqualTo(upper);
        lower.Should().BeGreaterThanOrEqualTo(0.0);
        upper.Should().BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public void Validate_ReturnsSuccess_ForValidBelief()
    {
        var belief = CapabilityBelief.Create("coding", 0.7, 0.3);
        belief.Validate().IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Validate_ReturnsFailure_ForEmptyName()
    {
        var belief = CapabilityBelief.Create("", 0.7, 0.3)
            with { CapabilityName = "" };
        belief.Validate().IsSuccess.Should().BeFalse();
    }
}
