// <copyright file="NanoAtomArrowsTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Abstractions.Core;
using Ouroboros.Agent.NanoAtoms;

namespace Ouroboros.Agent.Tests.NanoAtoms;

/// <summary>
/// Unit tests for <see cref="NanoAtomArrows"/>.
/// Tests focus on the digest and consolidation arrows which accept
/// simple record inputs, avoiding PipelineBranch construction complexity.
/// </summary>
[Trait("Category", "Unit")]
public class NanoAtomArrowsTests
{
    private readonly Mock<IChatCompletionModel> _modelMock = new();
    private readonly NanoAtomConfig _config = new(UseGoalDecomposer: false);

    // --- NanoDigestArrow ---

    [Fact]
    public async Task NanoDigestArrow_WhenModelFails_ReturnsLowConfidenceDigest()
    {
        // Arrange
        _modelMock
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("LLM error"));

        var fragment = ThoughtFragment.FromText("Test thought content");
        var arrow = NanoAtomArrows.NanoDigestArrow(_modelMock.Object, _config);

        // Act
        var result = await arrow(fragment);

        // Assert — fallback digest with low confidence
        result.Should().NotBeNull();
        result.Content.Should().Be(fragment.Content);
        result.Confidence.Should().Be(0.1);
        result.CompletedPhase.Should().Be(NanoAtomPhase.Process);
    }

    [Fact]
    public async Task NanoDigestArrow_WhenModelSucceeds_ReturnsDigestFragment()
    {
        // Arrange
        _modelMock
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Digested output");

        var fragment = ThoughtFragment.FromText("Test thought content");
        var arrow = NanoAtomArrows.NanoDigestArrow(_modelMock.Object, _config);

        // Act
        var result = await arrow(fragment);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task NanoDigestArrow_ReturnsDigestWithSourceAtomId()
    {
        // Arrange
        _modelMock
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("error"));

        var fragment = ThoughtFragment.FromText("Some content");
        var arrow = NanoAtomArrows.NanoDigestArrow(_modelMock.Object, _config);

        // Act
        var result = await arrow(fragment);

        // Assert — even fallback digest should have a valid source atom ID
        result.SourceAtomId.Should().NotBe(Guid.Empty);
        result.Id.Should().NotBe(Guid.Empty);
    }

    // --- NanoConsolidateArrow ---

    [Fact]
    public async Task NanoConsolidateArrow_WithDigests_ReturnsConsolidatedAction()
    {
        // Arrange
        var digests = new List<DigestFragment>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "Digest 1", 2.0, 0.8, NanoAtomPhase.Emit, DateTime.UtcNow),
            new(Guid.NewGuid(), Guid.NewGuid(), "Digest 2", 1.5, 0.9, NanoAtomPhase.Emit, DateTime.UtcNow)
        };
        var arrow = NanoAtomArrows.NanoConsolidateArrow(_modelMock.Object, _config);

        // Act
        var result = await arrow(digests);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().NotBeNullOrEmpty();
        result.StreamCount.Should().Be(1);
    }

    [Fact]
    public async Task NanoConsolidateArrow_WithSingleDigest_ReturnsAction()
    {
        // Arrange
        var digests = new List<DigestFragment>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "Single digest", 1.0, 0.9, NanoAtomPhase.Emit, DateTime.UtcNow)
        };
        var arrow = NanoAtomArrows.NanoConsolidateArrow(_modelMock.Object, _config);

        // Act
        var result = await arrow(digests);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task NanoConsolidateArrow_ActionHasCorrectActionType()
    {
        // Arrange
        var digests = new List<DigestFragment>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "Content", 2.0, 0.8, NanoAtomPhase.Emit, DateTime.UtcNow)
        };
        var arrow = NanoAtomArrows.NanoConsolidateArrow(_modelMock.Object, _config);

        // Act
        var result = await arrow(digests);

        // Assert — action type should be set
        result.ActionType.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task NanoConsolidateArrow_WithEmptyDigests_ReturnsResult()
    {
        // Arrange — empty digests to test edge case
        var digests = new List<DigestFragment>();
        var arrow = NanoAtomArrows.NanoConsolidateArrow(_modelMock.Object, _config);

        // Act
        var result = await arrow(digests);

        // Assert — should return either success or a minimal fallback action
        result.Should().NotBeNull();
    }

    // --- NanoReasoningArrow / SafeNanoReasoningArrow ---
    // Note: Full PipelineBranch-based tests require foundation layer types.
    // These are tested indirectly through integration tests.
    // The digest and consolidation arrows above verify the core arrow pattern.
}
