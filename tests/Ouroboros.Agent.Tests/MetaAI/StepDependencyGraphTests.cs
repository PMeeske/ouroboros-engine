// <copyright file="StepDependencyGraphTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class StepDependencyGraphTests
{
    [Fact]
    public void Constructor_EmptySteps_DoesNotThrow()
    {
        var act = () => new StepDependencyGraph(new List<Ouroboros.Agent.MetaAI.PlanStep>());
        act.Should().NotThrow();
    }

    [Fact]
    public void GetParallelGroups_EmptySteps_ReturnsEmpty()
    {
        var graph = new StepDependencyGraph(new List<Ouroboros.Agent.MetaAI.PlanStep>());
        var groups = graph.GetParallelGroups();
        groups.Should().BeEmpty();
    }

    [Fact]
    public void GetParallelGroups_IndependentSteps_AllInOneGroup()
    {
        var steps = new List<Ouroboros.Agent.MetaAI.PlanStep>
        {
            CreateStep("analyze", new Dictionary<string, object> { ["input"] = "data" }),
            CreateStep("summarize", new Dictionary<string, object> { ["input"] = "data" }),
            CreateStep("validate", new Dictionary<string, object> { ["input"] = "data" }),
        };

        var graph = new StepDependencyGraph(steps);
        var groups = graph.GetParallelGroups();

        groups.Should().HaveCount(1);
        groups[0].Should().HaveCount(3);
    }

    [Fact]
    public void GetParallelGroups_SequentialDependencies_OnePerGroup()
    {
        var steps = new List<Ouroboros.Agent.MetaAI.PlanStep>
        {
            CreateStep("fetch", new Dictionary<string, object> { ["url"] = "http://example.com" }),
            CreateStep("process", new Dictionary<string, object> { ["data"] = "$fetch" }),
            CreateStep("store", new Dictionary<string, object> { ["result"] = "$process" }),
        };

        var graph = new StepDependencyGraph(steps);
        var groups = graph.GetParallelGroups();

        groups.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void GetParallelGroups_MixedDependencies_GroupsCorrectly()
    {
        var steps = new List<Ouroboros.Agent.MetaAI.PlanStep>
        {
            CreateStep("stepA", new Dictionary<string, object> { ["input"] = "raw" }),
            CreateStep("stepB", new Dictionary<string, object> { ["input"] = "raw" }),
            CreateStep("stepC", new Dictionary<string, object> { ["data"] = "output_stepA" }),
        };

        var graph = new StepDependencyGraph(steps);
        var groups = graph.GetParallelGroups();

        // stepA and stepB are independent, stepC depends on stepA
        groups.Should().HaveCountGreaterThanOrEqualTo(2);
        groups[0].Should().Contain(0);
        groups[0].Should().Contain(1);
    }

    [Fact]
    public void GetParallelGroups_SingleStep_ReturnsOneGroupWithOneItem()
    {
        var steps = new List<Ouroboros.Agent.MetaAI.PlanStep>
        {
            CreateStep("only", new Dictionary<string, object> { ["x"] = "y" }),
        };

        var graph = new StepDependencyGraph(steps);
        var groups = graph.GetParallelGroups();

        groups.Should().HaveCount(1);
        groups[0].Should().HaveCount(1);
    }

    private static Ouroboros.Agent.MetaAI.PlanStep CreateStep(string action, Dictionary<string, object> parameters)
    {
        return new Ouroboros.Agent.MetaAI.PlanStep(action, parameters, $"Expected outcome for {action}", 0.8);
    }
}
