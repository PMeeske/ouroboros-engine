// <copyright file="NanoOuroborosAtomTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Agent.NanoAtoms;
using Ouroboros.Providers;

namespace Ouroboros.Agent.Tests.NanoAtoms;

/// <summary>
/// Tests for NanoOuroborosAtom — the self-consuming atomic processor.
/// </summary>
[Trait("Category", "Unit")]
public class NanoOuroborosAtomTests
{
    private static ThoughtFragment CreateTestFragment(string content = "Test thought content for processing")
    {
        return new ThoughtFragment(
            Id: Guid.NewGuid(),
            Content: content,
            Source: "test",
            EstimatedTokens: ThoughtFragment.EstimateTokenCount(content),
            GoalType: SubGoalType.Reasoning,
            Complexity: SubGoalComplexity.Simple,
            PreferredTier: PathwayTier.Local,
            Timestamp: DateTime.UtcNow,
            Tags: ["test"]);
    }

    [Fact]
    public async Task ProcessAsync_WithValidFragment_ReturnsDigestFragment()
    {
        // Arrange
        var mockModel = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        mockModel
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Processed output from the nano model");

        var config = NanoAtomConfig.Default();
        using var atom = new NanoOuroborosAtom(mockModel.Object, config);
        var fragment = CreateTestFragment();

        // Act
        var result = await atom.ProcessAsync(fragment);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Content.Should().NotBeNullOrEmpty();
        result.Value.SourceAtomId.Should().Be(atom.AtomId);
        result.Value.CompressionRatio.Should().BeGreaterThan(0);
        result.Value.Confidence.Should().BeInRange(0.0, 1.0);
        result.Value.CompletedPhase.Should().Be(NanoAtomPhase.Emit);
    }

    [Fact]
    public async Task ProcessAsync_CallsModelTwice_ForProcessAndDigest()
    {
        // Arrange: The ouroboros bite means the model is called twice:
        // once for processing and once for self-consuming (digest)
        var mockModel = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        mockModel
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Model response");

        var config = NanoAtomConfig.Default();
        using var atom = new NanoOuroborosAtom(mockModel.Object, config);
        var fragment = CreateTestFragment();

        // Act
        await atom.ProcessAsync(fragment);

        // Assert: Two calls — Process + Digest (the ouroboros bite)
        mockModel.Verify(
            m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ProcessAsync_WhenModelFails_FallsBackToSymbolic()
    {
        // Arrange: Model always throws → circuit breaker should activate
        var mockModel = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        mockModel
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Model unavailable"));

        var config = new NanoAtomConfig(EnableCircuitBreaker: true, CircuitBreakerFailureThreshold: 1);
        using var atom = new NanoOuroborosAtom(mockModel.Object, config);
        var fragment = CreateTestFragment("This is a test sentence. Another sentence here.");

        // Act
        var result = await atom.ProcessAsync(fragment);

        // Assert: Should still succeed with symbolic fallback
        result.IsSuccess.Should().BeTrue();
        result.Value.Content.Should().NotBeNullOrEmpty();
        result.Value.Confidence.Should().BeLessThan(0.6); // Lower confidence for symbolic
    }

    [Fact]
    public async Task ProcessAsync_CircuitBreakerOpens_AfterThresholdFailures()
    {
        // Arrange
        var mockModel = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        mockModel
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Fail"));

        var config = new NanoAtomConfig(EnableCircuitBreaker: true, CircuitBreakerFailureThreshold: 2);
        using var atom = new NanoOuroborosAtom(mockModel.Object, config);

        // Act: Process twice to trip the circuit breaker
        await atom.ProcessAsync(CreateTestFragment("First failure test sentence."));
        atom.IsCircuitOpen.Should().BeFalse(); // Process call fails, Digest call trips it

        await atom.ProcessAsync(CreateTestFragment("Second failure test."));

        // Assert
        atom.IsCircuitOpen.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessAsync_TracksPhaseCorrectly_ReturnsToIdle()
    {
        // Arrange
        var mockModel = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        mockModel
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Response");

        var config = NanoAtomConfig.Default();
        using var atom = new NanoOuroborosAtom(mockModel.Object, config);

        atom.CurrentPhase.Should().Be(NanoAtomPhase.Idle);

        // Act
        await atom.ProcessAsync(CreateTestFragment());

        // Assert: Should return to Idle after processing
        atom.CurrentPhase.Should().Be(NanoAtomPhase.Idle);
        atom.FragmentsProcessed.Should().Be(1);
    }

    [Fact]
    public void TokenEstimation_ReturnsReasonableEstimate()
    {
        // ~4 chars per token
        ThoughtFragment.EstimateTokenCount("Hello world").Should().Be(2); // 11/4 = 2
        ThoughtFragment.EstimateTokenCount("").Should().Be(0);
        ThoughtFragment.EstimateTokenCount("A").Should().Be(1); // Min 1
    }

    [Fact]
    public void FromSubGoal_CreatesFragmentWithRouting()
    {
        // Arrange
        var subGoal = SubGoal.FromDescription("Write a function to calculate fibonacci", 0);

        // Act
        var fragment = ThoughtFragment.FromSubGoal(subGoal);

        // Assert
        fragment.Content.Should().Be("Write a function to calculate fibonacci");
        fragment.Source.Should().Be("goal-decomposer");
        fragment.GoalType.Should().Be(SubGoalType.Coding);
    }

    [Fact]
    public void FromText_CreatesFragmentWithInferredRouting()
    {
        // Act
        var fragment = ThoughtFragment.FromText("Analyze the performance of this algorithm", 0);

        // Assert
        fragment.Content.Should().Be("Analyze the performance of this algorithm");
        fragment.Source.Should().Be("user");
        fragment.GoalType.Should().Be(SubGoalType.Reasoning);
    }
}
