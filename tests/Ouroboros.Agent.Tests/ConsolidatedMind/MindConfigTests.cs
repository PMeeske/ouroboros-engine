// <copyright file="MindConfigTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.ConsolidatedMind;

namespace Ouroboros.Tests.ConsolidatedMind;

[Trait("Category", "Unit")]
public sealed class MindConfigTests
{
    [Fact]
    public void DefaultConstructor_SetsExpectedDefaults()
    {
        // Act
        var config = new MindConfig();

        // Assert
        config.EnableThinking.Should().BeTrue();
        config.EnableVerification.Should().BeTrue();
        config.EnableParallelExecution.Should().BeTrue();
        config.MaxParallelism.Should().Be(3);
        config.DefaultTimeout.Should().BeNull();
        config.FallbackOnError.Should().BeTrue();
    }

    [Fact]
    public void Minimal_DisablesThinkingAndVerification()
    {
        // Act
        var config = MindConfig.Minimal();

        // Assert
        config.EnableThinking.Should().BeFalse();
        config.EnableVerification.Should().BeFalse();
        config.EnableParallelExecution.Should().BeFalse();
        config.MaxParallelism.Should().Be(1);
        config.FallbackOnError.Should().BeTrue();
    }

    [Fact]
    public void HighQuality_EnablesAllFeatures()
    {
        // Act
        var config = MindConfig.HighQuality();

        // Assert
        config.EnableThinking.Should().BeTrue();
        config.EnableVerification.Should().BeTrue();
        config.EnableParallelExecution.Should().BeTrue();
        config.MaxParallelism.Should().Be(4);
        config.DefaultTimeout.Should().Be(TimeSpan.FromMinutes(5));
        config.FallbackOnError.Should().BeTrue();
    }

    [Fact]
    public void CustomValues_ArePreserved()
    {
        // Arrange & Act
        var timeout = TimeSpan.FromSeconds(30);
        var config = new MindConfig(
            EnableThinking: false,
            EnableVerification: false,
            EnableParallelExecution: true,
            MaxParallelism: 8,
            DefaultTimeout: timeout,
            FallbackOnError: false);

        // Assert
        config.EnableThinking.Should().BeFalse();
        config.EnableVerification.Should().BeFalse();
        config.EnableParallelExecution.Should().BeTrue();
        config.MaxParallelism.Should().Be(8);
        config.DefaultTimeout.Should().Be(timeout);
        config.FallbackOnError.Should().BeFalse();
    }

    [Fact]
    public void Record_Equality_WorksCorrectly()
    {
        // Arrange
        var config1 = new MindConfig(EnableThinking: true, MaxParallelism: 3);
        var config2 = new MindConfig(EnableThinking: true, MaxParallelism: 3);
        var config3 = new MindConfig(EnableThinking: false, MaxParallelism: 3);

        // Assert
        config1.Should().Be(config2);
        config1.Should().NotBe(config3);
    }

    [Fact]
    public void With_Expression_CreatesModifiedCopy()
    {
        // Arrange
        var original = MindConfig.HighQuality();

        // Act
        var modified = original with { EnableThinking = false };

        // Assert
        modified.EnableThinking.Should().BeFalse();
        modified.EnableVerification.Should().Be(original.EnableVerification);
        modified.MaxParallelism.Should().Be(original.MaxParallelism);
        original.EnableThinking.Should().BeTrue();
    }
}
