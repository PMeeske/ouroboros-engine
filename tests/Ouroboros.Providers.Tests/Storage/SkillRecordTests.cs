using PlanStep = Ouroboros.Agent.MetaAI.PlanStep;
using Skill = Ouroboros.Agent.MetaAI.Skill;

namespace Ouroboros.Tests.Database.Storage;

/// <summary>
/// Unit tests for Skill record.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "Database")]
public sealed class SkillRecordTests
{
    [Fact]
    public void Skill_WithValidData_SetsAllProperties()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var prerequisites = new List<string> { "prerequisite1", "prerequisite2" };
        var steps = new List<PlanStep>
        {
            new PlanStep("action1", new Dictionary<string, object>(), "outcome1", 0.9),
            new PlanStep("action2", new Dictionary<string, object>(), "outcome2", 0.85)
        };

        // Act
        var skill = new Skill(
            Name: "test_skill",
            Description: "A test skill for unit testing",
            Prerequisites: prerequisites,
            Steps: steps,
            SuccessRate: 0.95,
            UsageCount: 50,
            CreatedAt: now,
            LastUsed: now);

        // Assert
        skill.Name.Should().Be("test_skill");
        skill.Description.Should().Be("A test skill for unit testing");
        skill.Prerequisites.Should().HaveCount(2);
        skill.Steps.Should().HaveCount(2);
        skill.SuccessRate.Should().Be(0.95);
        skill.UsageCount.Should().Be(50);
        skill.CreatedAt.Should().Be(now);
        skill.LastUsed.Should().Be(now);
    }

    [Fact]
    public void Skill_WithEmptyPrerequisites_Accepted()
    {
        // Act
        var skill = new Skill(
            "empty_prereq_skill",
            "Skill with no prerequisites",
            new List<string>(),
            new List<PlanStep>(),
            1.0,
            0,
            DateTime.UtcNow,
            DateTime.UtcNow);

        // Assert
        skill.Prerequisites.Should().BeEmpty();
        skill.Steps.Should().BeEmpty();
    }

    [Fact]
    public void Skill_Equality_WorksCorrectly()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var sharedPrereqs = new List<string>();
        var sharedSteps = new List<PlanStep>();
        var skill1 = new Skill("skill", "desc", sharedPrereqs, sharedSteps, 0.5, 10, now, now);
        var skill2 = new Skill("skill", "desc", sharedPrereqs, sharedSteps, 0.5, 10, now, now);

        // Assert - Records compare by value, but Lists compare by reference
        // Using same List instances ensures equality
        skill1.Should().Be(skill2);
    }

    [Fact]
    public void Skill_WithExpression_UpdatesUsageMetrics()
    {
        // Arrange
        var original = new Skill(
            "updatable_skill",
            "A skill that will be updated",
            new List<string>(),
            new List<PlanStep>(),
            0.8,
            10,
            DateTime.UtcNow,
            DateTime.UtcNow.AddHours(-1));

        // Act
        var updated = original with
        {
            UsageCount = original.UsageCount + 1,
            SuccessRate = 0.85,
            LastUsed = DateTime.UtcNow
        };

        // Assert
        original.UsageCount.Should().Be(10);
        updated.UsageCount.Should().Be(11);
        updated.SuccessRate.Should().Be(0.85);
        updated.LastUsed.Should().BeAfter(original.LastUsed);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Skill_WithDifferentSuccessRates_Accepted(double successRate)
    {
        // Act
        var skill = new Skill(
            "rated_skill",
            "Skill with specific success rate",
            new List<string>(),
            new List<PlanStep>(),
            successRate,
            0,
            DateTime.UtcNow,
            DateTime.UtcNow);

        // Assert
        skill.SuccessRate.Should().Be(successRate);
    }
}