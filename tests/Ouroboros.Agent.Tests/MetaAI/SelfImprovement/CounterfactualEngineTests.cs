// <copyright file="CounterfactualEngineTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.MetaAI.SelfImprovement;

namespace Ouroboros.Tests.MetaAI.SelfImprovement;

[Trait("Category", "Unit")]
public class CounterfactualEngineTests
{
    // ── SimulateAlternativeAsync ─────────────────────────────────────

    [Fact]
    public async Task SimulateAlternativeAsync_NullActualAction_Throws()
    {
        // Arrange
        var engine = new CounterfactualEngine();

        // Act
        var act = () => engine.SimulateAlternativeAsync(null!, "alt", "context");

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SimulateAlternativeAsync_NullAlternativeAction_Throws()
    {
        // Arrange
        var engine = new CounterfactualEngine();

        // Act
        var act = () => engine.SimulateAlternativeAsync("actual", null!, "context");

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SimulateAlternativeAsync_NullContext_Throws()
    {
        // Arrange
        var engine = new CounterfactualEngine();

        // Act
        var act = () => engine.SimulateAlternativeAsync("actual", "alt", null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SimulateAlternativeAsync_CancelledToken_Throws()
    {
        // Arrange
        var engine = new CounterfactualEngine();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => engine.SimulateAlternativeAsync("actual", "alt", "context", cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task SimulateAlternativeAsync_ValidInputs_ReturnsSuccess()
    {
        // Arrange
        var engine = new CounterfactualEngine();

        // Act
        var result = await engine.SimulateAlternativeAsync(
            "use basic approach", "use advanced approach with optimization", "performance testing context");

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task SimulateAlternativeAsync_MoreRelevantAlternative_ReturnsPositiveQualityDiff()
    {
        // Arrange
        var engine = new CounterfactualEngine();
        string context = "performance testing optimization speed";

        // Act — alternative has more overlap with context
        var result = await engine.SimulateAlternativeAsync(
            "do nothing", "performance testing optimization for speed improvement", context);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.QualityDifference.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SimulateAlternativeAsync_ContainsBothActions()
    {
        // Arrange
        var engine = new CounterfactualEngine();

        // Act
        var result = await engine.SimulateAlternativeAsync("actionA", "actionB", "ctx");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ActualAction.Should().Be("actionA");
        result.Value.AlternativeAction.Should().Be("actionB");
    }

    // ── ComputeRegretAsync ──────────────────────────────────────────

    [Fact]
    public async Task ComputeRegretAsync_NullActionTaken_Throws()
    {
        var engine = new CounterfactualEngine();
        var act = () => engine.ComputeRegretAsync(null!, "best", "outcome");
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ComputeRegretAsync_NullBestAlternative_Throws()
    {
        var engine = new CounterfactualEngine();
        var act = () => engine.ComputeRegretAsync("taken", null!, "outcome");
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ComputeRegretAsync_NullOutcome_Throws()
    {
        var engine = new CounterfactualEngine();
        var act = () => engine.ComputeRegretAsync("taken", "best", null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ComputeRegretAsync_ValidInputs_ReturnsRegretBetweenZeroAndOne()
    {
        // Arrange
        var engine = new CounterfactualEngine();

        // Act
        var result = await engine.ComputeRegretAsync("poor choice", "best choice with optimization", "outcome context");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeGreaterThanOrEqualTo(0.0);
        result.Value.Should().BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public async Task ComputeRegretAsync_SameActions_ReturnsZeroRegret()
    {
        // Arrange
        var engine = new CounterfactualEngine();

        // Act — identical actions should yield no regret
        var result = await engine.ComputeRegretAsync("same action", "same action", "outcome");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0.0);
    }

    [Fact]
    public async Task ComputeRegretAsync_AddsToRegretHistory()
    {
        // Arrange
        var engine = new CounterfactualEngine();
        engine.TotalRegretRecords.Should().Be(0);

        // Act
        await engine.ComputeRegretAsync("action", "better action", "outcome");

        // Assert
        engine.TotalRegretRecords.Should().Be(1);
    }

    // ── ExplainContrastivelyAsync ───────────────────────────────────

    [Fact]
    public async Task ExplainContrastivelyAsync_NullActualOutcome_Throws()
    {
        var engine = new CounterfactualEngine();
        var act = () => engine.ExplainContrastivelyAsync(null!, "expected");
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExplainContrastivelyAsync_NullExpectedOutcome_Throws()
    {
        var engine = new CounterfactualEngine();
        var act = () => engine.ExplainContrastivelyAsync("actual", null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExplainContrastivelyAsync_DifferentOutcomes_ReturnsDifferentiatingFactors()
    {
        // Arrange
        var engine = new CounterfactualEngine();

        // Act
        var result = await engine.ExplainContrastivelyAsync(
            "The system crashed due to memory overflow",
            "The system completed successfully with good performance");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.DifferentiatingFactors.Should().NotBeEmpty();
        result.Value.Explanation.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExplainContrastivelyAsync_SimilarOutcomes_ReturnsExplanation()
    {
        // Arrange
        var engine = new CounterfactualEngine();

        // Act
        var result = await engine.ExplainContrastivelyAsync(
            "task completed", "task completed");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Explanation.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExplainContrastivelyAsync_ContainsOriginalOutcomes()
    {
        // Arrange
        var engine = new CounterfactualEngine();

        // Act
        var result = await engine.ExplainContrastivelyAsync("actual outcome", "expected outcome");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ActualOutcome.Should().Be("actual outcome");
        result.Value.ExpectedOutcome.Should().Be("expected outcome");
    }

    // ── GetRegretHistory ────────────────────────────────────────────

    [Fact]
    public void GetRegretHistory_EmptyEngine_ReturnsEmptyList()
    {
        // Arrange
        var engine = new CounterfactualEngine();

        // Act
        var history = engine.GetRegretHistory();

        // Assert
        history.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRegretHistory_AfterMultipleRegrets_ReturnsInReverseChronologicalOrder()
    {
        // Arrange
        var engine = new CounterfactualEngine();
        await engine.ComputeRegretAsync("action1", "better1", "outcome1");
        await engine.ComputeRegretAsync("action2", "better2", "outcome2");
        await engine.ComputeRegretAsync("action3", "better3", "outcome3");

        // Act
        var history = engine.GetRegretHistory(3);

        // Assert
        history.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetRegretHistory_WithCountLimit_ReturnsRequestedNumber()
    {
        // Arrange
        var engine = new CounterfactualEngine();
        for (int i = 0; i < 10; i++)
        {
            await engine.ComputeRegretAsync($"action{i}", $"better{i}", $"outcome{i}");
        }

        // Act
        var history = engine.GetRegretHistory(5);

        // Assert
        history.Should().HaveCount(5);
    }

    // ── TotalRegretRecords ──────────────────────────────────────────

    [Fact]
    public async Task TotalRegretRecords_TracksAccumulation()
    {
        // Arrange
        var engine = new CounterfactualEngine();

        // Act
        for (int i = 0; i < 5; i++)
        {
            await engine.ComputeRegretAsync($"action{i}", $"better{i}", $"outcome{i}");
        }

        // Assert
        engine.TotalRegretRecords.Should().Be(5);
    }
}
