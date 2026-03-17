// <copyright file="TransitionTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.MetaAI.WorldModel;
using Action = Ouroboros.Agent.MetaAI.WorldModel.Action;

namespace Ouroboros.Agent.Tests.MetaAI.WorldModel;

/// <summary>
/// Unit tests for the Transition record.
/// </summary>
[Trait("Category", "Unit")]
public class TransitionTests
{
    [Fact]
    public void Constructor_WithValidParameters_SetsAllProperties()
    {
        // Arrange
        var prevState = CreateTestState(0.1f);
        var action = new Action("move", new Dictionary<string, object> { ["direction"] = "north" });
        var nextState = CreateTestState(0.2f);

        // Act
        var sut = new Transition(prevState, action, nextState, 1.5, false);

        // Assert
        sut.PreviousState.Should().Be(prevState);
        sut.ActionTaken.Should().Be(action);
        sut.NextState.Should().Be(nextState);
        sut.Reward.Should().Be(1.5);
        sut.Terminal.Should().BeFalse();
    }

    [Fact]
    public void Constructor_TerminalTransition_SetsTerminalTrue()
    {
        // Arrange
        var prevState = CreateTestState(0.1f);
        var action = new Action("finish", new Dictionary<string, object>());
        var nextState = CreateTestState(0.5f);

        // Act
        var sut = new Transition(prevState, action, nextState, 10.0, true);

        // Assert
        sut.Terminal.Should().BeTrue();
        sut.Reward.Should().Be(10.0);
    }

    [Fact]
    public void Equality_TwoIdenticalTransitions_AreEqual()
    {
        // Arrange
        var state = CreateTestState(0.1f);
        var action = new Action("act", new Dictionary<string, object>());

        var a = new Transition(state, action, state, 1.0, false);
        var b = new Transition(state, action, state, 1.0, false);

        // Act & Assert
        a.Should().Be(b);
    }

    [Fact]
    public void Constructor_NegativeReward_SetsCorrectly()
    {
        // Arrange
        var state = CreateTestState(0.1f);
        var action = new Action("bad-action", new Dictionary<string, object>());

        // Act
        var sut = new Transition(state, action, state, -5.0, false);

        // Assert
        sut.Reward.Should().Be(-5.0);
    }

    private static State CreateTestState(float embeddingValue)
    {
        return new State(
            new Dictionary<string, object>(),
            new float[] { embeddingValue, embeddingValue + 0.1f });
    }
}
