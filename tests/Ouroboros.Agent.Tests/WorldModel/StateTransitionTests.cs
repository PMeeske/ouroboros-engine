// <copyright file="StateTransitionTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.WorldModel;
using Ouroboros.Domain.Embodied;
using Vector3 = Ouroboros.Domain.Embodied.Vector3;

namespace Ouroboros.Tests.WorldModel;

/// <summary>
/// Unit tests for the <see cref="StateTransition"/> record.
/// Covers construction, property initialization, and record equality.
/// </summary>
[Trait("Category", "Unit")]
public class StateTransitionTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var from = CreateSensorState(Vector3.Zero);
        var to = CreateSensorState(Vector3.UnitX);
        var action = EmbodiedAction.Move(Vector3.UnitX, "Forward");
        var timestamp = DateTime.UtcNow;

        // Act
        var transition = new StateTransition(from, action, to, 1.0, false, timestamp);

        // Assert
        transition.FromState.Should().Be(from);
        transition.Action.Should().Be(action);
        transition.ToState.Should().Be(to);
        transition.Reward.Should().Be(1.0);
        transition.Terminal.Should().BeFalse();
        transition.ObservedAt.Should().Be(timestamp);
    }

    [Fact]
    public void Constructor_TerminalTransition_SetsTerminalTrue()
    {
        // Arrange
        var from = CreateSensorState(Vector3.Zero);
        var to = CreateSensorState(Vector3.UnitY);
        var action = EmbodiedAction.NoOp();

        // Act
        var transition = new StateTransition(from, action, to, -1.0, true, DateTime.UtcNow);

        // Assert
        transition.Terminal.Should().BeTrue();
        transition.Reward.Should().Be(-1.0);
    }

    [Fact]
    public void Constructor_NegativeReward_IsAllowed()
    {
        // Arrange
        var from = CreateSensorState(Vector3.Zero);
        var to = CreateSensorState(Vector3.Zero);
        var action = EmbodiedAction.NoOp();

        // Act
        var transition = new StateTransition(from, action, to, -10.0, false, DateTime.UtcNow);

        // Assert
        transition.Reward.Should().Be(-10.0);
    }

    [Fact]
    public void Constructor_ZeroReward_IsAllowed()
    {
        // Arrange
        var from = CreateSensorState(Vector3.Zero);
        var to = CreateSensorState(Vector3.Zero);
        var action = EmbodiedAction.NoOp();

        // Act
        var transition = new StateTransition(from, action, to, 0.0, false, DateTime.UtcNow);

        // Assert
        transition.Reward.Should().Be(0.0);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        // Arrange
        var from = CreateSensorState(Vector3.Zero);
        var to = CreateSensorState(Vector3.UnitX);
        var action = EmbodiedAction.NoOp();
        var timestamp = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var a = new StateTransition(from, action, to, 1.0, false, timestamp);
        var b = new StateTransition(from, action, to, 1.0, false, timestamp);

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void RecordEquality_DifferentReward_AreNotEqual()
    {
        // Arrange
        var from = CreateSensorState(Vector3.Zero);
        var to = CreateSensorState(Vector3.UnitX);
        var action = EmbodiedAction.NoOp();
        var timestamp = DateTime.UtcNow;

        var a = new StateTransition(from, action, to, 1.0, false, timestamp);
        var b = new StateTransition(from, action, to, 2.0, false, timestamp);

        // Assert
        a.Should().NotBe(b);
    }

    [Fact]
    public void RecordEquality_DifferentTerminal_AreNotEqual()
    {
        // Arrange
        var from = CreateSensorState(Vector3.Zero);
        var to = CreateSensorState(Vector3.UnitX);
        var action = EmbodiedAction.NoOp();
        var timestamp = DateTime.UtcNow;

        var a = new StateTransition(from, action, to, 1.0, false, timestamp);
        var b = new StateTransition(from, action, to, 1.0, true, timestamp);

        // Assert
        a.Should().NotBe(b);
    }

    [Fact]
    public void RecordEquality_DifferentAction_AreNotEqual()
    {
        // Arrange
        var from = CreateSensorState(Vector3.Zero);
        var to = CreateSensorState(Vector3.UnitX);
        var action1 = EmbodiedAction.NoOp();
        var action2 = EmbodiedAction.Move(Vector3.UnitX);
        var timestamp = DateTime.UtcNow;

        var a = new StateTransition(from, action1, to, 1.0, false, timestamp);
        var b = new StateTransition(from, action2, to, 1.0, false, timestamp);

        // Assert
        a.Should().NotBe(b);
    }

    [Fact]
    public void Constructor_WithRotationAction_PreservesActionDetails()
    {
        // Arrange
        var from = CreateSensorState(Vector3.Zero);
        var to = CreateSensorState(Vector3.Zero);
        var action = EmbodiedAction.Rotate(new Vector3(0, 90, 0), "TurnRight");

        // Act
        var transition = new StateTransition(from, action, to, 0.5, false, DateTime.UtcNow);

        // Assert
        transition.Action.ActionName.Should().Be("TurnRight");
        transition.Action.Rotation.Y.Should().Be(90);
    }

    private static SensorState CreateSensorState(Vector3 position)
    {
        return new SensorState(
            position,
            Quaternion.Identity,
            Vector3.Zero,
            Array.Empty<float>(),
            Array.Empty<float>(),
            new Dictionary<string, float>(),
            DateTime.UtcNow);
    }
}
