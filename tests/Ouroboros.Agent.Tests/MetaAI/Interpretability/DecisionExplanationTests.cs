using FluentAssertions;
using Ouroboros.Agent.MetaAI.Interpretability;
using Xunit;

namespace Ouroboros.Tests.MetaAI.Interpretability;

[Trait("Category", "Unit")]
public class DecisionExplanationTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var id = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;
        var factors = new List<ReasoningFactor>
        {
            new("model-a", "High confidence output", 0.8, 0.95)
        };

        var explanation = new DecisionExplanation(id, "Chose model A", factors, 0.92, timestamp);

        explanation.DecisionId.Should().Be(id);
        explanation.Summary.Should().Be("Chose model A");
        explanation.ContributingFactors.Should().HaveCount(1);
        explanation.OverallConfidence.Should().Be(0.92);
        explanation.Timestamp.Should().Be(timestamp);
    }
}

[Trait("Category", "Unit")]
public class ReasoningFactorTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var factor = new ReasoningFactor("history", "Past success rate", 0.6, 0.88);

        factor.Source.Should().Be("history");
        factor.Description.Should().Be("Past success rate");
        factor.Weight.Should().Be(0.6);
        factor.Confidence.Should().Be(0.88);
    }

    [Fact]
    public void RecordEquality_WithSameValues_AreEqual()
    {
        var a = new ReasoningFactor("s", "d", 0.5, 0.9);
        var b = new ReasoningFactor("s", "d", 0.5, 0.9);

        a.Should().Be(b);
    }
}

[Trait("Category", "Unit")]
public class PlanExplanationTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var steps = new List<StepExplanation>
        {
            new("Fetch data", "Required for analysis", ["Cache lookup", "Direct API call"])
        };

        var explanation = new PlanExplanation("Analyze metrics", steps, 0.85);

        explanation.PlanGoal.Should().Be("Analyze metrics");
        explanation.StepExplanations.Should().HaveCount(1);
        explanation.OverallConfidence.Should().Be(0.85);
    }
}

[Trait("Category", "Unit")]
public class StepExplanationTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var alternatives = new List<string> { "Option A", "Option B" };
        var step = new StepExplanation("Query database", "Fastest data source", alternatives);

        step.Action.Should().Be("Query database");
        step.Reasoning.Should().Be("Fastest data source");
        step.AlternativesConsidered.Should().HaveCount(2);
    }
}

[Trait("Category", "Unit")]
public class AttentionReportTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var items = new List<AttentionItem>
        {
            new("User query", "High", "input"),
            new("Context window", "Medium", "memory")
        };

        var report = new AttentionReport(items, 50, 1);

        report.ActiveItems.Should().HaveCount(2);
        report.TotalWorkspaceSize.Should().Be(50);
        report.HighPriorityCount.Should().Be(1);
    }
}

[Trait("Category", "Unit")]
public class AttentionItemTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var item = new AttentionItem("Goal tracking", "High", "planner");

        item.Content.Should().Be("Goal tracking");
        item.Priority.Should().Be("High");
        item.Source.Should().Be("planner");
    }
}

[Trait("Category", "Unit")]
public class CalibrationReportTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var report = new CalibrationReport(0.15, 0.08, 100, 85, 15);

        report.BrierScore.Should().Be(0.15);
        report.CalibrationError.Should().Be(0.08);
        report.TotalForecasts.Should().Be(100);
        report.VerifiedForecasts.Should().Be(85);
        report.FailedForecasts.Should().Be(15);
    }

    [Fact]
    public void RecordEquality_WithSameValues_AreEqual()
    {
        var a = new CalibrationReport(0.1, 0.05, 50, 45, 5);
        var b = new CalibrationReport(0.1, 0.05, 50, 45, 5);

        a.Should().Be(b);
    }
}
