using FluentAssertions;
using Ouroboros.Pipeline.Planning;
using Ouroboros.Pipeline.Verification;

namespace Ouroboros.Tests.Planning;

[Trait("Category", "Unit")]
public sealed class PlanCandidateTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var plan = new Plan("Test plan");
        double score = 42.5;
        string explanation = "Good plan";

        // Act
        var candidate = new PlanCandidate(plan, score, explanation);

        // Assert
        candidate.Plan.Should().Be(plan);
        candidate.Score.Should().Be(42.5);
        candidate.Explanation.Should().Be("Good plan");
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        // Arrange
        var plan = new Plan("Test plan");
        var candidate1 = new PlanCandidate(plan, 10.0, "Reason");
        var candidate2 = new PlanCandidate(plan, 10.0, "Reason");

        // Assert
        candidate1.Should().Be(candidate2);
    }

    [Fact]
    public void RecordEquality_DifferentScores_AreNotEqual()
    {
        // Arrange
        var plan = new Plan("Test plan");
        var candidate1 = new PlanCandidate(plan, 10.0, "Reason");
        var candidate2 = new PlanCandidate(plan, 20.0, "Reason");

        // Assert
        candidate1.Should().NotBe(candidate2);
    }

    [Fact]
    public void WithExpression_CanCreateModifiedCopy()
    {
        // Arrange
        var plan = new Plan("Test plan");
        var original = new PlanCandidate(plan, 10.0, "Original");

        // Act
        var modified = original with { Score = 99.0 };

        // Assert
        modified.Score.Should().Be(99.0);
        modified.Explanation.Should().Be("Original");
        modified.Plan.Should().Be(plan);
    }
}
