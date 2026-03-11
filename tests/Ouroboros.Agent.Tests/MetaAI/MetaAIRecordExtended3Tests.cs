using FluentAssertions;
using Ouroboros.Agent.MetaAI;
using Xunit;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class ApprovalRequestTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var now = DateTime.UtcNow;
        var parameters = new Dictionary<string, object> { ["target"] = (object)"prod" };
        var request = new ApprovalRequest("req-1", "deploy", parameters, "Stable build", now);

        request.RequestId.Should().Be("req-1");
        request.Action.Should().Be("deploy");
        request.Parameters.Should().ContainKey("target");
        request.Rationale.Should().Be("Stable build");
        request.RequestedAt.Should().Be(now);
    }
}

[Trait("Category", "Unit")]
public class ApprovalResponseTests
{
    [Fact]
    public void Create_Approved_ShouldSetProperties()
    {
        var now = DateTime.UtcNow;
        var response = new ApprovalResponse("req-1", true, null, null, now);

        response.RequestId.Should().Be("req-1");
        response.Approved.Should().BeTrue();
        response.Reason.Should().BeNull();
        response.Modifications.Should().BeNull();
        response.RespondedAt.Should().Be(now);
    }

    [Fact]
    public void Create_Denied_WithReason_ShouldSetProperties()
    {
        var now = DateTime.UtcNow;
        var mods = new Dictionary<string, object> { ["env"] = (object)"staging" };
        var response = new ApprovalResponse("req-2", false, "Not ready", mods, now);

        response.Approved.Should().BeFalse();
        response.Reason.Should().Be("Not ready");
        response.Modifications.Should().ContainKey("env");
    }
}

[Trait("Category", "Unit")]
public class VariantMetricsTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var metrics = new VariantMetrics(0.95, 150.0, 300.0, 500.0, 0.87, 1000, 950);

        metrics.SuccessRate.Should().Be(0.95);
        metrics.AverageLatencyMs.Should().Be(150.0);
        metrics.P95LatencyMs.Should().Be(300.0);
        metrics.P99LatencyMs.Should().Be(500.0);
        metrics.AverageConfidence.Should().Be(0.87);
        metrics.TotalPrompts.Should().Be(1000);
        metrics.SuccessfulPrompts.Should().Be(950);
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        var a = new VariantMetrics(0.5, 100, 200, 300, 0.8, 50, 25);
        var b = new VariantMetrics(0.5, 100, 200, 300, 0.8, 50, 25);

        a.Should().Be(b);
    }
}

[Trait("Category", "Unit")]
public class OuroborosExperienceTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var insights = new List<string> { "insight1", "insight2" };
        var experience = new OuroborosExperience(id, "Solve problem X", true, 0.92, insights, now, TimeSpan.FromMinutes(5));

        experience.Id.Should().Be(id);
        experience.Goal.Should().Be("Solve problem X");
        experience.Success.Should().BeTrue();
        experience.QualityScore.Should().Be(0.92);
        experience.Insights.Should().HaveCount(2);
        experience.Timestamp.Should().Be(now);
        experience.Duration.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void Create_WithDefaultDuration_ShouldBeZero()
    {
        var experience = new OuroborosExperience(Guid.NewGuid(), "g", false, 0.0, Array.Empty<string>(), DateTime.UtcNow);

        experience.Duration.Should().Be(TimeSpan.Zero);
    }
}

[Trait("Category", "Unit")]
public class EvaluationMetricsTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var customMetrics = new Dictionary<string, double> { ["recall"] = 0.9 };
        var metrics = new EvaluationMetrics("test-1", true, 0.85, TimeSpan.FromSeconds(2), 5, 0.9, customMetrics);

        metrics.TestCase.Should().Be("test-1");
        metrics.Success.Should().BeTrue();
        metrics.QualityScore.Should().Be(0.85);
        metrics.ExecutionTime.Should().Be(TimeSpan.FromSeconds(2));
        metrics.PlanSteps.Should().Be(5);
        metrics.ConfidenceScore.Should().Be(0.9);
        metrics.CustomMetrics.Should().ContainKey("recall");
    }
}

[Trait("Category", "Unit")]
public class EvaluationResultsTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var testResults = new List<EvaluationMetrics>();
        var aggregated = new Dictionary<string, double> { ["accuracy"] = 0.95 };
        var results = new EvaluationResults(100, 95, 5, 0.9, 0.85, TimeSpan.FromSeconds(1), testResults, aggregated);

        results.TotalTests.Should().Be(100);
        results.SuccessfulTests.Should().Be(95);
        results.FailedTests.Should().Be(5);
        results.AverageQualityScore.Should().Be(0.9);
        results.AverageConfidence.Should().Be(0.85);
        results.AverageExecutionTime.Should().Be(TimeSpan.FromSeconds(1));
        results.TestResults.Should().BeEmpty();
        results.AggregatedMetrics.Should().ContainKey("accuracy");
    }
}

[Trait("Category", "Unit")]
public class TemporalPlanBasicTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var tasks = new List<ScheduledTask>
        {
            new("Build", DateTime.UtcNow, DateTime.UtcNow.AddHours(1), new List<string>())
        };
        var plan = new TemporalPlan("Deploy app", tasks, TimeSpan.FromHours(2));

        plan.Goal.Should().Be("Deploy app");
        plan.Tasks.Should().HaveCount(1);
        plan.TotalDuration.Should().Be(TimeSpan.FromHours(2));
    }
}

[Trait("Category", "Unit")]
public class HierarchicalPlanTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var now = DateTime.UtcNow;
        var topPlan = new Plan("main", new List<PlanStep>(), DateTime.UtcNow);
        var subPlans = new Dictionary<string, Plan> { ["sub1"] = topPlan };
        var plan = new HierarchicalPlan("Big goal", topPlan, subPlans, 3, now);

        plan.Goal.Should().Be("Big goal");
        plan.TopLevelPlan.Should().Be(topPlan);
        plan.SubPlans.Should().HaveCount(1);
        plan.MaxDepth.Should().Be(3);
        plan.CreatedAt.Should().Be(now);
    }
}
