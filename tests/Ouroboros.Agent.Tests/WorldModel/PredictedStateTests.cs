// <copyright file="PredictedStateTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.WorldModel;
using Ouroboros.Domain.Embodied;
using Vector3 = Ouroboros.Domain.Embodied.Vector3;

namespace Ouroboros.Tests.WorldModel;

/// <summary>
/// Unit tests for the <see cref="PredictedState"/> record.
/// Covers construction, property initialization, and record equality.
/// </summary>
[Trait("Category", "Unit")]
public class PredictedStateTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var sensorState = CreateSensorState();
        var metadata = new Dictionary<string, object> { ["source"] = "model-v1" };

        // Act
        var predicted = new PredictedState(
            sensorState,
            Reward: 1.5,
            Terminal: false,
            Confidence: 0.9,
            Metadata: metadata);

        // Assert
        predicted.State.Should().Be(sensorState);
        predicted.Reward.Should().Be(1.5);
        predicted.Terminal.Should().BeFalse();
        predicted.Confidence.Should().Be(0.9);
        predicted.Metadata.Should().ContainKey("source");
    }

    [Fact]
    public void Constructor_TerminalTrue_SetsCorrectly()
    {
        // Arrange
        var state = CreateSensorState();

        // Act
        var predicted = new PredictedState(
            state,
            Reward: -1.0,
            Terminal: true,
            Confidence: 0.5,
            Metadata: new Dictionary<string, object>());

        // Assert
        predicted.Terminal.Should().BeTrue();
        predicted.Reward.Should().Be(-1.0);
    }

    [Fact]
    public void Constructor_EmptyMetadata_IsValid()
    {
        // Arrange
        var state = CreateSensorState();
        IReadOnlyDictionary<string, object> emptyMetadata = new Dictionary<string, object>();

        // Act
        var predicted = new PredictedState(state, 0.0, false, 0.5, emptyMetadata);

        // Assert
        predicted.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        // Arrange
        var state = CreateSensorState();
        var metadata = (IReadOnlyDictionary<string, object>)new Dictionary<string, object>();
        var a = new PredictedState(state, 1.0, false, 0.8, metadata);
        var b = new PredictedState(state, 1.0, false, 0.8, metadata);

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void RecordEquality_DifferentReward_AreNotEqual()
    {
        // Arrange
        var state = CreateSensorState();
        var metadata = (IReadOnlyDictionary<string, object>)new Dictionary<string, object>();
        var a = new PredictedState(state, 1.0, false, 0.8, metadata);
        var b = new PredictedState(state, 2.0, false, 0.8, metadata);

        // Assert
        a.Should().NotBe(b);
    }

    [Fact]
    public void RecordEquality_DifferentTerminal_AreNotEqual()
    {
        // Arrange
        var state = CreateSensorState();
        var metadata = (IReadOnlyDictionary<string, object>)new Dictionary<string, object>();
        var a = new PredictedState(state, 1.0, false, 0.8, metadata);
        var b = new PredictedState(state, 1.0, true, 0.8, metadata);

        // Assert
        a.Should().NotBe(b);
    }

    [Fact]
    public void RecordEquality_DifferentConfidence_AreNotEqual()
    {
        // Arrange
        var state = CreateSensorState();
        var metadata = (IReadOnlyDictionary<string, object>)new Dictionary<string, object>();
        var a = new PredictedState(state, 1.0, false, 0.8, metadata);
        var b = new PredictedState(state, 1.0, false, 0.3, metadata);

        // Assert
        a.Should().NotBe(b);
    }

    [Fact]
    public void Constructor_NegativeReward_IsAllowed()
    {
        // Arrange
        var state = CreateSensorState();

        // Act
        var predicted = new PredictedState(
            state, -5.0, false, 0.5, new Dictionary<string, object>());

        // Assert
        predicted.Reward.Should().Be(-5.0);
    }

    [Fact]
    public void Constructor_ZeroConfidence_IsAllowed()
    {
        // Arrange
        var state = CreateSensorState();

        // Act
        var predicted = new PredictedState(
            state, 0.0, false, 0.0, new Dictionary<string, object>());

        // Assert
        predicted.Confidence.Should().Be(0.0);
    }

    private static SensorState CreateSensorState()
    {
        return new SensorState(
            new Vector3(1, 2, 3),
            Quaternion.Identity,
            new Vector3(0, 0, 0),
            Array.Empty<float>(),
            Array.Empty<float>(),
            new Dictionary<string, float>(),
            DateTime.UtcNow);
    }
}
