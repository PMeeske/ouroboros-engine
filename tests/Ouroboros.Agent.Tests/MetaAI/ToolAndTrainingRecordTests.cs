using System.Collections.Immutable;
using FluentAssertions;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Pipeline.Learning;
using Xunit;
using PlanStep = Ouroboros.Agent.PlanStep;
using Plan = Ouroboros.Agent.MetaAI.Plan;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class ToolRecommendationTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var rec = new ToolRecommendation("web_search", "Search the web", 0.85, ToolCategory.Web);

        rec.ToolName.Should().Be("web_search");
        rec.Description.Should().Be("Search the web");
        rec.RelevanceScore.Should().Be(0.85);
        rec.Category.Should().Be(ToolCategory.Web);
    }

    [Fact]
    public void IsHighlyRecommended_WhenScoreAbove07_ShouldBeTrue()
    {
        var rec = new ToolRecommendation("tool", "desc", 0.8, ToolCategory.General);
        rec.IsHighlyRecommended.Should().BeTrue();
    }

    [Fact]
    public void IsHighlyRecommended_WhenScoreExactly07_ShouldBeFalse()
    {
        var rec = new ToolRecommendation("tool", "desc", 0.7, ToolCategory.General);
        rec.IsHighlyRecommended.Should().BeFalse();
    }

    [Fact]
    public void IsHighlyRecommended_WhenScoreBelow07_ShouldBeFalse()
    {
        var rec = new ToolRecommendation("tool", "desc", 0.5, ToolCategory.General);
        rec.IsHighlyRecommended.Should().BeFalse();
    }

    [Fact]
    public void IsRecommended_WhenScoreAbove04_ShouldBeTrue()
    {
        var rec = new ToolRecommendation("tool", "desc", 0.5, ToolCategory.General);
        rec.IsRecommended.Should().BeTrue();
    }

    [Fact]
    public void IsRecommended_WhenScoreExactly04_ShouldBeFalse()
    {
        var rec = new ToolRecommendation("tool", "desc", 0.4, ToolCategory.General);
        rec.IsRecommended.Should().BeFalse();
    }

    [Fact]
    public void IsRecommended_WhenScoreBelow04_ShouldBeFalse()
    {
        var rec = new ToolRecommendation("tool", "desc", 0.2, ToolCategory.General);
        rec.IsRecommended.Should().BeFalse();
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        var a = new ToolRecommendation("t", "d", 0.5, ToolCategory.Code);
        var b = new ToolRecommendation("t", "d", 0.5, ToolCategory.Code);
        a.Should().Be(b);
    }
}

[Trait("Category", "Unit")]
public class ToolSelectionTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var selection = new ToolSelection("web_search", "{\"query\": \"test\"}");

        selection.ToolName.Should().Be("web_search");
        selection.ArgumentsJson.Should().Be("{\"query\": \"test\"}");
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        var a = new ToolSelection("tool", "{}");
        var b = new ToolSelection("tool", "{}");
        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentArgs_ShouldNotBeEqual()
    {
        var a = new ToolSelection("tool", "{\"a\":1}");
        var b = new ToolSelection("tool", "{\"b\":2}");
        a.Should().NotBe(b);
    }
}

[Trait("Category", "Unit")]
public class ToolSelectionContextTests
{
    [Fact]
    public void Create_DefaultValues_ShouldHaveNullsAndFalses()
    {
        var ctx = new ToolSelectionContext();

        ctx.MaxTools.Should().BeNull();
        ctx.RequiredCategories.Should().BeNull();
        ctx.ExcludedCategories.Should().BeNull();
        ctx.RequiredToolNames.Should().BeNull();
        ctx.PreferFastTools.Should().BeFalse();
        ctx.PreferReliableTools.Should().BeFalse();
    }

