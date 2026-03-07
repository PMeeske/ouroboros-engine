// <copyright file="TransferLearnerTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;
using MetaAISkill = Ouroboros.Agent.MetaAI.Skill;
using MetaAIPlanStep = Ouroboros.Agent.MetaAI.PlanStep;

namespace Ouroboros.Tests.MetaAI.SelfImprovement;

[Trait("Category", "Unit")]
public class TransferLearnerTests
{
    private readonly Mock<IChatCompletionModel> _llmMock = new();
    private readonly Mock<ISkillRegistry> _skillsMock = new();
    private readonly Mock<IMemoryStore> _memoryMock = new();

    [Fact]
    public void Constructor_NullLlm_Throws()
    {
        var act = () => new TransferLearner(null!, _skillsMock.Object, _memoryMock.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task AdaptSkillToDomainAsync_NullSkill_ReturnsFailure()
    {
        var learner = CreateLearner();
        var result = await learner.AdaptSkillToDomainAsync(null!, "target");
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task AdaptSkillToDomainAsync_EmptyDomain_ReturnsFailure()
    {
        var learner = CreateLearner();
        var skill = new MetaAISkill("test_skill", "desc", new List<string>(), new List<MetaAIPlanStep>(),
            0.8, 5, DateTime.UtcNow, DateTime.UtcNow);

        var result = await learner.AdaptSkillToDomainAsync(skill, "  ");

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task EstimateTransferabilityAsync_NullSkill_ReturnsZero()
    {
        var learner = CreateLearner();
        var score = await learner.EstimateTransferabilityAsync(null!, "target");
        score.Should().Be(0.0);
    }

    [Fact]
    public async Task FindAnalogiesAsync_EmptyDomains_ReturnsEmpty()
    {
        var learner = CreateLearner();
        var analogies = await learner.FindAnalogiesAsync("", "target");
        analogies.Should().BeEmpty();
    }

    [Fact]
    public void GetTransferHistory_EmptyName_ReturnsEmpty()
    {
        var learner = CreateLearner();
        learner.GetTransferHistory("  ").Should().BeEmpty();
    }

    [Fact]
    public void GetTransferHistory_Unknown_ReturnsEmpty()
    {
        var learner = CreateLearner();
        learner.GetTransferHistory("unknown_skill").Should().BeEmpty();
    }

    [Fact]
    public void RecordTransferValidation_NullResult_DoesNotThrow()
    {
        var learner = CreateLearner();
        learner.RecordTransferValidation(null!, true);
    }

    private TransferLearner CreateLearner()
    {
        return new TransferLearner(_llmMock.Object, _skillsMock.Object, _memoryMock.Object);
    }
}
