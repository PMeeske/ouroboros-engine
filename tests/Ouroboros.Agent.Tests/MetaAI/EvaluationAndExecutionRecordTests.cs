using FluentAssertions;
using Ouroboros.Agent.MetaAI;
using Xunit;
using PlanStep = Ouroboros.Agent.PlanStep;
using Plan = Ouroboros.Agent.MetaAI.Plan;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class EvaluationMetricsTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var customMetrics = new Dictionary<string, double>
        {
            ["accuracy"] = 0.95,
            ["f1"] = 0.88
        };
        var duration = TimeSpan.FromSeconds(2.5);

        var metrics = new EvaluationMetrics(
            "test-case-1", true, 0.92, duration, 5, 0.88, customMetrics);

        metrics.TestCase.Should().Be("test-case-1");
        metrics.Success.Should().BeTrue();
        metrics.QualityScore.Should().Be(0.92);
        metrics.ExecutionTime.Should().Be(duration);
        metrics.PlanSteps.Should().Be(5);
        metrics.ConfidenceScore.Should().Be(0.88);
        metrics.CustomMetrics.Should().HaveCount(2);
        metrics.CustomMetrics["accuracy"].Should().Be(0.95);
    }

    [Fact]
    public void Create_WithEmptyCustomMetrics_ShouldWork()
    {
        var metrics = new EvaluationMetrics(
            "test", false, 0.3, TimeSpan.Zero, 1, 0.2, new Dictionary<string, double>());

        metrics.Success.Should().BeFalse();
        metrics.CustomMetrics.Should().BeEmpty();
    }
}

[Trait("Category", "Unit")]
public class EvaluationResultsTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var testResults = new List<EvaluationMetrics>
        {
            new("tc-1", true, 0.9, TimeSpan.FromSeconds(1), 3, 0.85, new Dictionary<string, double>()),
            new("tc-2", false, 0.4, TimeSpan.FromSeconds(2), 5, 0.5, new Dictionary<string, double>())
        };
        var aggregated = new Dictionary<string, double> { ["mean_quality"] = 0.65 };
        var avgTime = TimeSpan.FromSeconds(1.5);

        var results = new EvaluationResults(
            2, 1, 1, 0.65, 0.675, avgTime, testResults, aggregated);

        results.TotalTests.Should().Be(2);
        results.SuccessfulTests.Should().Be(1);
        results.FailedTests.Should().Be(1);
        results.AverageQualityScore.Should().Be(0.65);
        results.AverageConfidence.Should().Be(0.675);
        results.AverageExecutionTime.Should().Be(avgTime);
        results.TestResults.Should().HaveCount(2);
        results.AggregatedMetrics.Should().ContainKey("mean_quality");
    }
}

[Trait("Category", "Unit")]
public class ExecutedStepTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var duration = TimeSpan.FromMilliseconds(150);
        var outputs = new Dictionary<string, object>
        {
            ["result"] = "success",
            ["count"] = 42
        };

        var step = new ExecutedStep("ParseInput", true, duration, outputs);

        step.StepName.Should().Be("ParseInput");
        step.Success.Should().BeTrue();
        step.Duration.Should().Be(duration);
        step.Outputs.Should().HaveCount(2);
        step.Outputs["result"].Should().Be("success");
    }

    [Fact]
    public void Create_Failed_ShouldWork()
    {
        var step = new ExecutedStep("Deploy", false, TimeSpan.FromSeconds(30), new Dictionary<string, object>());

        step.Success.Should().BeFalse();
        step.Outputs.Should().BeEmpty();
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        var outputs = new Dictionary<string, object>();
        var d = TimeSpan.FromSeconds(1);
        var a = new ExecutedStep("s", true, d, outputs);
        var b = new ExecutedStep("s", true, d, outputs);
        a.Should().Be(b);
    }
}

[Trait("Category", "Unit")]
public class PlanExecutionContextTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var steps = new List<PlanStep>
        {
            new("step1", new Dictionary<string, object>(), "out1", 0.9),
            new("step2", new Dictionary<string, object>(), "out2", 0.8)
        };
        var plan = new Plan(
            "test goal",
            steps,
            new Dictionary<string, double>(),
            DateTime.UtcNow);
        var completedSteps = new List<StepResult>
        {
            new(steps[0], true, "done", null, TimeSpan.FromSeconds(1), new Dictionary<string, object>())
        };
        var metadata = new Dictionary<string, object> { ["env"] = "test" };

        var context = new PlanExecutionContext(plan, completedSteps, steps[1], 1, metadata);

        context.OriginalPlan.Should().Be(plan);
        context.CompletedSteps.Should().HaveCount(1);
        context.CurrentStep.Action.Should().Be("step2");
        context.CurrentStepIndex.Should().Be(1);
        context.Metadata.Should().ContainKey("env");
    }
}

