using FluentAssertions;
using Ouroboros.Agent.MetaAI;
using Xunit;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class OuroborosResultTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var phases = new List<PhaseResult>
        {
            new(ImprovementPhase.Plan, true, "planned", null, TimeSpan.FromSeconds(1))
        };
        var metadata = new Dictionary<string, object> { ["model"] = (object)"gpt-4" };
        var result = new OuroborosResult("Solve X", true, "Solution", phases, 3, ImprovementPhase.Learn, "Good", TimeSpan.FromMinutes(5), metadata);

        result.Goal.Should().Be("Solve X");
        result.Success.Should().BeTrue();
        result.Output.Should().Be("Solution");
        result.PhaseResults.Should().HaveCount(1);
        result.CycleCount.Should().Be(3);
        result.CurrentPhase.Should().Be(ImprovementPhase.Learn);
        result.SelfReflection.Should().Be("Good");
        result.Duration.Should().Be(TimeSpan.FromMinutes(5));
        result.Metadata.Should().ContainKey("model");
    }
}

[Trait("Category", "Unit")]
public class OuroborosCapabilityTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var capability = new OuroborosCapability("coding", "Write code", 0.9);

        capability.Name.Should().Be("coding");
        capability.Description.Should().Be("Write code");
        capability.ConfidenceLevel.Should().Be(0.9);
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        var a = new OuroborosCapability("x", "y", 0.5);
        var b = new OuroborosCapability("x", "y", 0.5);
        a.Should().Be(b);
    }
}

[Trait("Category", "Unit")]
public class ToolRecommendationTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var rec = new ToolRecommendation("compiler", "Compiles code", 0.85, ToolCategory.Code);

        rec.ToolName.Should().Be("compiler");
        rec.Description.Should().Be("Compiles code");
        rec.RelevanceScore.Should().Be(0.85);
        rec.Category.Should().Be(ToolCategory.Code);
    }

    [Fact]
    public void IsHighlyRecommended_WhenAbove07_ShouldBeTrue()
    {
        var rec = new ToolRecommendation("t", "d", 0.8, ToolCategory.General);
        rec.IsHighlyRecommended.Should().BeTrue();
    }

    [Fact]
    public void IsHighlyRecommended_WhenBelow07_ShouldBeFalse()
    {
        var rec = new ToolRecommendation("t", "d", 0.5, ToolCategory.General);
        rec.IsHighlyRecommended.Should().BeFalse();
    }

    [Fact]
    public void IsRecommended_WhenAbove04_ShouldBeTrue()
    {
        var rec = new ToolRecommendation("t", "d", 0.5, ToolCategory.General);
        rec.IsRecommended.Should().BeTrue();
    }

    [Fact]
    public void IsRecommended_WhenBelow04_ShouldBeFalse()
    {
        var rec = new ToolRecommendation("t", "d", 0.3, ToolCategory.General);
        rec.IsRecommended.Should().BeFalse();
    }
}

[Trait("Category", "Unit")]
public class ToolSelectionAgentTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var selection = new ToolSelection("search", "{\"query\":\"test\"}");

        selection.ToolName.Should().Be("search");
        selection.ArgumentsJson.Should().Be("{\"query\":\"test\"}");
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        var a = new ToolSelection("t", "{}");
        var b = new ToolSelection("t", "{}");
        a.Should().Be(b);
    }
}

[Trait("Category", "Unit")]
public class StakeholderReviewConfigTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var config = new StakeholderReviewConfig();

        config.MinimumRequiredApprovals.Should().Be(2);
        config.RequireAllReviewersApprove.Should().BeTrue();
        config.AutoResolveNonBlockingComments.Should().BeFalse();
    }

    [Fact]
    public void Create_WithCustomValues_ShouldOverrideDefaults()
    {
        var config = new StakeholderReviewConfig(3, false, true, TimeSpan.FromHours(1), TimeSpan.FromSeconds(30));

        config.MinimumRequiredApprovals.Should().Be(3);
        config.RequireAllReviewersApprove.Should().BeFalse();
        config.AutoResolveNonBlockingComments.Should().BeTrue();
        config.ReviewTimeout.Should().Be(TimeSpan.FromHours(1));
        config.PollingInterval.Should().Be(TimeSpan.FromSeconds(30));
    }
}

[Trait("Category", "Unit")]
public class TaskAssignmentTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var now = DateTime.UtcNow;
        var step = new PlanStep("step-1", "Do something", "tool");
        var assignment = new TaskAssignment("task-1", "agent-1", step, now, TaskAssignmentStatus.Pending);

        assignment.TaskId.Should().Be("task-1");
        assignment.AgentId.Should().Be("agent-1");
        assignment.Step.Should().Be(step);
        assignment.AssignedAt.Should().Be(now);
        assignment.Status.Should().Be(TaskAssignmentStatus.Pending);
    }
}

[Trait("Category", "Unit")]
public class SkillRegistryStatsTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var stats = new SkillRegistryStats(25, 0.8, 500, "search", "analyze", "/data/skills", true);

        stats.TotalSkills.Should().Be(25);
        stats.AverageSuccessRate.Should().Be(0.8);
        stats.TotalExecutions.Should().Be(500);
        stats.MostUsedSkill.Should().Be("search");
        stats.MostSuccessfulSkill.Should().Be("analyze");
        stats.StoragePath.Should().Be("/data/skills");
        stats.IsPersisted.Should().BeTrue();
    }

    [Fact]
    public void Create_WithNullSkills_ShouldAllowNull()
    {
        var stats = new SkillRegistryStats(0, 0, 0, null, null, "path", false);

        stats.MostUsedSkill.Should().BeNull();
        stats.MostSuccessfulSkill.Should().BeNull();
    }
}

[Trait("Category", "Unit")]
public class SerializableSkillTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var preconditions = new List<string> { "pre1" };
        var effects = new List<string> { "eff1" };
        var tags = new List<string> { "tag1" };
        var skill = new SerializableSkill("s1", "search", "Search the web", "Knowledge", preconditions, effects, 0.9, 100, 500, tags);

        skill.Id.Should().Be("s1");
        skill.Name.Should().Be("search");
        skill.Description.Should().Be("Search the web");
        skill.Category.Should().Be("Knowledge");
        skill.Preconditions.Should().HaveCount(1);
        skill.Effects.Should().HaveCount(1);
        skill.SuccessRate.Should().Be(0.9);
        skill.UsageCount.Should().Be(100);
        skill.AverageExecutionTime.Should().Be(500);
        skill.Tags.Should().HaveCount(1);
    }
}

[Trait("Category", "Unit")]
public class SerializableSkillDataTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var data = new SerializableSkillData("s1", "name", "desc", "cat", new List<string>(), new List<string>(), 0.5, 10, 200, new List<string>());

        data.Id.Should().Be("s1");
        data.Name.Should().Be("name");
        data.SuccessRate.Should().Be(0.5);
        data.UsageCount.Should().Be(10);
    }
}
