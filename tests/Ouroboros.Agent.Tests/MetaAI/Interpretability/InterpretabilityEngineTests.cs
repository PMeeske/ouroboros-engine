// <copyright file="InterpretabilityEngineTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Agent.MetaAI.Interpretability;
using Ouroboros.Agent.MetaAI.SelfModel;
using AgentCapability = Ouroboros.Agent.MetaAI.AgentCapability;

namespace Ouroboros.Agent.Tests.MetaAI.Interpretability;

/// <summary>
/// Tests for the InterpretabilityEngine that explains agent decisions.
/// </summary>
[Trait("Category", "Unit")]
public class InterpretabilityEngineTests
{
    [Fact]
    public void ExplainDecision_WithNoDependencies_ReturnsEmptyFactors()
    {
        var engine = new InterpretabilityEngine();

        var explanation = engine.ExplainDecision(Guid.NewGuid(), "test decision");

        explanation.ContributingFactors.Should().BeEmpty();
        explanation.OverallConfidence.Should().Be(0.0);
        explanation.Summary.Should().Contain("test decision");
        explanation.Summary.Should().Contain("0 contributing factors");
    }

    [Fact]
    public void ExplainDecision_WithWorkspace_IncludesAttentionFactors()
    {
        var workspace = new GlobalWorkspace();
        workspace.AddItem("Critical task", WorkspacePriority.High, "TestSource");
        workspace.AddItem("Another critical", WorkspacePriority.Critical, "TestSource");

        var engine = new InterpretabilityEngine(workspace: workspace);

        var explanation = engine.ExplainDecision(Guid.NewGuid(), "workspace decision");

        explanation.ContributingFactors.Should().Contain(f => f.Source == "GlobalWorkspace");
        explanation.ContributingFactors
            .Where(f => f.Source == "GlobalWorkspace")
            .Should().HaveCountGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void ExplainDecision_WithMonitor_IncludesForecastFactors()
    {
        var monitor = new PredictiveMonitor();
        monitor.CreateForecast(
            "Test forecast",
            "cpu_usage",
            0.75,
            0.85,
            DateTime.UtcNow.AddHours(1));

        var engine = new InterpretabilityEngine(monitor: monitor);

        var explanation = engine.ExplainDecision(Guid.NewGuid(), "monitored decision");

        explanation.ContributingFactors.Should().Contain(f => f.Source == "PredictiveMonitor");
        var monitorFactor = explanation.ContributingFactors.First(f => f.Source == "PredictiveMonitor");
        monitorFactor.Description.Should().Contain("cpu_usage");
    }

    [Fact]
    public void ExplainDecision_WithIdentity_IncludesPerformanceFactor()
    {
        var mockRegistry = new Mock<ICapabilityRegistry>();
        mockRegistry.Setup(r => r.GetCapabilitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentCapability>());

        var identity = new IdentityGraph(Guid.NewGuid(), "TestAgent", mockRegistry.Object);

        var engine = new InterpretabilityEngine(identity: identity);

        var explanation = engine.ExplainDecision(Guid.NewGuid(), "identity decision");

        explanation.ContributingFactors.Should().Contain(f => f.Source == "IdentityGraph");
        var identityFactor = explanation.ContributingFactors.First(f => f.Source == "IdentityGraph");
        identityFactor.Description.Should().Contain("success rate");
    }

    [Fact]
    public void ExplainDecision_WithEthics_IncludesEthicsFactor()
    {
        var ethics = EthicsFrameworkFactory.CreateDefault();

        var engine = new InterpretabilityEngine(ethics: ethics);

        var explanation = engine.ExplainDecision(Guid.NewGuid(), "ethical decision");

        explanation.ContributingFactors.Should().Contain(f => f.Source == "EthicsFramework");
        var ethicsFactor = explanation.ContributingFactors.First(f => f.Source == "EthicsFramework");
        ethicsFactor.Description.Should().Contain("ethical principles");
        ethicsFactor.Weight.Should().Be(1.0);
        ethicsFactor.Confidence.Should().Be(1.0);
    }

    [Fact]
    public void ExplainDecision_WithAllDependencies_CombinesAllFactors()
    {
        var workspace = new GlobalWorkspace();
        workspace.AddItem("Urgent item", WorkspacePriority.High, "System");

        var monitor = new PredictiveMonitor();
        monitor.CreateForecast("f1", "metric1", 0.5, 0.8, DateTime.UtcNow.AddHours(1));

        var mockRegistry = new Mock<ICapabilityRegistry>();
        mockRegistry.Setup(r => r.GetCapabilitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentCapability>());
        var identity = new IdentityGraph(Guid.NewGuid(), "TestAgent", mockRegistry.Object);

        var ethics = EthicsFrameworkFactory.CreateDefault();

        var engine = new InterpretabilityEngine(workspace, monitor, identity, ethics);

        var explanation = engine.ExplainDecision(Guid.NewGuid(), "full decision");

        explanation.ContributingFactors.Should().HaveCountGreaterThanOrEqualTo(4);
        explanation.ContributingFactors.Select(f => f.Source).Distinct()
            .Should().Contain(new[] { "GlobalWorkspace", "PredictiveMonitor", "IdentityGraph", "EthicsFramework" });
        explanation.OverallConfidence.Should().BeGreaterThan(0.0);
    }

    [Fact]
    public void ExplainDecision_PreservesDecisionId()
    {
        var decisionId = Guid.NewGuid();
        var engine = new InterpretabilityEngine();

        var explanation = engine.ExplainDecision(decisionId, "test");

        explanation.DecisionId.Should().Be(decisionId);
    }

    [Fact]
    public void ExplainPlan_WithSteps_ReturnsStepExplanations()
    {
        var engine = new InterpretabilityEngine();
        var steps = new List<string> { "Analyze code", "Generate fix", "Apply fix", "Verify" };

        var planExplanation = engine.ExplainPlan("Fix compilation error", steps);

        planExplanation.PlanGoal.Should().Be("Fix compilation error");
        planExplanation.StepExplanations.Should().HaveCount(4);
        planExplanation.StepExplanations[0].Action.Should().Be("Analyze code");
        planExplanation.StepExplanations[0].Reasoning.Should().Contain("Fix compilation error");
        planExplanation.StepExplanations[0].AlternativesConsidered.Should().NotBeEmpty();
    }

    [Fact]
    public void ExplainPlan_WithEmptySteps_ReturnsEmptyExplanation()
    {
        var engine = new InterpretabilityEngine();

        var planExplanation = engine.ExplainPlan("Empty plan", Array.Empty<string>());

        planExplanation.StepExplanations.Should().BeEmpty();
        planExplanation.OverallConfidence.Should().Be(0.5);
    }

    [Fact]
    public void GetAttentionReport_WithNoWorkspace_ReturnsEmptyReport()
    {
        var engine = new InterpretabilityEngine();

        var report = engine.GetAttentionReport();

        report.ActiveItems.Should().BeEmpty();
        report.TotalWorkspaceSize.Should().Be(0);
        report.HighPriorityCount.Should().Be(0);
    }

    [Fact]
    public void GetAttentionReport_WithWorkspace_ReturnsActiveItems()
    {
        var workspace = new GlobalWorkspace();
        workspace.AddItem("Low priority item", WorkspacePriority.Low, "Test");
        workspace.AddItem("High priority item", WorkspacePriority.High, "Test");
        workspace.AddItem("Critical item", WorkspacePriority.Critical, "Test");

        var engine = new InterpretabilityEngine(workspace: workspace);

        var report = engine.GetAttentionReport();

        report.TotalWorkspaceSize.Should().BeGreaterThan(0);
        report.HighPriorityCount.Should().BeGreaterThanOrEqualTo(2);
        report.ActiveItems.Should().Contain(i => i.Content == "Critical item");
    }

    [Fact]
    public void GetCalibrationReport_WithNoMonitor_ReturnsZeroReport()
    {
        var engine = new InterpretabilityEngine();

        var report = engine.GetCalibrationReport();

        report.BrierScore.Should().Be(0);
        report.CalibrationError.Should().Be(0);
        report.TotalForecasts.Should().Be(0);
        report.VerifiedForecasts.Should().Be(0);
        report.FailedForecasts.Should().Be(0);
    }

    [Fact]
    public void GetCalibrationReport_WithMonitorAndForecasts_ReturnsCalibrationData()
    {
        var monitor = new PredictiveMonitor();

        // Create a forecast and resolve it
        var forecast = monitor.CreateForecast(
            "Test prediction",
            "accuracy",
            0.85,
            0.9,
            DateTime.UtcNow.AddHours(1));
        monitor.UpdateForecastOutcome(forecast.Id, 0.84); // Close to prediction → verified

        var engine = new InterpretabilityEngine(monitor: monitor);

        var report = engine.GetCalibrationReport();

        report.TotalForecasts.Should().Be(1);
        report.VerifiedForecasts.Should().Be(1);
        report.FailedForecasts.Should().Be(0);
    }

    [Fact]
    public void ExplainDecision_Workspace_LimitsToThreeItems()
    {
        var workspace = new GlobalWorkspace();
        for (int i = 0; i < 5; i++)
        {
            workspace.AddItem($"High item {i}", WorkspacePriority.High, "Test");
        }

        var engine = new InterpretabilityEngine(workspace: workspace);

        var explanation = engine.ExplainDecision(Guid.NewGuid(), "test");

        explanation.ContributingFactors
            .Count(f => f.Source == "GlobalWorkspace")
            .Should().BeLessThanOrEqualTo(3);
    }

    [Fact]
    public void ExplainDecision_Monitor_LimitsToTwoForecasts()
    {
        var monitor = new PredictiveMonitor();
        for (int i = 0; i < 5; i++)
        {
            monitor.CreateForecast($"Forecast {i}", $"metric_{i}", 0.5, 0.7, DateTime.UtcNow.AddHours(1));
        }

        var engine = new InterpretabilityEngine(monitor: monitor);

        var explanation = engine.ExplainDecision(Guid.NewGuid(), "test");

        explanation.ContributingFactors
            .Count(f => f.Source == "PredictiveMonitor")
            .Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public void GetAttentionReport_ItemsHavePriorityAndSource()
    {
        var workspace = new GlobalWorkspace();
        workspace.AddItem("Test content", WorkspacePriority.Critical, "TestModule");

        var engine = new InterpretabilityEngine(workspace: workspace);

        var report = engine.GetAttentionReport();

        report.ActiveItems.Should().Contain(i =>
            i.Content == "Test content" &&
            i.Priority == "Critical" &&
            i.Source == "TestModule");
    }
}
