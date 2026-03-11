using FluentAssertions;
using Ouroboros.Agent.MetaAI;
using Xunit;

namespace Ouroboros.Tests.MetaAI.SelfImprovement;

[Trait("Category", "Unit")]
public class HypothesisTestResultTests
{
    private static Hypothesis CreateTestHypothesis() => new(
        Guid.NewGuid(),
        "Test hypothesis",
        "Testing",
        0.7,
        new List<string> { "evidence1" },
        new List<string>(),
        DateTime.UtcNow,
        false,
        null);

    private static Experiment CreateTestExperiment(Hypothesis h) => new(
        Guid.NewGuid(),
        h,
        "Test experiment",
        new List<PlanStep>(),
        new Dictionary<string, object> { ["expected"] = "result" },
        DateTime.UtcNow);

    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var hypothesis = CreateTestHypothesis();
        var experiment = CreateTestExperiment(hypothesis);
        var execution = new PlanExecutionResult(true, new List<StepResult>(), "Output");
        var testedAt = DateTime.UtcNow;

        var result = new HypothesisTestResult(
            hypothesis, experiment, execution, true, 0.15, "Confirmed", testedAt);

        result.Hypothesis.Should().Be(hypothesis);
        result.Experiment.Should().Be(experiment);
        result.Execution.Should().Be(execution);
        result.HypothesisSupported.Should().BeTrue();
        result.ConfidenceAdjustment.Should().Be(0.15);
        result.Explanation.Should().Be("Confirmed");
        result.TestedAt.Should().Be(testedAt);
    }

    [Fact]
    public void RecordEquality_WithSameValues_AreEqual()
    {
        var hypothesis = CreateTestHypothesis();
        var experiment = CreateTestExperiment(hypothesis);
        var execution = new PlanExecutionResult(true, new List<StepResult>(), "Out");
        var time = DateTime.UtcNow;

        var a = new HypothesisTestResult(hypothesis, experiment, execution, true, 0.1, "ok", time);
        var b = new HypothesisTestResult(hypothesis, experiment, execution, true, 0.1, "ok", time);

        a.Should().Be(b);
    }

    [Fact]
    public void NegativeConfidenceAdjustment_Supported()
    {
        var hypothesis = CreateTestHypothesis();
        var experiment = CreateTestExperiment(hypothesis);
        var execution = new PlanExecutionResult(false, new List<StepResult>(), "Failed");

        var result = new HypothesisTestResult(
            hypothesis, experiment, execution, false, -0.2, "Rejected", DateTime.UtcNow);

        result.HypothesisSupported.Should().BeFalse();
        result.ConfidenceAdjustment.Should().Be(-0.2);
    }
}
