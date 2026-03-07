using FluentAssertions;
using Moq;
using Ouroboros.Agent.ConsolidatedMind;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;

namespace Ouroboros.Tests.ConsolidatedMind;

[Trait("Category", "Unit")]
public sealed class MindPipelineBuilderTests
{
    [Fact]
    public async Task Build_NoSteps_ReturnsIdentity()
    {
        using var mind = new Agent.ConsolidatedMind.ConsolidatedMind();
        var builder = ConsolidatedMindArrows.CreatePipeline(mind);

        var pipeline = builder.Build();
        var branch = CreateTestBranch();
        var result = await pipeline(branch);

        result.Should().BeSameAs(branch);
    }

    [Fact]
    public void WithStep_AddsCustomStep()
    {
        using var mind = new Agent.ConsolidatedMind.ConsolidatedMind();
        var builder = ConsolidatedMindArrows.CreatePipeline(mind);

        Step<PipelineBranch, PipelineBranch> customStep = branch => Task.FromResult(branch);
        builder.WithStep(customStep);

        var pipeline = builder.Build();
        pipeline.Should().NotBeNull();
    }

    [Fact]
    public async Task Build_MultipleSteps_ExecutesInOrder()
    {
        using var mind = new Agent.ConsolidatedMind.ConsolidatedMind();
        var builder = ConsolidatedMindArrows.CreatePipeline(mind);

        var executionOrder = new List<int>();

        builder.WithStep(async branch =>
        {
            executionOrder.Add(1);
            return branch;
        });
        builder.WithStep(async branch =>
        {
            executionOrder.Add(2);
            return branch;
        });
        builder.WithStep(async branch =>
        {
            executionOrder.Add(3);
            return branch;
        });

        var pipeline = builder.Build();
        await pipeline(CreateTestBranch());

        executionOrder.Should().ContainInOrder(1, 2, 3);
    }

    [Fact]
    public void WithVerification_FluentlyChains()
    {
        using var mind = new Agent.ConsolidatedMind.ConsolidatedMind();
        var builder = ConsolidatedMindArrows.CreatePipeline(mind);

        var result = builder.WithVerification();

        result.Should().BeSameAs(builder);
    }

    private static PipelineBranch CreateTestBranch()
    {
        var storeMock = new Mock<Ouroboros.Domain.IVectorStore>();
        return PipelineBranch.Create(storeMock.Object);
    }
}
