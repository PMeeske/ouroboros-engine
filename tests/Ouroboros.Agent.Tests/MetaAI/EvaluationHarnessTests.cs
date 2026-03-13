// <copyright file="EvaluationHarnessTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using PlanStep = Ouroboros.Agent.PlanStep;
using Plan = Ouroboros.Agent.MetaAI.Plan;
using Skill = Ouroboros.Agent.MetaAI.Skill;

namespace Ouroboros.Agent.Tests.MetaAI;

/// <summary>
/// Unit tests for the EvaluationHarness Meta-AI benchmarking component.
/// </summary>
[Trait("Category", "Unit")]
public class EvaluationHarnessTests
{
    private readonly Mock<IMetaAIPlannerOrchestrator> _mockOrchestrator;
    private readonly EvaluationHarness _harness;

    public EvaluationHarnessTests()
    {
        _mockOrchestrator = new Mock<IMetaAIPlannerOrchestrator>();
        _harness = new EvaluationHarness(_mockOrchestrator.Object);
    }

    private void SetupSuccessfulOrchestration(
        string goal = "test goal",
        double qualityScore = 0.9,
        bool verified = true)
    {
        var planSteps = new List<PlanStep>
        {
            new PlanStep("step1", new Dictionary<string, object>(), "expected1", 0.9),
            new PlanStep("step2", new Dictionary<string, object>(), "expected2", 0.85)
        };

        var plan = new Plan(
            goal,
            planSteps,
            new Dictionary<string, double> { ["overall"] = 0.8 },
            DateTime.UtcNow);

        var stepResults = new List<StepResult>
        {
            new StepResult(planSteps[0], true, "output1", null, TimeSpan.FromMilliseconds(50),
                new Dictionary<string, object>()),
            new StepResult(planSteps[1], true, "output2", null, TimeSpan.FromMilliseconds(75),
                new Dictionary<string, object>())
        };

        var execution = new PlanExecutionResult(
            plan,
            stepResults,
            true,
            "Final output",
            new Dictionary<string, object>(),
            TimeSpan.FromMilliseconds(125));

        var verification = new PlanVerificationResult(
            execution,
            verified,
            qualityScore,
            new List<string>(),
            new List<string> { "Consider caching" },
            DateTime.UtcNow);

        _mockOrchestrator
            .Setup(o => o.PlanAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Plan, string>.Success(plan));

        _mockOrchestrator
            .Setup(o => o.ExecuteAsync(It.IsAny<Plan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PlanExecutionResult, string>.Success(execution));

        _mockOrchestrator
            .Setup(o => o.VerifyAsync(It.IsAny<PlanExecutionResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PlanVerificationResult, string>.Success(verification));
    }

    [Fact]
    public async Task EvaluateTestCaseAsync_SuccessfulOrchestration_ReturnsSuccessMetrics()
    {
        // Arrange
        SetupSuccessfulOrchestration(qualityScore: 0.92, verified: true);

        var testCase = new TestCase("Addition Test", "Calculate 2 + 3", null, null);

        // Act
        var metrics = await _harness.EvaluateTestCaseAsync(testCase);

        // Assert
        metrics.Success.Should().BeTrue();
        metrics.QualityScore.Should().Be(0.92);
        metrics.PlanSteps.Should().Be(2);
        metrics.TestCase.Should().Be("Addition Test");
        metrics.CustomMetrics.Should().ContainKey("steps_completed");
        metrics.CustomMetrics["steps_completed"].Should().Be(2);
        metrics.CustomMetrics["steps_successful"].Should().Be(2);

        _mockOrchestrator.Verify(o => o.LearnFromExecution(It.IsAny<PlanVerificationResult>()), Times.Once);
    }

    [Fact]
    public async Task EvaluateTestCaseAsync_PlanningFails_ReturnsFailureMetrics()
    {
        // Arrange
        _mockOrchestrator
            .Setup(o => o.PlanAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Plan, string>.Failure("Unable to create a plan for this goal"));

        var testCase = new TestCase("Impossible Task", "Do the impossible", null, null);

        // Act
        var metrics = await _harness.EvaluateTestCaseAsync(testCase);

        // Assert
        metrics.Success.Should().BeFalse();
        metrics.QualityScore.Should().Be(0.0);
        metrics.PlanSteps.Should().Be(0);
        metrics.CustomMetrics.Should().ContainKey("error");
        metrics.CustomMetrics["error"].Should().Be(1.0);
    }

    [Fact]
    public async Task EvaluateBatchAsync_MultipleCases_ReturnsAggregatedResults()
    {
        // Arrange
        SetupSuccessfulOrchestration(qualityScore: 0.85, verified: true);

        var testCases = new List<TestCase>
        {
            new TestCase("Test A", "Goal A", null, null),
            new TestCase("Test B", "Goal B", null, null),
            new TestCase("Test C", "Goal C", null, null)
        };

        // Act
        var results = await _harness.EvaluateBatchAsync(testCases);

        // Assert
        results.TotalTests.Should().Be(3);
        results.SuccessfulTests.Should().Be(3);
        results.FailedTests.Should().Be(0);
        results.AverageQualityScore.Should().Be(0.85);
        results.TestResults.Should().HaveCount(3);
        results.AggregatedMetrics.Should().ContainKey("success_rate");
        results.AggregatedMetrics["success_rate"].Should().Be(1.0);
    }

    [Fact]
    public async Task RunBenchmarkAsync_ExecutesPredefinedCases_ReturnsResults()
    {
        // Arrange
        SetupSuccessfulOrchestration(qualityScore: 0.88, verified: true);

        // Act
        var results = await _harness.RunBenchmarkAsync();

        // Assert
        results.TotalTests.Should().Be(5);
        results.TestResults.Should().HaveCount(5);
        results.AverageQualityScore.Should().BeGreaterThan(0);
        results.AggregatedMetrics.Should().ContainKey("avg_quality");

        // Verify the orchestrator was called for each benchmark case
        _mockOrchestrator.Verify(
            o => o.PlanAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>?>(), It.IsAny<CancellationToken>()),
            Times.Exactly(5));
    }
}