[Trait("Category", "Unit")]
public class ExecutionTraceTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var executedSteps = new List<ExecutedStep>
        {
            new("step1", true, TimeSpan.FromMilliseconds(100), new Dictionary<string, object>()),
            new("step2", false, TimeSpan.FromMilliseconds(200), new Dictionary<string, object>())
        };

        var trace = new ExecutionTrace(executedSteps, 1, "NullReferenceException");

        trace.Steps.Should().HaveCount(2);
        trace.FailedAtIndex.Should().Be(1);
        trace.FailureReason.Should().Be("NullReferenceException");
    }

    [Fact]
    public void Create_NoFailure_ShouldWork()
    {
        var steps = new List<ExecutedStep>
        {
            new("step1", true, TimeSpan.FromMilliseconds(50), new Dictionary<string, object>())
        };

        var trace = new ExecutionTrace(steps, -1, string.Empty);

        trace.FailedAtIndex.Should().Be(-1);
        trace.FailureReason.Should().BeEmpty();
    }
}

[Trait("Category", "Unit")]
public class ExperimentResultTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var started = DateTime.UtcNow;
        var completed = started.AddMinutes(5);
        var variantResults = new List<VariantResult>
        {
            new("variant-A", new List<PromptResult>(), new VariantMetrics(0.9, 100, 200, 300, 0.85, 10, 9)),
            new("variant-B", new List<PromptResult>(), new VariantMetrics(0.8, 120, 250, 350, 0.75, 10, 8))
        };
        var analysis = new StatisticalAnalysis(0.15, true, "Variant A is significantly better");

        var result = new ExperimentResult(
            "exp-1", started, completed, variantResults, analysis, "variant-A", ExperimentStatus.Completed);

        result.ExperimentId.Should().Be("exp-1");
        result.StartedAt.Should().Be(started);
        result.CompletedAt.Should().Be(completed);
        result.VariantResults.Should().HaveCount(2);
        result.Analysis.Should().NotBeNull();
        result.Winner.Should().Be("variant-A");
        result.Status.Should().Be(ExperimentStatus.Completed);
    }

    [Fact]
    public void Duration_ShouldCalculateCorrectly()
    {
        var started = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var completed = new DateTime(2024, 1, 1, 10, 5, 0, DateTimeKind.Utc);

        var result = new ExperimentResult(
            "exp-1", started, completed, new List<VariantResult>(), null, null, ExperimentStatus.Completed);

        result.Duration.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void IsCompleted_WhenCompleted_ShouldBeTrue()
    {
        var now = DateTime.UtcNow;
        var result = new ExperimentResult(
            "exp-1", now, now, new List<VariantResult>(), null, null, ExperimentStatus.Completed);

        result.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public void IsCompleted_WhenRunning_ShouldBeFalse()
    {
        var now = DateTime.UtcNow;
        var result = new ExperimentResult(
            "exp-1", now, now, new List<VariantResult>(), null, null, ExperimentStatus.Running);

        result.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public void IsCompleted_WhenFailed_ShouldBeFalse()
    {
        var now = DateTime.UtcNow;
        var result = new ExperimentResult(
            "exp-1", now, now, new List<VariantResult>(), null, null, ExperimentStatus.Failed);

        result.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public void IsCompleted_WhenCancelled_ShouldBeFalse()
    {
        var now = DateTime.UtcNow;
        var result = new ExperimentResult(
            "exp-1", now, now, new List<VariantResult>(), null, null, ExperimentStatus.Cancelled);

        result.IsCompleted.Should().BeFalse();
    }
}

[Trait("Category", "Unit")]
public class StatisticalAnalysisTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var analysis = new StatisticalAnalysis(0.25, true, "Large effect size detected");

        analysis.EffectSize.Should().Be(0.25);
        analysis.IsSignificant.Should().BeTrue();
        analysis.Interpretation.Should().Be("Large effect size detected");
    }

    [Fact]
    public void Create_NotSignificant_ShouldWork()
    {
        var analysis = new StatisticalAnalysis(0.01, false, "No significant difference");

        analysis.IsSignificant.Should().BeFalse();
        analysis.EffectSize.Should().Be(0.01);
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        var a = new StatisticalAnalysis(0.1, true, "sig");
        var b = new StatisticalAnalysis(0.1, true, "sig");
        a.Should().Be(b);
    }
}

[Trait("Category", "Unit")]
public class EpicTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var now = DateTime.UtcNow;
        var subIssues = new List<int> { 10, 11, 12 };

        var epic = new Epic(1, "User Authentication", "Implement auth system", subIssues, now);

        epic.EpicNumber.Should().Be(1);
        epic.Title.Should().Be("User Authentication");
        epic.Description.Should().Be("Implement auth system");
        epic.SubIssueNumbers.Should().HaveCount(3);
        epic.SubIssueNumbers.Should().Contain(10);
        epic.CreatedAt.Should().Be(now);
    }

    [Fact]
    public void Create_WithEmptySubIssues_ShouldWork()
    {
        var epic = new Epic(2, "Empty Epic", "desc", new List<int>(), DateTime.UtcNow);
        epic.SubIssueNumbers.Should().BeEmpty();
    }
}
