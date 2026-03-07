// <copyright file="ThoughtConsolidatorTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Agent.NanoAtoms;

namespace Ouroboros.Agent.Tests.NanoAtoms;

/// <summary>
/// Tests for ThoughtConsolidator — merging streams into ConsolidatedActions.
/// </summary>
[Trait("Category", "Unit")]
public class ThoughtConsolidatorTests
{
    private static DigestFragment CreateDigest(string content, double confidence = 0.8)
    {
        return new DigestFragment(
            Guid.NewGuid(), Guid.NewGuid(), content,
            2.0, confidence, NanoAtomPhase.Emit, DateTime.UtcNow);
    }

    [Fact]
    public async Task ConsolidateAsync_WithDigests_ReturnsConsolidatedAction()
    {
        // Arrange
        var config = NanoAtomConfig.Default();
        var consolidator = new ThoughtConsolidator(config);
        consolidator.AddDigest(CreateDigest("First thought digest"));
        consolidator.AddDigest(CreateDigest("Second thought digest"));

        // Act
        var result = await consolidator.ConsolidateAsync(streamCount: 2);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Content.Should().NotBeNullOrEmpty();
        result.Value.SourceDigests.Should().HaveCount(2);
        result.Value.StreamCount.Should().Be(2);
        result.Value.Confidence.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ConsolidateAsync_NoDigests_ReturnsFailure()
    {
        // Arrange
        var config = NanoAtomConfig.Default();
        var consolidator = new ThoughtConsolidator(config);

        // Act
        var result = await consolidator.ConsolidateAsync(streamCount: 0);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("No digests");
    }

    [Fact]
    public async Task ConsolidateAsync_WithSynthesisModel_UsesLLM()
    {
        // Arrange
        var mockModel = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        mockModel
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Synthesized response combining all thoughts");

        var config = NanoAtomConfig.Default();
        var consolidator = new ThoughtConsolidator(config, mockModel.Object);
        consolidator.AddDigest(CreateDigest("Digest A"));
        consolidator.AddDigest(CreateDigest("Digest B"));

        // Act
        var result = await consolidator.ConsolidateAsync(streamCount: 2);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Content.Should().Be("Synthesized response combining all thoughts");
    }

    [Fact]
    public async Task ConsolidateAsync_RuleBasedFallback_ConcatenatesDigests()
    {
        // Arrange: No synthesis model → rule-based merge
        var config = NanoAtomConfig.Default();
        var consolidator = new ThoughtConsolidator(config, synthesisModel: null);
        consolidator.AddDigest(CreateDigest("Part one of the response"));
        consolidator.AddDigest(CreateDigest("Part two of the response"));

        // Act
        var result = await consolidator.ConsolidateAsync(streamCount: 2);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Content.Should().Contain("Part one");
        result.Value.Content.Should().Contain("Part two");
        result.Value.ActionType.Should().Be("response");
    }
}
