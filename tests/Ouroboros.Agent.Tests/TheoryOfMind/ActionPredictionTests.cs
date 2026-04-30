// <copyright file="ActionPredictionTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Agent.TheoryOfMind;
using Ouroboros.Domain.Embodied;
using Vector3 = Ouroboros.Domain.Embodied.Vector3;

namespace Ouroboros.Tests.TheoryOfMind;

/// <summary>
/// Unit tests for the <see cref="ActionPrediction"/> record.
/// Covers constructors, factory methods, confidence clamping, and record equality.
/// </summary>
[Trait("Category", "Unit")]
public class ActionPredictionTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var action = EmbodiedAction.Move(Vector3.UnitX, "Forward");

        // Act
        var prediction = new ActionPrediction("agent-1", action, 0.75, "Likely to move forward");

        // Assert
        prediction.AgentId.Should().Be("agent-1");
        prediction.PredictedAction.Should().Be(action);
        prediction.Confidence.Should().Be(0.75);
        prediction.Reasoning.Should().Be("Likely to move forward");
    }

    [Fact]
    public void NoOp_WithDefaultReason_ReturnsNoOpPrediction()
    {
        // Act
        var prediction = ActionPrediction.NoOp("agent-1");

        // Assert
        prediction.AgentId.Should().Be("agent-1");
        prediction.PredictedAction.ActionName.Should().Be("NoOp");
        prediction.Confidence.Should().Be(0.0);
        prediction.Reasoning.Should().Be("Insufficient data");
    }

    [Fact]
    public void NoOp_WithCustomReason_UsesProvidedReason()
    {
        // Act
        var prediction = ActionPrediction.NoOp("agent-2", "Agent is unresponsive");

        // Assert
        prediction.AgentId.Should().Be("agent-2");
        prediction.Reasoning.Should().Be("Agent is unresponsive");
        prediction.Confidence.Should().Be(0.0);
    }

    [Fact]
    public void Create_WithValidConfidence_PreservesValue()
    {
        // Arrange
        var action = EmbodiedAction.NoOp();

        // Act
        var prediction = ActionPrediction.Create("agent-1", action, 0.85, "High confidence");

        // Assert
        prediction.Confidence.Should().Be(0.85);
    }

    [Fact]
    public void Create_WithConfidenceAboveOne_ClampsToOne()
    {
        // Arrange
        var action = EmbodiedAction.NoOp();

        // Act
        var prediction = ActionPrediction.Create("agent-1", action, 1.5, "Over-confident");

        // Assert
        prediction.Confidence.Should().Be(1.0);
    }

    [Fact]
    public void Create_WithNegativeConfidence_ClampsToZero()
    {
        // Arrange
        var action = EmbodiedAction.NoOp();

        // Act
        var prediction = ActionPrediction.Create("agent-1", action, -0.3, "Negative confidence");

        // Assert
        prediction.Confidence.Should().Be(0.0);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Create_WithBoundaryConfidence_PreservesValue(double confidence)
    {
        // Arrange
        var action = EmbodiedAction.NoOp();

        // Act
        var prediction = ActionPrediction.Create("agent-1", action, confidence, "test");

        // Assert
        prediction.Confidence.Should().Be(confidence);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        // Arrange
        var action = EmbodiedAction.NoOp();
        var a = new ActionPrediction("agent-1", action, 0.5, "reason");
        var b = new ActionPrediction("agent-1", action, 0.5, "reason");

        // Assert
        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void RecordEquality_DifferentAgentId_AreNotEqual()
    {
        // Arrange
        var action = EmbodiedAction.NoOp();
        var a = new ActionPrediction("agent-1", action, 0.5, "reason");
        var b = new ActionPrediction("agent-2", action, 0.5, "reason");

        // Assert
        a.Should().NotBe(b);
    }

    [Fact]
    public void RecordEquality_DifferentConfidence_AreNotEqual()
    {
        // Arrange
        var action = EmbodiedAction.NoOp();
        var a = new ActionPrediction("agent-1", action, 0.5, "reason");
        var b = new ActionPrediction("agent-1", action, 0.9, "reason");

        // Assert
        a.Should().NotBe(b);
    }

    [Fact]
    public void Constructor_DoesNotClamp_ConfidenceDirectly()
    {
        // Arrange — the raw constructor does NOT clamp; only Create does
        var action = EmbodiedAction.NoOp();

        // Act
        var prediction = new ActionPrediction("agent-1", action, 2.0, "raw");

        // Assert
        prediction.Confidence.Should().Be(2.0);
    }

    [Fact]
    public void NoOp_PredictedAction_HasZeroMovement()
    {
        // Act
        var prediction = ActionPrediction.NoOp("agent-1");

        // Assert
        prediction.PredictedAction.Movement.Should().Be(Vector3.Zero);
        prediction.PredictedAction.Rotation.Should().Be(Vector3.Zero);
    }

    [Fact]
    public void Create_PreservesAgentIdAndReasoning()
    {
        // Arrange
        var action = EmbodiedAction.Rotate(new Vector3(0, 90, 0), "TurnRight");

        // Act
        var prediction = ActionPrediction.Create("agent-42", action, 0.6, "Heading toward target");

        // Assert
        prediction.AgentId.Should().Be("agent-42");
        prediction.PredictedAction.ActionName.Should().Be("TurnRight");
        prediction.Reasoning.Should().Be("Heading toward target");
    }
}
