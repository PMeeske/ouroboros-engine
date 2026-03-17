// <copyright file="AttentionPolicyTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.MetaAI.SelfModel;

namespace Ouroboros.Agent.Tests.MetaAI.SelfModel;

/// <summary>
/// Unit tests for the AttentionPolicy record.
/// </summary>
[Trait("Category", "Unit")]
public class AttentionPolicyTests
{
    [Fact]
    public void Constructor_WithValidParameters_SetsAllProperties()
    {
        // Arrange & Act
        var sut = new AttentionPolicy(
            MaxWorkspaceSize: 100,
            MaxHighPriorityItems: 10,
            DefaultItemLifetime: TimeSpan.FromMinutes(30),
            MinAttentionThreshold: 0.5);

        // Assert
        sut.MaxWorkspaceSize.Should().Be(100);
        sut.MaxHighPriorityItems.Should().Be(10);
        sut.DefaultItemLifetime.Should().Be(TimeSpan.FromMinutes(30));
        sut.MinAttentionThreshold.Should().Be(0.5);
    }

    [Fact]
    public void Equality_TwoIdenticalRecords_AreEqual()
    {
        // Arrange
        var a = new AttentionPolicy(50, 5, TimeSpan.FromMinutes(10), 0.3);
        var b = new AttentionPolicy(50, 5, TimeSpan.FromMinutes(10), 0.3);

        // Act & Assert
        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentRecords_AreNotEqual()
    {
        // Arrange
        var a = new AttentionPolicy(50, 5, TimeSpan.FromMinutes(10), 0.3);
        var b = new AttentionPolicy(100, 10, TimeSpan.FromMinutes(20), 0.5);

        // Act & Assert
        a.Should().NotBe(b);
    }

    [Fact]
    public void With_ModifiedProperty_CreatesNewRecord()
    {
        // Arrange
        var original = new AttentionPolicy(50, 5, TimeSpan.FromMinutes(10), 0.3);

        // Act
        var modified = original with { MaxWorkspaceSize = 200 };

        // Assert
        modified.MaxWorkspaceSize.Should().Be(200);
        original.MaxWorkspaceSize.Should().Be(50);
    }

    [Fact]
    public void Constructor_WithZeroValues_SetsCorrectly()
    {
        // Arrange & Act
        var sut = new AttentionPolicy(0, 0, TimeSpan.Zero, 0.0);

        // Assert
        sut.MaxWorkspaceSize.Should().Be(0);
        sut.MaxHighPriorityItems.Should().Be(0);
        sut.DefaultItemLifetime.Should().Be(TimeSpan.Zero);
        sut.MinAttentionThreshold.Should().Be(0.0);
    }
}
