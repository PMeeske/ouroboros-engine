// <copyright file="MetaLearnerConfigTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.MetaAI.MetaLearning;

namespace Ouroboros.Tests.MetaAI.MetaLearning;

/// <summary>
/// Tests for MetaLearnerConfig record defaults and parameter handling.
/// </summary>
[Trait("Category", "Unit")]
public class MetaLearnerConfigTests
{
    [Fact]
    public void DefaultConstructor_SetsExpectedDefaults()
    {
        // Act
        var config = new MetaLearnerConfig();

        // Assert
        config.MinEpisodesForOptimization.Should().Be(10);
        config.MaxFewShotExamples.Should().Be(5);
        config.MinConfidenceThreshold.Should().Be(0.6);
        config.DefaultEvaluationWindow.Should().Be(TimeSpan.FromDays(30));
    }

    [Fact]
    public void ParameterizedConstructor_OverridesDefaults()
    {
        // Act
        var config = new MetaLearnerConfig(
            MinEpisodesForOptimization: 20,
            MaxFewShotExamples: 10,
            MinConfidenceThreshold: 0.8);

        // Assert
        config.MinEpisodesForOptimization.Should().Be(20);
        config.MaxFewShotExamples.Should().Be(10);
        config.MinConfidenceThreshold.Should().Be(0.8);
    }

    [Fact]
    public void Record_SupportsWithExpression()
    {
        // Arrange
        var original = new MetaLearnerConfig();

        // Act
        var modified = original with { MinEpisodesForOptimization = 50 };

        // Assert
        modified.MinEpisodesForOptimization.Should().Be(50);
        modified.MaxFewShotExamples.Should().Be(original.MaxFewShotExamples);
    }

    [Fact]
    public void Record_EqualityByValue()
    {
        // Arrange
        var config1 = new MetaLearnerConfig(10, 5, 0.6, TimeSpan.FromDays(30));
        var config2 = new MetaLearnerConfig();

        // Assert
        config1.Should().Be(config2);
    }
}
