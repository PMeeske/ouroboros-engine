// <copyright file="MindPipelineBuilderTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Moq;
using Ouroboros.Abstractions.Core;
using Ouroboros.Agent.ConsolidatedMind;

namespace Ouroboros.Tests.ConsolidatedMind;

[Trait("Category", "Unit")]
public sealed class MindPipelineBuilderTests : IDisposable
{
    private readonly Agent.ConsolidatedMind.ConsolidatedMind _mind;

    public MindPipelineBuilderTests()
    {
        _mind = new Agent.ConsolidatedMind.ConsolidatedMind(MindConfig.Minimal());
        var mockModel = new Mock<IChatCompletionModel>();
        mockModel.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("test response");
        _mind.RegisterSpecialist(new SpecializedModel(
            SpecializedRole.QuickResponse, mockModel.Object, "test", new[] { "general" }));
    }

    public void Dispose() => _mind.Dispose();

    [Fact]
    public void Build_WithNoSteps_ReturnsIdentityStep()
    {
        // Arrange
        var builder = ConsolidatedMindArrows.CreatePipeline(_mind);

        // Act
        var step = builder.Build();

        // Assert — identity step should return the same branch
        step.Should().NotBeNull();
    }

    [Fact]
    public void WithStep_AddsCustomStep()
    {
        // Arrange
        var builder = ConsolidatedMindArrows.CreatePipeline(_mind);
        Step<PipelineBranch, PipelineBranch> customStep = branch => Task.FromResult(branch);

        // Act
        var result = builder.WithStep(customStep);

        // Assert — returns same builder for fluent chaining
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithVerification_ReturnsSameBuilder()
    {
        // Arrange
        var builder = ConsolidatedMindArrows.CreatePipeline(_mind);

        // Act
        var result = builder.WithVerification();

        // Assert
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void CreatePipeline_ReturnsNewBuilder()
    {
        // Act
        var builder = ConsolidatedMindArrows.CreatePipeline(_mind);

        // Assert
        builder.Should().NotBeNull();
    }
}