    [Fact]
    public void Create_WithInitProperties_ShouldSetThem()
    {
        var ctx = new ToolSelectionContext
        {
            MaxTools = 3,
            RequiredCategories = new List<ToolCategory> { ToolCategory.Code, ToolCategory.Analysis },
            ExcludedCategories = new List<ToolCategory> { ToolCategory.Creative },
            RequiredToolNames = new List<string> { "compiler" },
            PreferFastTools = true,
            PreferReliableTools = true
        };

        ctx.MaxTools.Should().Be(3);
        ctx.RequiredCategories.Should().HaveCount(2);
        ctx.ExcludedCategories.Should().HaveCount(1);
        ctx.RequiredToolNames.Should().Contain("compiler");
        ctx.PreferFastTools.Should().BeTrue();
        ctx.PreferReliableTools.Should().BeTrue();
    }
}

[Trait("Category", "Unit")]
public class TrainingBatchTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var now = DateTime.UtcNow;
        var experiences = new List<Experience>
        {
            new(Guid.NewGuid(), "s1", "a1", 0.5, "s2", now, ImmutableDictionary<string, object>.Empty, 1.0)
        };
        var metrics = new Dictionary<string, double> { ["loss"] = 0.05 };

        var batch = new TrainingBatch(experiences, metrics, now);

        batch.Experiences.Should().HaveCount(1);
        batch.Metrics.Should().ContainKey("loss");
        batch.Metrics["loss"].Should().Be(0.05);
        batch.CreatedAt.Should().Be(now);
    }

    [Fact]
    public void Create_WithEmptyCollections_ShouldWork()
    {
        var batch = new TrainingBatch(
            new List<Experience>(), new Dictionary<string, double>(), DateTime.UtcNow);

        batch.Experiences.Should().BeEmpty();
        batch.Metrics.Should().BeEmpty();
    }
}

[Trait("Category", "Unit")]
public class TrainingResultTests
{
    [Fact]
    public void Create_Success_ShouldSetAllProperties()
    {
        var improvedMetrics = new Dictionary<string, double>
        {
            ["accuracy"] = 0.95,
            ["loss"] = 0.02
        };
        var patterns = new List<string> { "retry-on-timeout", "cache-similar" };

        var result = new TrainingResult(100, improvedMetrics, patterns, true);

        result.ExperiencesProcessed.Should().Be(100);
        result.ImprovedMetrics.Should().HaveCount(2);
        result.ImprovedMetrics["accuracy"].Should().Be(0.95);
        result.LearnedPatterns.Should().HaveCount(2);
        result.LearnedPatterns.Should().Contain("retry-on-timeout");
        result.Success.Should().BeTrue();
    }

    [Fact]
    public void Create_Failure_ShouldWork()
    {
        var result = new TrainingResult(
            0, new Dictionary<string, double>(), new List<string>(), false);

        result.ExperiencesProcessed.Should().Be(0);
        result.Success.Should().BeFalse();
    }

    [Fact]
    public void Equality_SameReferences_ShouldBeEqual()
    {
        var metrics = new Dictionary<string, double>();
        var patterns = new List<string>();
        var a = new TrainingResult(10, metrics, patterns, true);
        var b = new TrainingResult(10, metrics, patterns, true);
        a.Should().Be(b);
    }
}

[Trait("Category", "Unit")]
public class VariantMetricsTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var metrics = new VariantMetrics(0.9, 120.5, 250.0, 400.0, 0.85, 100, 90);

        metrics.SuccessRate.Should().Be(0.9);
        metrics.AverageLatencyMs.Should().Be(120.5);
        metrics.P95LatencyMs.Should().Be(250.0);
        metrics.P99LatencyMs.Should().Be(400.0);
        metrics.AverageConfidence.Should().Be(0.85);
        metrics.TotalPrompts.Should().Be(100);
        metrics.SuccessfulPrompts.Should().Be(90);
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        var a = new VariantMetrics(0.9, 100, 200, 300, 0.8, 50, 45);
        var b = new VariantMetrics(0.9, 100, 200, 300, 0.8, 50, 45);
        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentValues_ShouldNotBeEqual()
    {
        var a = new VariantMetrics(0.9, 100, 200, 300, 0.8, 50, 45);
        var b = new VariantMetrics(0.8, 100, 200, 300, 0.8, 50, 40);
        a.Should().NotBe(b);
    }
}

