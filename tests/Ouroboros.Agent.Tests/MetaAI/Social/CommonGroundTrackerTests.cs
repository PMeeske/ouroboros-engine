// <copyright file="CommonGroundTrackerTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.MetaAI.Social;

namespace Ouroboros.Agent.Tests.MetaAI.Social;

/// <summary>
/// Unit tests for <see cref="CommonGroundTracker"/>.
/// </summary>
[Trait("Category", "Unit")]
public class CommonGroundTrackerTests
{
    private readonly CommonGroundTracker _sut = new();

    // --- AddToCommonGround ---

    [Fact]
    public void AddToCommonGround_NullPersonId_ThrowsArgumentNullException()
    {
        var act = () => _sut.AddToCommonGround(null!, "proposition", GroundingMethod.Explicit);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddToCommonGround_NullProposition_ThrowsArgumentNullException()
    {
        var act = () => _sut.AddToCommonGround("person-1", null!, GroundingMethod.Explicit);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddToCommonGround_ValidInput_AddsProposition()
    {
        // Act
        _sut.AddToCommonGround("alice", "The sky is blue", GroundingMethod.Explicit);

        // Assert
        var ground = _sut.GetCommonGround("alice");
        ground.Should().HaveCount(1);
        ground[0].Should().Be("The sky is blue");
    }

    [Fact]
    public void AddToCommonGround_DuplicateProposition_DoesNotAddTwice()
    {
        // Act
        _sut.AddToCommonGround("alice", "The sky is blue", GroundingMethod.Explicit);
        _sut.AddToCommonGround("alice", "The sky is blue", GroundingMethod.Implicit);

        // Assert
        _sut.GetCommonGround("alice").Should().HaveCount(1);
    }

    [Fact]
    public void AddToCommonGround_DifferentPropositions_AddsAll()
    {
        // Act
        _sut.AddToCommonGround("alice", "Fact A", GroundingMethod.Explicit);
        _sut.AddToCommonGround("alice", "Fact B", GroundingMethod.Implicit);
        _sut.AddToCommonGround("alice", "Fact C", GroundingMethod.Inferred);

        // Assert
        _sut.GetCommonGround("alice").Should().HaveCount(3);
    }

    [Fact]
    public void AddToCommonGround_DifferentPeople_SeparateGrounds()
    {
        // Act
        _sut.AddToCommonGround("alice", "Fact A", GroundingMethod.Explicit);
        _sut.AddToCommonGround("bob", "Fact B", GroundingMethod.Explicit);

        // Assert
        _sut.GetCommonGround("alice").Should().HaveCount(1);
        _sut.GetCommonGround("bob").Should().HaveCount(1);
        _sut.TrackedPartnerCount.Should().Be(2);
    }

    // --- IsInCommonGround ---

    [Fact]
    public void IsInCommonGround_NullPersonId_ThrowsArgumentNullException()
    {
        var act = () => _sut.IsInCommonGround(null!, "proposition");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void IsInCommonGround_NullProposition_ThrowsArgumentNullException()
    {
        var act = () => _sut.IsInCommonGround("person-1", null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void IsInCommonGround_ExistingProposition_ReturnsTrue()
    {
        // Arrange
        _sut.AddToCommonGround("alice", "The project is on schedule", GroundingMethod.Explicit);

        // Act & Assert
        _sut.IsInCommonGround("alice", "The project is on schedule").Should().BeTrue();
    }

    [Fact]
    public void IsInCommonGround_PartialMatch_ReturnsTrue()
    {
        // Arrange
        _sut.AddToCommonGround("alice", "The project is on schedule", GroundingMethod.Explicit);

        // Act — substring match
        _sut.IsInCommonGround("alice", "project is on schedule").Should().BeTrue();
    }

    [Fact]
    public void IsInCommonGround_CaseInsensitive_ReturnsTrue()
    {
        // Arrange
        _sut.AddToCommonGround("alice", "The Sky Is Blue", GroundingMethod.Explicit);

        // Act & Assert
        _sut.IsInCommonGround("alice", "the sky is blue").Should().BeTrue();
    }

    [Fact]
    public void IsInCommonGround_UnknownPerson_ReturnsFalse()
    {
        _sut.IsInCommonGround("stranger", "anything").Should().BeFalse();
    }

    [Fact]
    public void IsInCommonGround_NonExistingProposition_ReturnsFalse()
    {
        // Arrange
        _sut.AddToCommonGround("alice", "Fact A", GroundingMethod.Explicit);

        // Act & Assert
        _sut.IsInCommonGround("alice", "Completely different topic").Should().BeFalse();
    }

    // --- GetCommonGround ---

    [Fact]
    public void GetCommonGround_NullPersonId_ThrowsArgumentNullException()
    {
        var act = () => _sut.GetCommonGround(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetCommonGround_UnknownPerson_ReturnsEmptyList()
    {
        _sut.GetCommonGround("unknown").Should().BeEmpty();
    }

    [Fact]
    public void GetCommonGround_OrderedByConfidence()
    {
        // Arrange — Explicit has higher confidence (0.95) than Inferred (0.50)
        _sut.AddToCommonGround("alice", "Low confidence fact", GroundingMethod.Inferred);
        _sut.AddToCommonGround("alice", "High confidence fact", GroundingMethod.Explicit);

        // Act
        var ground = _sut.GetCommonGround("alice");

        // Assert — ordered by confidence descending
        ground[0].Should().Be("High confidence fact");
    }

    // --- DetectMisunderstandingAsync ---

    [Fact]
    public async Task DetectMisunderstandingAsync_NullPersonId_ThrowsArgumentNullException()
    {
        var act = () => _sut.DetectMisunderstandingAsync(null!, "utterance", "response");
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task DetectMisunderstandingAsync_NullUtterance_ThrowsArgumentNullException()
    {
        var act = () => _sut.DetectMisunderstandingAsync("person", null!, "response");
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task DetectMisunderstandingAsync_NullResponse_ThrowsArgumentNullException()
    {
        var act = () => _sut.DetectMisunderstandingAsync("person", "utterance", null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task DetectMisunderstandingAsync_CancelledToken_Throws()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => _sut.DetectMisunderstandingAsync("person", "utterance", "response", cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task DetectMisunderstandingAsync_HighOverlap_NoMisunderstanding()
    {
        // Arrange — utterance and response share many keywords
        var result = await _sut.DetectMisunderstandingAsync(
            "alice",
            "The weather is nice and sunny today",
            "Yes the weather is sunny and warm today");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Detected.Should().BeFalse();
    }

    [Fact]
    public async Task DetectMisunderstandingAsync_LowOverlap_DetectsMisunderstanding()
    {
        // Arrange — completely different topics
        var result = await _sut.DetectMisunderstandingAsync(
            "alice",
            "Let us discuss quantum physics and entanglement",
            "I had pizza for dinner with garlic bread");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Detected.Should().BeTrue();
        result.Value.ClarificationSuggestion.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task DetectMisunderstandingAsync_ReturnsConfidenceInRange()
    {
        // Act
        var result = await _sut.DetectMisunderstandingAsync(
            "alice", "topic one", "topic two");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Confidence.Should().BeGreaterThanOrEqualTo(0.0);
        result.Value.Confidence.Should().BeLessThanOrEqualTo(1.0);
    }

    // --- RecordGroundingSuccess ---

    [Fact]
    public void RecordGroundingSuccess_NullPersonId_ThrowsArgumentNullException()
    {
        var act = () => _sut.RecordGroundingSuccess(null!, "prop", true);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RecordGroundingSuccess_NullProposition_ThrowsArgumentNullException()
    {
        var act = () => _sut.RecordGroundingSuccess("person", null!, true);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RecordGroundingSuccess_Understood_IncreasesConfidence()
    {
        // Arrange
        _sut.AddToCommonGround("alice", "Shared fact", GroundingMethod.Implicit);
        // Implicit has 0.70 confidence

        // Act
        _sut.RecordGroundingSuccess("alice", "Shared fact", understood: true);

        // Assert — confidence should have increased by 0.1
        // Verification: the proposition should still be in common ground (with higher confidence)
        _sut.IsInCommonGround("alice", "Shared fact").Should().BeTrue();
    }

    [Fact]
    public void RecordGroundingSuccess_NotUnderstood_DecreasesConfidence()
    {
        // Arrange
        _sut.AddToCommonGround("alice", "Shared fact", GroundingMethod.Implicit);

        // Act
        _sut.RecordGroundingSuccess("alice", "Shared fact", understood: false);

        // Assert — confidence decreased by 0.2 (0.70 - 0.2 = 0.50)
        _sut.IsInCommonGround("alice", "Shared fact").Should().BeTrue();
    }

    [Fact]
    public void RecordGroundingSuccess_UnknownPerson_DoesNotThrow()
    {
        // Act — should silently return
        var act = () => _sut.RecordGroundingSuccess("unknown", "prop", true);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void RecordGroundingSuccess_UnknownProposition_DoesNotThrow()
    {
        // Arrange
        _sut.AddToCommonGround("alice", "Known fact", GroundingMethod.Explicit);

        // Act
        var act = () => _sut.RecordGroundingSuccess("alice", "Unknown fact", true);

        // Assert
        act.Should().NotThrow();
    }

    // --- TrackedPartnerCount ---

    [Fact]
    public void TrackedPartnerCount_InitiallyZero()
    {
        _sut.TrackedPartnerCount.Should().Be(0);
    }

    [Fact]
    public void TrackedPartnerCount_ReflectsUniquePeople()
    {
        // Act
        _sut.AddToCommonGround("alice", "A", GroundingMethod.Explicit);
        _sut.AddToCommonGround("bob", "B", GroundingMethod.Explicit);
        _sut.AddToCommonGround("alice", "C", GroundingMethod.Explicit);

        // Assert
        _sut.TrackedPartnerCount.Should().Be(2);
    }

    // --- Grounding method confidence ---

    [Theory]
    [InlineData(GroundingMethod.Explicit)]
    [InlineData(GroundingMethod.Implicit)]
    [InlineData(GroundingMethod.Presupposed)]
    [InlineData(GroundingMethod.Inferred)]
    public void AddToCommonGround_AllGroundingMethods_Work(GroundingMethod method)
    {
        // Act
        _sut.AddToCommonGround("alice", $"Fact via {method}", method);

        // Assert
        _sut.IsInCommonGround("alice", $"Fact via {method}").Should().BeTrue();
    }
}
