// <copyright file="Phase2OrchestratorBuilderTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;

namespace Ouroboros.Tests.MetaAI.SelfImprovement;

[Trait("Category", "Unit")]
public class Phase2OrchestratorBuilderTests
{
    private readonly Mock<IChatCompletionModel> _llmMock = new();

    // ── WithLLM ────────────────────────────────────────────────────

    [Fact]
    public void WithLLM_NullLlm_Throws()
    {
        var builder = new Phase2OrchestratorBuilder();

        var act = () => builder.WithLLM(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WithLLM_ReturnsSameBuilder()
    {
        var builder = new Phase2OrchestratorBuilder();

        var result = builder.WithLLM(_llmMock.Object);

        result.Should().BeSameAs(builder);
    }

    // ── WithTools ──────────────────────────────────────────────────

    [Fact]
    public void WithTools_NullTools_Throws()
    {
        var builder = new Phase2OrchestratorBuilder();

        var act = () => builder.WithTools(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WithTools_ReturnsSameBuilder()
    {
        var builder = new Phase2OrchestratorBuilder();

        var result = builder.WithTools(new ToolRegistry());

        result.Should().BeSameAs(builder);
    }

    // ── WithMemory ─────────────────────────────────────────────────

    [Fact]
    public void WithMemory_NullMemory_Throws()
    {
        var builder = new Phase2OrchestratorBuilder();

        var act = () => builder.WithMemory(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WithMemory_ReturnsSameBuilder()
    {
        var builder = new Phase2OrchestratorBuilder();
        var memoryMock = new Mock<IMemoryStore>();

        var result = builder.WithMemory(memoryMock.Object);

        result.Should().BeSameAs(builder);
    }

    // ── WithMemoryConfig ───────────────────────────────────────────

    [Fact]
    public void WithMemoryConfig_NullConfig_Throws()
    {
        var builder = new Phase2OrchestratorBuilder();

        var act = () => builder.WithMemoryConfig(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WithMemoryConfig_ReturnsSameBuilder()
    {
        var builder = new Phase2OrchestratorBuilder();

        var result = builder.WithMemoryConfig(new PersistentMemoryConfig());

        result.Should().BeSameAs(builder);
    }

    // ── WithSkills ─────────────────────────────────────────────────

    [Fact]
    public void WithSkills_NullSkills_Throws()
    {
        var builder = new Phase2OrchestratorBuilder();

        var act = () => builder.WithSkills(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ── WithSkillExtractionConfig ──────────────────────────────────

    [Fact]
    public void WithSkillExtractionConfig_NullConfig_Throws()
    {
        var builder = new Phase2OrchestratorBuilder();

        var act = () => builder.WithSkillExtractionConfig(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ── WithCapabilityConfig ───────────────────────────────────────

    [Fact]
    public void WithCapabilityConfig_NullConfig_Throws()
    {
        var builder = new Phase2OrchestratorBuilder();

        var act = () => builder.WithCapabilityConfig(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ── WithGoalConfig ─────────────────────────────────────────────

    [Fact]
    public void WithGoalConfig_NullConfig_Throws()
    {
        var builder = new Phase2OrchestratorBuilder();

        var act = () => builder.WithGoalConfig(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ── WithEvaluatorConfig ────────────────────────────────────────

    [Fact]
    public void WithEvaluatorConfig_NullConfig_Throws()
    {
        var builder = new Phase2OrchestratorBuilder();

        var act = () => builder.WithEvaluatorConfig(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ── WithSafety ─────────────────────────────────────────────────

    [Fact]
    public void WithSafety_NullSafety_Throws()
    {
        var builder = new Phase2OrchestratorBuilder();

        var act = () => builder.WithSafety(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ── WithEthics ─────────────────────────────────────────────────

    [Fact]
    public void WithEthics_NullEthics_Throws()
    {
        var builder = new Phase2OrchestratorBuilder();

        var act = () => builder.WithEthics(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ── WithConfidenceThreshold ────────────────────────────────────

    [Fact]
    public void WithConfidenceThreshold_ReturnsSameBuilder()
    {
        var builder = new Phase2OrchestratorBuilder();

        var result = builder.WithConfidenceThreshold(0.5);

        result.Should().BeSameAs(builder);
    }

    // ── Build ──────────────────────────────────────────────────────

    [Fact]
    public void Build_WithoutLLM_ThrowsInvalidOperationException()
    {
        var builder = new Phase2OrchestratorBuilder()
            .WithTools(new ToolRegistry());

        var act = () => builder.Build();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*LLM*");
    }

    [Fact]
    public void Build_WithoutTools_ThrowsInvalidOperationException()
    {
        var builder = new Phase2OrchestratorBuilder()
            .WithLLM(_llmMock.Object);

        var act = () => builder.Build();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Tools*");
    }

    [Fact]
    public void Build_WithRequiredComponents_ReturnsTuple()
    {
        var builder = new Phase2OrchestratorBuilder()
            .WithLLM(_llmMock.Object)
            .WithTools(new ToolRegistry());

        var result = builder.Build();

        result.Orchestrator.Should().NotBeNull();
        result.CapabilityRegistry.Should().NotBeNull();
        result.GoalHierarchy.Should().NotBeNull();
        result.SelfEvaluator.Should().NotBeNull();
    }

    // ── Fluent chaining ────────────────────────────────────────────

    [Fact]
    public void FluentChaining_AllMethods_ReturnSameBuilder()
    {
        var builder = new Phase2OrchestratorBuilder();

        var result = builder
            .WithLLM(_llmMock.Object)
            .WithTools(new ToolRegistry())
            .WithMemoryConfig(new PersistentMemoryConfig())
            .WithConfidenceThreshold(0.8);

        result.Should().BeSameAs(builder);
    }

    // ── CreateDefault ──────────────────────────────────────────────

    [Fact]
    public void CreateDefault_ReturnsAllComponents()
    {
        var result = Phase2OrchestratorBuilder.CreateDefault(_llmMock.Object);

        result.Orchestrator.Should().NotBeNull();
        result.CapabilityRegistry.Should().NotBeNull();
        result.GoalHierarchy.Should().NotBeNull();
        result.SelfEvaluator.Should().NotBeNull();
    }
}
