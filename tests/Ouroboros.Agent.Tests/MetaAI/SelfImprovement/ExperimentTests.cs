// <copyright file="ExperimentTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;

namespace Ouroboros.Tests.MetaAI.SelfImprovement;

[Trait("Category", "Unit")]
public class ExperimentTests
{
    private static Hypothesis CreateHypothesis() =>
        new(Guid.NewGuid(), "Test hypothesis", "TestDomain", 0.7,
            new List<string> { "evidence1" }, new List<string>(),
            DateTime.UtcNow, false, null);

    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var hypothesis = CreateHypothesis();
        var description = "Run benchmark test";
        var steps = new List<PlanStep>
        {
            new("Step 1", new Dictionary<string, object>(), "result 1", 0.9)
        };
        var expectedOutcomes = new Dictionary<string, object> { ["accuracy"] = 0.95 };
        var designedAt = DateTime.UtcNow;

        // Act
        var experiment = new Experiment(id, hypothesis, description, steps, expectedOutcomes, designedAt);

        // Assert
        experiment.Id.Should().Be(id);
        experiment.Hypothesis.Should().Be(hypothesis);
        experiment.Description.Should().Be(description);
        experiment.Steps.Should().HaveCount(1);
        experiment.ExpectedOutcomes.Should().ContainKey("accuracy");
        experiment.DesignedAt.Should().Be(designedAt);
    }

    [Fact]
    public void Constructor_WithEmptySteps_Succeeds()
    {
        var experiment = new Experiment(
            Guid.NewGuid(), CreateHypothesis(), "desc",
            new List<PlanStep>(), new Dictionary<string, object>(),
            DateTime.UtcNow);

        experiment.Steps.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithEmptyExpectedOutcomes_Succeeds()
    {
        var experiment = new Experiment(
            Guid.NewGuid(), CreateHypothesis(), "desc",
            new List<PlanStep>(), new Dictionary<string, object>(),
            DateTime.UtcNow);

        experiment.ExpectedOutcomes.Should().BeEmpty();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var id = Guid.NewGuid();
        var hypothesis = CreateHypothesis();
        var steps = new List<PlanStep>();
        var outcomes = new Dictionary<string, object>();
        var time = DateTime.UtcNow;

        var a = new Experiment(id, hypothesis, "desc", steps, outcomes, time);
        var b = new Experiment(id, hypothesis, "desc", steps, outcomes, time);

        a.Should().Be(b);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        var original = new Experiment(
            Guid.NewGuid(), CreateHypothesis(), "original",
            new List<PlanStep>(), new Dictionary<string, object>(),
            DateTime.UtcNow);

        var modified = original with { Description = "modified" };

        modified.Description.Should().Be("modified");
        modified.Id.Should().Be(original.Id);
    }

    [Fact]
    public void Constructor_WithMultipleSteps_PreservesOrder()
    {
        var steps = new List<PlanStep>
        {
            new("Step 1", new Dictionary<string, object>(), "result 1", 0.9),
            new("Step 2", new Dictionary<string, object>(), "result 2", 0.8),
            new("Step 3", new Dictionary<string, object>(), "result 3", 0.7)
        };

        var experiment = new Experiment(
            Guid.NewGuid(), CreateHypothesis(), "multi-step",
            steps, new Dictionary<string, object>(), DateTime.UtcNow);

        experiment.Steps.Should().HaveCount(3);
        experiment.Steps[0].Action.Should().Be("Step 1");
        experiment.Steps[2].Action.Should().Be("Step 3");
    }
}
