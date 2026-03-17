// <copyright file="StateTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.MetaAI.WorldModel;

namespace Ouroboros.Agent.Tests.MetaAI.WorldModel;

/// <summary>
/// Unit tests for the State record.
/// </summary>
[Trait("Category", "Unit")]
public class StateTests
{
    [Fact]
    public void Constructor_WithValidParameters_SetsProperties()
    {
        // Arrange
        var features = new Dictionary<string, object> { ["temperature"] = 72.0, ["location"] = "office" };
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };

        // Act
        var sut = new State(features, embedding);

        // Assert
        sut.Features.Should().HaveCount(2);
        sut.Features["temperature"].Should().Be(72.0);
        sut.Embedding.Should().HaveCount(3);
    }

    [Fact]
    public void Equality_TwoIdenticalStates_AreEqual()
    {
        // Arrange
        var features = new Dictionary<string, object> { ["key"] = "value" };
        var embedding = new float[] { 1.0f, 2.0f };

        var a = new State(features, embedding);
        var b = new State(features, embedding);

        // Act & Assert
        a.Should().Be(b);
    }

    [Fact]
    public void Constructor_WithEmptyFeatures_Succeeds()
    {
        // Arrange & Act
        var sut = new State(new Dictionary<string, object>(), Array.Empty<float>());

        // Assert
        sut.Features.Should().BeEmpty();
        sut.Embedding.Should().BeEmpty();
    }

    [Fact]
    public void With_ModifiedEmbedding_CreatesNewRecord()
    {
        // Arrange
        var original = new State(
            new Dictionary<string, object>(),
            new float[] { 1.0f, 2.0f });

        // Act
        var modified = original with { Embedding = new float[] { 3.0f, 4.0f } };

        // Assert
        modified.Embedding[0].Should().Be(3.0f);
        original.Embedding[0].Should().Be(1.0f);
    }
}
