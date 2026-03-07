// <copyright file="SymbolicKnowledgeBaseTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Abstractions;
using Ouroboros.Agent.NeuralSymbolic;

namespace Ouroboros.Tests.NeuralSymbolic;

[Trait("Category", "Unit")]
public class SymbolicKnowledgeBaseTests
{
    private readonly Mock<IMeTTaEngine> _engineMock = new();

    [Fact]
    public void Constructor_NullEngine_Throws()
    {
        var act = () => new SymbolicKnowledgeBase(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ValidEngine_DoesNotThrow()
    {
        var act = () => new SymbolicKnowledgeBase(_engineMock.Object);
        act.Should().NotThrow();
    }

    [Fact]
    public void RuleCount_Initially_IsZero()
    {
        var kb = new SymbolicKnowledgeBase(_engineMock.Object);
        kb.RuleCount.Should().Be(0);
    }

    [Fact]
    public async Task AddRuleAsync_NullRule_ReturnsFailure()
    {
        var kb = new SymbolicKnowledgeBase(_engineMock.Object);
        var result = await kb.AddRuleAsync(null!);
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("null");
    }

    [Fact]
    public async Task AddRuleAsync_ValidRule_IncrementsCount()
    {
        _engineMock
            .Setup(e => e.AddFactAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Result<Unit, string>.Success(Unit.Value)));

        var kb = new SymbolicKnowledgeBase(_engineMock.Object);
        var rule = CreateRule("test-rule");

        var result = await kb.AddRuleAsync(rule);

        result.IsSuccess.Should().BeTrue();
        kb.RuleCount.Should().Be(1);
    }

    [Fact]
    public async Task AddRuleAsync_WhenEngineFailsAddFact_ReturnsFailure()
    {
        _engineMock
            .Setup(e => e.AddFactAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Result<Unit, string>.Failure("engine error")));

        var kb = new SymbolicKnowledgeBase(_engineMock.Object);
        var rule = CreateRule("test-rule");

        var result = await kb.AddRuleAsync(rule);

        result.IsFailure.Should().BeTrue();
    }

    private static SymbolicRule CreateRule(string name)
    {
        return new SymbolicRule(
            name,
            $"(= ({name} $x) True)",
            $"Test rule: {name}",
            new List<string>(),
            new List<string>(),
            0.9,
            RuleSource.LearnedFromExperience);
    }
}