[Trait("Category", "Unit")]
public class VariantResultTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var promptResults = new List<PromptResult>
        {
            new("prompt1", true, 100, 0.9, "gpt-4", null),
            new("prompt2", false, 5000, 0.1, null, "Timeout")
        };
        var metrics = new VariantMetrics(0.5, 2550, 5000, 5000, 0.5, 2, 1);

        var result = new VariantResult("variant-A", promptResults, metrics);

        result.VariantId.Should().Be("variant-A");
        result.PromptResults.Should().HaveCount(2);
        result.Metrics.SuccessRate.Should().Be(0.5);
    }

    [Fact]
    public void Create_WithEmptyPromptResults_ShouldWork()
    {
        var metrics = new VariantMetrics(0, 0, 0, 0, 0, 0, 0);
        var result = new VariantResult("empty", new List<PromptResult>(), metrics);

        result.PromptResults.Should().BeEmpty();
    }
}

[Trait("Category", "Unit")]
public class TemporalConstraintTests
{
    [Fact]
    public void Create_WithDuration_ShouldSetAllProperties()
    {
        var duration = TimeSpan.FromMinutes(30);
        var constraint = new TemporalConstraint("TaskA", "TaskB", TemporalRelation.Before, duration);

        constraint.TaskA.Should().Be("TaskA");
        constraint.TaskB.Should().Be("TaskB");
        constraint.Relation.Should().Be(TemporalRelation.Before);
        constraint.Duration.Should().Be(duration);
    }

    [Fact]
    public void Create_WithoutDuration_ShouldDefaultToNull()
    {
        var constraint = new TemporalConstraint("A", "B", TemporalRelation.Simultaneous);

        constraint.Duration.Should().BeNull();
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        var a = new TemporalConstraint("A", "B", TemporalRelation.After);
        var b = new TemporalConstraint("A", "B", TemporalRelation.After);
        a.Should().Be(b);
    }
}

[Trait("Category", "Unit")]
public class TemporalPlanTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var now = DateTime.UtcNow;
        var tasks = new List<ScheduledTask>
        {
            new("Build", now, now.AddMinutes(10), new List<string>()),
            new("Test", now.AddMinutes(10), now.AddMinutes(20), new List<string> { "Build" })
        };
        var totalDuration = TimeSpan.FromMinutes(20);

        var plan = new TemporalPlan("Deploy application", tasks, totalDuration, now);

        plan.Goal.Should().Be("Deploy application");
        plan.Tasks.Should().HaveCount(2);
        plan.TotalDuration.Should().Be(totalDuration);
        plan.CreatedAt.Should().Be(now);
    }

    [Fact]
    public void Create_WithDefaultCreatedAt_ShouldUseDefault()
    {
        var plan = new TemporalPlan(
            "goal", new List<ScheduledTask>(), TimeSpan.FromMinutes(5));

        plan.CreatedAt.Should().Be(default(DateTime));
    }
}

[Trait("Category", "Unit")]
public class HierarchicalPlanTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var now = DateTime.UtcNow;
        var topLevel = new Plan(
            "top goal",
            new List<PlanStep> { new("step1", new Dictionary<string, object>(), "out", 0.9) },
            new Dictionary<string, double>(),
            now);
        var subPlans = new Dictionary<string, Plan>
        {
            ["sub1"] = new Plan("sub goal", new List<PlanStep>(), new Dictionary<string, double>(), now)
        };

        var hierarchical = new HierarchicalPlan("Master goal", topLevel, subPlans, 3, now);

        hierarchical.Goal.Should().Be("Master goal");
        hierarchical.TopLevelPlan.Should().Be(topLevel);
        hierarchical.SubPlans.Should().HaveCount(1);
        hierarchical.MaxDepth.Should().Be(3);
        hierarchical.CreatedAt.Should().Be(now);
    }
}
