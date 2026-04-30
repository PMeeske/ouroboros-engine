// <copyright file="OrchestrationExperimentTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class OrchestrationExperimentTests
{
    [Fact]
    public void Constructor_DoesNotThrow()
    {
        var act = () => new OrchestrationExperiment();
        act.Should().NotThrow();
    }

    [Fact]
    public void CompletedExperiments_InitiallyEmpty()
    {
        var experiment = new OrchestrationExperiment();
        experiment.CompletedExperiments.Should().BeEmpty();
    }

    [Fact]
    public void RunningExperiments_InitiallyEmpty()
    {
        var experiment = new OrchestrationExperiment();
        experiment.RunningExperiments.Should().BeEmpty();
    }

    [Fact]
    public async Task RunExperimentAsync_EmptyId_ReturnsFailure()
    {
        var experiment = new OrchestrationExperiment();
        var variants = new List<IModelOrchestrator>
        {
            new Mock<IModelOrchestrator>().Object,
            new Mock<IModelOrchestrator>().Object,
        };
        var prompts = new List<string> { "test" };

        var result = await experiment.RunExperimentAsync("", variants, prompts);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public async Task RunExperimentAsync_NullVariants_ReturnsFailure()
    {
        var experiment = new OrchestrationExperiment();

        var result = await experiment.RunExperimentAsync("exp1", null!, new List<string> { "test" });

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task RunExperimentAsync_TooFewVariants_ReturnsFailure()
    {
        var experiment = new OrchestrationExperiment();
        var variants = new List<IModelOrchestrator>
        {
            new Mock<IModelOrchestrator>().Object,
        };

        var result = await experiment.RunExperimentAsync("exp1", variants, new List<string> { "test" });

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("2 variants");
    }

    [Fact]
    public async Task RunExperimentAsync_NullPrompts_ReturnsFailure()
    {
        var experiment = new OrchestrationExperiment();
        var variants = new List<IModelOrchestrator>
        {
            new Mock<IModelOrchestrator>().Object,
            new Mock<IModelOrchestrator>().Object,
        };

        var result = await experiment.RunExperimentAsync("exp1", variants, null!);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task RunExperimentAsync_EmptyPrompts_ReturnsFailure()
    {
        var experiment = new OrchestrationExperiment();
        var variants = new List<IModelOrchestrator>
        {
            new Mock<IModelOrchestrator>().Object,
            new Mock<IModelOrchestrator>().Object,
        };

        var result = await experiment.RunExperimentAsync("exp1", variants, new List<string>());

        result.IsFailure.Should().BeTrue();
    }
}
