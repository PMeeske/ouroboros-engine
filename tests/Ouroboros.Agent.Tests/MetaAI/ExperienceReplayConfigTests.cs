// <copyright file="ExperienceReplayConfigTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;

namespace Ouroboros.Agent.Tests.MetaAI;

/// <summary>
/// Unit tests for the ExperienceReplayConfig record.
/// </summary>
[Trait("Category", "Unit")]
public class ExperienceReplayConfigTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var config = new ExperienceReplayConfig();

        // Assert
        config.BatchSize.Should().Be(10);
        config.MinQualityScore.Should().Be(0.6);
        config.MaxExperiences.Should().Be(100);
        config.PrioritizeHighQuality.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithCustomValues_SetsCorrectly()
    {
        // Arrange & Act
        var config = new ExperienceReplayConfig(
            BatchSize: 32,
            MinQualityScore: 0.8,
            MaxExperiences: 500,
            PrioritizeHighQuality: false);

        // Assert
        config.BatchSize.Should().Be(32);
        config.MinQualityScore.Should().Be(0.8);
        config.MaxExperiences.Should().Be(500);
        config.PrioritizeHighQuality.Should().BeFalse();
    }

    [Fact]
    public void Equality_TwoIdenticalRecords_AreEqual()
    {
        // Arrange
        var a = new ExperienceReplayConfig(20, 0.7, 200, true);
        var b = new ExperienceReplayConfig(20, 0.7, 200, true);

        // Act & Assert
        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentRecords_AreNotEqual()
    {
        // Arrange
        var a = new ExperienceReplayConfig(10, 0.6, 100, true);
        var b = new ExperienceReplayConfig(20, 0.8, 200, false);

        // Act & Assert
        a.Should().NotBe(b);
    }

    [Fact]
    public void With_ModifiedProperty_CreatesNewRecord()
    {
        // Arrange
        var original = new ExperienceReplayConfig();

        // Act
        var modified = original with { BatchSize = 64 };

        // Assert
        modified.BatchSize.Should().Be(64);
        original.BatchSize.Should().Be(10);
    }

    [Fact]
    public void Constructor_WithZeroBatchSize_SetsCorrectly()
    {
        // Arrange & Act
        var config = new ExperienceReplayConfig(BatchSize: 0);

        // Assert
        config.BatchSize.Should().Be(0);
    }
}
