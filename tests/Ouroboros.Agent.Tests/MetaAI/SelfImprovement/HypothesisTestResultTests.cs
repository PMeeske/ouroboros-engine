// <copyright file="HypothesisTestResultTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;

namespace Ouroboros.Tests.MetaAI.SelfImprovement;

[Trait("Category", "Unit")]
public class HypothesisTestResultTests
{
    private static Hypothesis CreateHypothesis() =>
        new(Guid.NewGuid(), "Test hypothesis", "TestDomain", 0.7,
            new List<string>(), new List<string>(),
            DateTime.UtcNow, false, null);

    private static Experiment CreateExperiment(Hypothesis hypothesis) =>
        new(Guid.NewGuid(), hypothesis, "Test experiment",
            new List<PlanStep>(), new Dictionary<string, object>(),
            DateTime.UtcNow);

    private static PlanExecutionResult CreateExecutionResult()
    {
        var step = new PlanStep("TestAction", new Dictionary<string, object>(), "expected", 0.9);
        var plan = new Plan("test goal", new List<PlanStep> { step },
            new Dictionary<string, double>(), DateTime.UtcNow);
        return new PlanExecutionResult(
            plan,
            new List<StepResult>
            {
                new StepResult(step, true, "output", null, TimeSpan.FromMilliseconds(100),
                    new Dictionary<string, object>())
            },
            true, "output", new Dictionary<string, object>(), TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var hypothesis = CreateHypothesis();
        var experiment = CreateExperiment(hypothesis);
        var execution = CreateExecutionResult();
        var testedAt = DateTime.UtcNow;

        // Act
        var result = new HypothesisTestResult(
            hypothesis, experiment, execution,
            true, 0.15, "Hypothesis confirmed by experiment", testedAt);

        // Assert
        result.Hypothesis.Should().Be(hypothesis);
        result.Experiment.Should().Be(experiment);
        result.Execution.Should().Be(execution);
        result.HypothesisSupported.Should().BeTrue();
        result.ConfidenceAdjustment.Should().Be(0.15);
        result.Explanation.Should().Be("Hypothesis confirmed by experiment");
        result.TestedAt.Should().Be(testedAt);
    }

    [Fact]
    public void Constructor_WithUnsupportedHypothesis_SetsCorrectly()
    {
        var hypothesis = CreateHypothesis();
        var experiment = CreateExperiment(hypothesis);
        var execution = CreateExecutionResult();

        var result = new HypothesisTestResult(
            hypothesis, experiment, execution,
            false, -0.2, "Evidence contradicts hypothesis", DateTime.UtcNow);

        result.HypothesisSupported.Should().BeFalse();
        result.ConfidenceAdjustment.Should().BeNegative();
    }

    [Fact]
    public void Constructor_WithZeroConfidenceAdjustment_SetsCorrectly()
    {
        var hypothesis = CreateHypothesis();
        var experiment = CreateExperiment(hypothesis);
        var execution = CreateExecutionResult();

        var result = new HypothesisTestResult(
            hypothesis, experiment, execution,
            false, 0.0, "Inconclusive", DateTime.UtcNow);

        result.ConfidenceAdjustment.Should().Be(0.0);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        var hypothesis = CreateHypothesis();
        var experiment = CreateExperiment(hypothesis);
        var execution = CreateExecutionResult();

        var original = new HypothesisTestResult(
            hypothesis, experiment, execution,
            true, 0.1, "Supported", DateTime.UtcNow);

        var modified = original with { Explanation = "Strongly supported" };

        modified.Explanation.Should().Be("Strongly supported");
        modified.HypothesisSupported.Should().Be(original.HypothesisSupported);
    }
}
