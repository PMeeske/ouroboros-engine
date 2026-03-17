// <copyright file="DecisionExplanationTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.MetaAI.Interpretability;
using Ouroboros.Agent.MetaAI.SelfModel;
using Ouroboros.Core.Ethics;

namespace Ouroboros.Agent.Tests.MetaAI.Interpretability;

/// <summary>
/// Tests for DecisionExplanation records and InterpretabilityEngine methods
/// beyond what InterpretabilityEngineTests covers (ExplainPlan, GetAttentionReport,
/// GetCalibrationReport, and record construction).
/// </summary>
[Trait("Category", "Unit")]
public class DecisionExplanationTests
{
    // ── DecisionExplanation record ──────────────────────────────────

    [Fact]
    public void DecisionExplanation_RecordProperties_AreSetCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var factors = new List<ReasoningFactor>
        {
            new("source1", "desc1", 0.8, 0.9),
            new("source2", "desc2", 0.5, 0.7)
        };
        var timestamp = DateTime.UtcNow;

        // Act
        var explanation = new DecisionExplanation(id, "Test summary", factors, 0.85, timestamp);

        // Assert
        explanation.DecisionId.Should().Be(id);
        explanation.Summary.Should().Be("Test summary");
        explanation.ContributingFactors.Should().HaveCount(2);
        explanation.OverallConfidence.Should().Be(0.85);
        explanation.Timestamp.Should().Be(timestamp);
    }

    // ── ReasoningFactor record ──────────────────────────────────────

    [Fact]
    public void ReasoningFactor_RecordProperties_AreSetCorrectly()
    {
        // Act
        var factor = new ReasoningFactor("GlobalWorkspace", "High-priority item", 0.9, 0.95);

        // Assert
        factor.Source.Should().Be("GlobalWorkspace");
        factor.Description.Should().Be("High-priority item");
        factor.Weight.Should().Be(0.9);
        factor.Confidence.Should().Be(0.95);
    }

    // ── PlanExplanation record ──────────────────────────────────────

    [Fact]
    public void PlanExplanation_RecordProperties_AreSetCorrectly()
    {
        // Arrange
        var steps = new List<StepExplanation>
        {
            new("Action1", "Reasoning1", new[] { "Alt1", "Alt2" }),
            new("Action2", "Reasoning2", new[] { "Alt3" })
        };

        // Act
        var planExplanation = new PlanExplanation("Achieve goal", steps, 0.75);

        // Assert
        planExplanation.PlanGoal.Should().Be("Achieve goal");
        planExplanation.StepExplanations.Should().HaveCount(2);
        planExplanation.OverallConfidence.Should().Be(0.75);
    }

    // ── StepExplanation record ──────────────────────────────────────

    [Fact]
    public void StepExplanation_RecordProperties_AreSetCorrectly()
    {
        // Act
        var step = new StepExplanation("Do something", "Because it helps", new[] { "Option A", "Option B" });

        // Assert
        step.Action.Should().Be("Do something");
        step.Reasoning.Should().Be("Because it helps");
        step.AlternativesConsidered.Should().HaveCount(2);
    }

    // ── AttentionReport record ──────────────────────────────────────

    [Fact]
    public void AttentionReport_RecordProperties_AreSetCorrectly()
    {
        // Arrange
        var items = new List<AttentionItem>
        {
            new("Content1", "High", "Source1"),
            new("Content2", "Critical", "Source2")
        };

        // Act
        var report = new AttentionReport(items, 10, 2);

        // Assert
        report.ActiveItems.Should().HaveCount(2);
        report.TotalWorkspaceSize.Should().Be(10);
        report.HighPriorityCount.Should().Be(2);
    }

    // ── CalibrationReport record ────────────────────────────────────

    [Fact]
    public void CalibrationReport_RecordProperties_AreSetCorrectly()
    {
        // Act
        var report = new CalibrationReport(0.15, 0.05, 100, 80, 20);

        // Assert
        report.BrierScore.Should().Be(0.15);
        report.CalibrationError.Should().Be(0.05);
        report.TotalForecasts.Should().Be(100);
        report.VerifiedForecasts.Should().Be(80);
        report.FailedForecasts.Should().Be(20);
    }

    // ── InterpretabilityEngine.ExplainPlan ──────────────────────────

    [Fact]
    public void ExplainPlan_EmptySteps_ReturnsEmptyExplanation()
    {
        // Arrange
        var engine = new InterpretabilityEngine();

        // Act
        var explanation = engine.ExplainPlan("Some goal", Array.Empty<string>());

        // Assert
        explanation.PlanGoal.Should().Be("Some goal");
        explanation.StepExplanations.Should().BeEmpty();
        explanation.OverallConfidence.Should().Be(0.5);
    }

    [Fact]
    public void ExplainPlan_WithSteps_ReturnsExplanationForEachStep()
    {
        // Arrange
        var engine = new InterpretabilityEngine();
        var steps = new[] { "Gather data", "Analyze results", "Generate report" };

        // Act
        var explanation = engine.ExplainPlan("Complete analysis", steps);

        // Assert
        explanation.StepExplanations.Should().HaveCount(3);
        explanation.StepExplanations[0].Action.Should().Be("Gather data");
        explanation.StepExplanations[1].Action.Should().Be("Analyze results");
        explanation.StepExplanations[2].Action.Should().Be("Generate report");
    }

    [Fact]
    public void ExplainPlan_StepReasoningContainsPlanGoal()
    {
        // Arrange
        var engine = new InterpretabilityEngine();

        // Act
        var explanation = engine.ExplainPlan("Optimize performance", new[] { "Profile code" });

        // Assert
        explanation.StepExplanations[0].Reasoning.Should().Contain("Optimize performance");
    }

    [Fact]
    public void ExplainPlan_StepsHaveAlternativesConsidered()
    {
        // Arrange
        var engine = new InterpretabilityEngine();

        // Act
        var explanation = engine.ExplainPlan("Goal", new[] { "Step1" });

        // Assert
        explanation.StepExplanations[0].AlternativesConsidered.Should().NotBeEmpty();
    }

    // ── InterpretabilityEngine.GetAttentionReport ───────────────────

    [Fact]
    public void GetAttentionReport_NoWorkspace_ReturnsEmptyReport()
    {
        // Arrange
        var engine = new InterpretabilityEngine();

        // Act
        var report = engine.GetAttentionReport();

        // Assert
        report.ActiveItems.Should().BeEmpty();
        report.TotalWorkspaceSize.Should().Be(0);
        report.HighPriorityCount.Should().Be(0);
    }

    [Fact]
    public void GetAttentionReport_WithWorkspace_ReturnsPopulatedReport()
    {
        // Arrange
        var workspace = new GlobalWorkspace();
        workspace.AddItem("Important task", WorkspacePriority.High, "TestSource");
        workspace.AddItem("Critical task", WorkspacePriority.Critical, "TestSource");

        var engine = new InterpretabilityEngine(workspace: workspace);

        // Act
        var report = engine.GetAttentionReport();

        // Assert
        report.ActiveItems.Should().NotBeEmpty();
        report.TotalWorkspaceSize.Should().BeGreaterThan(0);
    }

    // ── InterpretabilityEngine.GetCalibrationReport ─────────────────

    [Fact]
    public void GetCalibrationReport_NoMonitor_ReturnsZeroReport()
    {
        // Arrange
        var engine = new InterpretabilityEngine();

        // Act
        var report = engine.GetCalibrationReport();

        // Assert
        report.BrierScore.Should().Be(0);
        report.CalibrationError.Should().Be(0);
        report.TotalForecasts.Should().Be(0);
        report.VerifiedForecasts.Should().Be(0);
        report.FailedForecasts.Should().Be(0);
    }

    [Fact]
    public void GetCalibrationReport_WithMonitor_ReturnsCalibrationData()
    {
        // Arrange
        var monitor = new PredictiveMonitor();
        monitor.CreateForecast("test", "metric", 0.8, 0.9, DateTime.UtcNow.AddHours(1));

        var engine = new InterpretabilityEngine(monitor: monitor);

        // Act
        var report = engine.GetCalibrationReport();

        // Assert — at least we get a report back (exact values depend on monitor state)
        report.Should().NotBeNull();
    }

    // ── InterpretabilityEngine.ExplainDecision with IdentityGraph ───

    [Fact]
    public void ExplainDecision_WithEthicsFramework_IncludesEthicsFactors()
    {
        // Arrange
        var ethicsMock = new Mock<IEthicsFramework>();
        ethicsMock.Setup(e => e.GetCorePrinciples())
            .Returns(new List<string> { "Beneficence", "Non-maleficence", "Autonomy" });

        var engine = new InterpretabilityEngine(ethics: ethicsMock.Object);

        // Act
        var explanation = engine.ExplainDecision(Guid.NewGuid(), "ethical decision");

        // Assert
        explanation.ContributingFactors.Should().Contain(f => f.Source == "EthicsFramework");
        explanation.ContributingFactors
            .First(f => f.Source == "EthicsFramework")
            .Description.Should().Contain("3 ethical principles");
    }

    [Fact]
    public void ExplainDecision_WithAllComponents_CombinesAllSources()
    {
        // Arrange
        var workspace = new GlobalWorkspace();
        workspace.AddItem("High priority", WorkspacePriority.High, "Test");

        var monitor = new PredictiveMonitor();
        monitor.CreateForecast("forecast", "metric", 0.5, 0.8, DateTime.UtcNow.AddHours(1));

        var ethicsMock = new Mock<IEthicsFramework>();
        ethicsMock.Setup(e => e.GetCorePrinciples()).Returns(new List<string> { "Principle1" });

        var engine = new InterpretabilityEngine(workspace, monitor, ethics: ethicsMock.Object);

        // Act
        var explanation = engine.ExplainDecision(Guid.NewGuid(), "complex decision");

        // Assert — should have factors from multiple sources
        var sources = explanation.ContributingFactors.Select(f => f.Source).Distinct().ToList();
        sources.Should().Contain("GlobalWorkspace");
        sources.Should().Contain("EthicsFramework");
        explanation.OverallConfidence.Should().BeGreaterThan(0.0);
    }
}
