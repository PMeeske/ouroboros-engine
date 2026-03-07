// <copyright file="IntentionPredictionTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.TheoryOfMind;

namespace Ouroboros.Tests.TheoryOfMind;

/// <summary>
/// Unit tests for the <see cref="IntentionPrediction"/> record.
/// Covers constructors, factory methods, confidence clamping, and record equality.
/// </summary>
[Trait("Category", "Unit")]
public class IntentionPredictionTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var evidence = new List<string> { "Observed approaching target" };
        var alternatives = new List<string> { "Exploring", "Idle" };

        // Act
        var prediction = new IntentionPrediction(
            "agent-1",
            "Reach the goal",
            0.8,
            evidence,
            alternatives);

        // Assert
        prediction.AgentId.Should().Be("agent-1");
        prediction.PredictedGoal.Should().Be("Reach the goal");
        prediction.Confidence.Should().Be(0.8);
        prediction.SupportingEvidence.Should().ContainSingle("Observed approaching target");
        prediction.AlternativeGoals.Should().HaveCount(2);
    }

    [Fact]
    public void Unknown_ReturnsLowConfidencePrediction()
    {
        // Act
        var prediction = IntentionPrediction.Unknown("agent-1");

        // Assert
        prediction.AgentId.Should().Be("agent-1");
        prediction.PredictedGoal.Should().Contain("Unknown");
        prediction.Confidence.Should().Be(0.0);
        prediction.SupportingEvidence.Should().ContainSingle().Which.Should().Contain("Insufficient");
        prediction.AlternativeGoals.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithValidConfidence_PreservesValue()
    {
        // Act
        var prediction = IntentionPrediction.Create("agent-1", "Explore", 0.7);

        // Assert
        prediction.Confidence.Should().Be(0.7);
        prediction.PredictedGoal.Should().Be("Explore");
    }

    [Fact]
    public void Create_WithConfidenceAboveOne_ClampsToOne()
    {
        // Act
        var prediction = IntentionPrediction.Create("agent-1", "goal", 1.5);

        // Assert
        prediction.Confidence.Should().Be(1.0);
    }

    [Fact]
    public void Create_WithNegativeConfidence_ClampsToZero()
    {
        // Act
        var prediction = IntentionPrediction.Create("agent-1", "goal", -0.5);

        // Assert
        prediction.Confidence.Should().Be(0.0);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Create_WithBoundaryConfidence_PreservesValue(double confidence)
    {
        // Act
        var prediction = IntentionPrediction.Create("agent-1", "goal", confidence);

        // Assert
        prediction.Confidence.Should().Be(confidence);
    }

    [Fact]
    public void Create_NullEvidence_DefaultsToEmptyList()
    {
        // Act
        var prediction = IntentionPrediction.Create("agent-1", "goal", 0.5, evidence: null);

        // Assert
        prediction.SupportingEvidence.Should().NotBeNull();
        prediction.SupportingEvidence.Should().BeEmpty();
    }

    [Fact]
    public void Create_NullAlternatives_DefaultsToEmptyList()
    {
        // Act
        var prediction = IntentionPrediction.Create("agent-1", "goal", 0.5, alternatives: null);

        // Assert
        prediction.AlternativeGoals.Should().NotBeNull();
        prediction.AlternativeGoals.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithEvidenceAndAlternatives_PreservesThem()
    {
        // Arrange
        var evidence = new List<string> { "ev1", "ev2" };
        var alternatives = new List<string> { "alt1" };

        // Act
        var prediction = IntentionPrediction.Create("agent-1", "goal", 0.6, evidence, alternatives);

        // Assert
        prediction.SupportingEvidence.Should().HaveCount(2);
        prediction.AlternativeGoals.Should().ContainSingle("alt1");
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        // Arrange
        var evidence = new List<string> { "ev" };
        var alternatives = new List<string>();
        var a = new IntentionPrediction("agent-1", "goal", 0.5, evidence, alternatives);
        var b = new IntentionPrediction("agent-1", "goal", 0.5, evidence, alternatives);

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void RecordEquality_DifferentGoal_AreNotEqual()
    {
        // Arrange
        var evidence = new List<string>();
        var alternatives = new List<string>();
        var a = new IntentionPrediction("agent-1", "goal-A", 0.5, evidence, alternatives);
        var b = new IntentionPrediction("agent-1", "goal-B", 0.5, evidence, alternatives);

        // Assert
        a.Should().NotBe(b);
    }

    [Fact]
    public void Constructor_DoesNotClamp_ConfidenceDirectly()
    {
        // Arrange — the raw constructor does NOT clamp; only Create does
        var prediction = new IntentionPrediction(
            "agent-1", "goal", 2.5, new List<string>(), new List<string>());

        // Assert
        prediction.Confidence.Should().Be(2.5);
    }
}
