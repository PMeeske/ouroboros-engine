// <copyright file="MetaLearnerTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.MetaAI.MetaLearning;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;

namespace Ouroboros.Tests.MetaAI.MetaLearning;

[Trait("Category", "Unit")]
public class MetaLearnerTests
{
    private readonly Mock<IChatCompletionModel> _llmMock = new();
    private readonly Mock<ISkillRegistry> _skillsMock = new();
    private readonly Mock<IMemoryStore> _memoryMock = new();

    [Fact]
    public void Constructor_NullLlm_Throws()
    {
        var act = () => new MetaLearner(null!, _skillsMock.Object, _memoryMock.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullSkillRegistry_Throws()
    {
        var act = () => new MetaLearner(_llmMock.Object, null!, _memoryMock.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullMemory_Throws()
    {
        var act = () => new MetaLearner(_llmMock.Object, _skillsMock.Object, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task OptimizeLearningStrategyAsync_NullHistory_ReturnsFailure()
    {
        var learner = new MetaLearner(_llmMock.Object, _skillsMock.Object, _memoryMock.Object);
        var result = await learner.OptimizeLearningStrategyAsync(null!);
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task OptimizeLearningStrategyAsync_InsufficientHistory_ReturnsFailure()
    {
        var learner = new MetaLearner(_llmMock.Object, _skillsMock.Object, _memoryMock.Object);
        var result = await learner.OptimizeLearningStrategyAsync(new List<LearningEpisode>());
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Insufficient");
    }
}
