using Ouroboros.Agent.MetaAI.Interpretability;

namespace Ouroboros.Agent.Tests.Interpretability;

[Trait("Category", "Unit")]
public class InterpretabilityRecordsTests
{
    #region DecisionExplanation

    [Fact]
    public void DecisionExplanation_Creation_ShouldSetProperties()
    {
        var factors = new List<ReasoningFactor>
        {
            new ReasoningFactor("source1", "desc1", 0.5, 0.9)
        };
        var timestamp = DateTime.UtcNow;
        var explanation = new DecisionExplanation(Guid.NewGuid(), "summary", factors, 0.85, timestamp);

        explanation.Summary.Should().Be("summary");
        explanation.ContributingFactors.Should().BeEquivalentTo(factors);
        explanation.OverallConfidence.Should().Be(0.85);
        explanation.Timestamp.Should().Be(timestamp);
    }

    #endregion

    #region ReasoningFactor

    [Fact]
    public void ReasoningFactor_Creation_ShouldSetProperties()
    {
        var factor = new ReasoningFactor("memory", "past experience", 0.6, 0.8);

        factor.Source.Should().Be("memory");
        factor.Description.Should().Be("past experience");
        factor.Weight.Should().Be(0.6);
        factor.Confidence.Should().Be(0.8);
    }

    #endregion

    #region PlanExplanation

    [Fact]
    public void PlanExplanation_Creation_ShouldSetProperties()
    {
        var steps = new List<StepExplanation>
        {
            new StepExplanation("action1", "reason1", new List<string> { "alt1" })
        };
        var explanation = new PlanExplanation("goal", steps, 0.9);

        explanation.PlanGoal.Should().Be("goal");
        explanation.StepExplanations.Should().BeEquivalentTo(steps);
        explanation.OverallConfidence.Should().Be(0.9);
    }

    #endregion

    #region StepExplanation

    [Fact]
    public void StepExplanation_Creation_ShouldSetProperties()
    {
        var alts = new List<string> { "alternative1" };
        var step = new StepExplanation("action", "reasoning", alts);

        step.Action.Should().Be("action");
        step.Reasoning.Should().Be("reasoning");
        step.AlternativesConsidered.Should().BeEquivalentTo(alts);
    }

    #endregion

    #region AttentionReport

    [Fact]
    public void AttentionReport_Creation_ShouldSetProperties()
    {
        var items = new List<AttentionItem>
        {
            new AttentionItem("content", "High", "source")
        };
        var report = new AttentionReport(items, 10, 2);

        report.ActiveItems.Should().BeEquivalentTo(items);
        report.TotalWorkspaceSize.Should().Be(10);
        report.HighPriorityCount.Should().Be(2);
    }

    #endregion

    #region AttentionItem

    [Fact]
    public void AttentionItem_Creation_ShouldSetProperties()
    {
        var item = new AttentionItem("content", "Normal", "memory");

        item.Content.Should().Be("content");
        item.Priority.Should().Be("Normal");
        item.Source.Should().Be("memory");
    }

    #endregion

    #region CalibrationReport

    [Fact]
    public void CalibrationReport_Creation_ShouldSetProperties()
    {
        var report = new CalibrationReport(0.15, 0.05, 100, 90, 10);

        report.BrierScore.Should().Be(0.15);
        report.CalibrationError.Should().Be(0.05);
        report.TotalForecasts.Should().Be(100);
        report.VerifiedForecasts.Should().Be(90);
        report.FailedForecasts.Should().Be(10);
    }

    #endregion
}
