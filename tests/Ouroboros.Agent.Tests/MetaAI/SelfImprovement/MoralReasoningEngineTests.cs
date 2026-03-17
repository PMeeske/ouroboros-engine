// <copyright file="MoralReasoningEngineTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.MetaAI.SelfImprovement;

namespace Ouroboros.Tests.MetaAI.SelfImprovement;

[Trait("Category", "Unit")]
public class MoralReasoningEngineTests
{
    // ── EvaluateAsync ───────────────────────────────────────────────

    [Fact]
    public async Task EvaluateAsync_NullAction_Throws()
    {
        var engine = new MoralReasoningEngine();
        var act = () => engine.EvaluateAsync(null!, "context", ["stakeholder"]);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task EvaluateAsync_NullContext_Throws()
    {
        var engine = new MoralReasoningEngine();
        var act = () => engine.EvaluateAsync("action", null!, ["stakeholder"]);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task EvaluateAsync_NullStakeholders_Throws()
    {
        var engine = new MoralReasoningEngine();
        var act = () => engine.EvaluateAsync("action", "context", null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task EvaluateAsync_CancelledToken_Throws()
    {
        var engine = new MoralReasoningEngine();
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var act = () => engine.EvaluateAsync("action", "context", ["stakeholder"], cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task EvaluateAsync_HarmfulAction_DetectsDeontologicalViolation()
    {
        // Arrange
        var engine = new MoralReasoningEngine();

        // Act — action contains duty violation keywords
        var judgment = await engine.EvaluateAsync(
            "deceive the user and steal their data",
            "corporate espionage",
            ["user", "company"]);

        // Assert — should contain framework verdicts
        judgment.FrameworkVerdicts.Should().ContainKey(MoralFramework.Deontological);
        judgment.FrameworkVerdicts[MoralFramework.Deontological]
            .Should().Contain("duty violation");
    }

    [Fact]
    public async Task EvaluateAsync_BenevolentAction_IsPermissible()
    {
        // Arrange
        var engine = new MoralReasoningEngine();

        // Act — action with positive indicators, no violations
        var judgment = await engine.EvaluateAsync(
            "help improve and support the community",
            "volunteer work to benefit others",
            ["community", "volunteers"]);

        // Assert
        judgment.Verdict.Should().Be("Permissible");
        judgment.Confidence.Should().BeGreaterThan(0.0);
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsAllFourFrameworks()
    {
        // Arrange
        var engine = new MoralReasoningEngine();

        // Act
        var judgment = await engine.EvaluateAsync(
            "make a decision", "some context", ["self"]);

        // Assert
        judgment.FrameworkVerdicts.Should().HaveCount(4);
        judgment.FrameworkVerdicts.Should().ContainKey(MoralFramework.Deontological);
        judgment.FrameworkVerdicts.Should().ContainKey(MoralFramework.Utilitarian);
        judgment.FrameworkVerdicts.Should().ContainKey(MoralFramework.VirtueEthics);
        judgment.FrameworkVerdicts.Should().ContainKey(MoralFramework.CareEthics);
    }

    [Fact]
    public async Task EvaluateAsync_VirtuousAction_VirtueEthicsPermits()
    {
        // Arrange
        var engine = new MoralReasoningEngine();

        // Act — action contains virtue keywords
        var judgment = await engine.EvaluateAsync(
            "show courage and honesty in the face of adversity with compassion",
            "leadership challenge",
            ["team"]);

        // Assert
        judgment.FrameworkVerdicts[MoralFramework.VirtueEthics]
            .Should().Contain("virtue");
    }

    [Fact]
    public async Task EvaluateAsync_RelationshipDamagingAction_CareEthicsDetects()
    {
        // Arrange
        var engine = new MoralReasoningEngine();

        // Act — action contains relationship damage keywords
        var judgment = await engine.EvaluateAsync(
            "abandon and neglect the team, ignore their needs",
            "management decision",
            ["team", "organization"]);

        // Assert
        judgment.FrameworkVerdicts[MoralFramework.CareEthics]
            .Should().Contain("damage");
    }

    [Fact]
    public async Task EvaluateAsync_ConfidenceIsBetweenZeroAndOne()
    {
        // Arrange
        var engine = new MoralReasoningEngine();

        // Act
        var judgment = await engine.EvaluateAsync("some action", "context", ["self"]);

        // Assert
        judgment.Confidence.Should().BeGreaterThanOrEqualTo(0.0).And.BeLessThanOrEqualTo(1.0);
    }

    // ── DeliberateAsync ─────────────────────────────────────────────

    [Fact]
    public async Task DeliberateAsync_NullDilemma_Throws()
    {
        var engine = new MoralReasoningEngine();
        var act = () => engine.DeliberateAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task DeliberateAsync_ValidDilemma_ReturnsDeliberation()
    {
        // Arrange
        var engine = new MoralReasoningEngine();

        // Act
        var deliberation = await engine.DeliberateAsync(
            "Should we sacrifice short-term profits to benefit the environment?");

        // Assert
        deliberation.Dilemma.Description.Should().NotBeNullOrWhiteSpace();
        deliberation.SynthesizedVerdict.Should().NotBeNullOrWhiteSpace();
        deliberation.FrameworkJudgments.Should().NotBeEmpty();
        deliberation.ConsensusLevel.Should().BeGreaterThanOrEqualTo(0.0).And.BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public async Task DeliberateAsync_IncrementsTotalDeliberations()
    {
        // Arrange
        var engine = new MoralReasoningEngine();
        engine.TotalDeliberations.Should().Be(0);

        // Act
        await engine.DeliberateAsync("moral dilemma one");
        await engine.DeliberateAsync("moral dilemma two");

        // Assert
        engine.TotalDeliberations.Should().Be(2);
    }

    // ── GetCurrentDevelopmentLevel ──────────────────────────────────

    [Fact]
    public void GetCurrentDevelopmentLevel_NoDeliberations_ReturnsPreConventional()
    {
        // Arrange
        var engine = new MoralReasoningEngine();

        // Act
        var level = engine.GetCurrentDevelopmentLevel();

        // Assert
        level.Should().Be(MoralDevelopmentLevel.PreConventional);
    }

    [Fact]
    public async Task GetCurrentDevelopmentLevel_AfterDeliberations_ReturnsAppropriateLevel()
    {
        // Arrange
        var engine = new MoralReasoningEngine();

        // Act — perform several deliberations to build up sophistication
        for (int i = 0; i < 5; i++)
        {
            await engine.DeliberateAsync(
                "Should we help others with courage and honesty while supporting the community?");
        }

        // Assert — should be at least Conventional after deliberations that engage multiple frameworks
        var level = engine.GetCurrentDevelopmentLevel();
        level.Should().NotBe(MoralDevelopmentLevel.PreConventional);
    }

    // ── TotalDeliberations ──────────────────────────────────────────

    [Fact]
    public void TotalDeliberations_InitiallyZero()
    {
        var engine = new MoralReasoningEngine();
        engine.TotalDeliberations.Should().Be(0);
    }
}
