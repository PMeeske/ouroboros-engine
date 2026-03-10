// <copyright file="NanoAtomConfigTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.NanoAtoms;

namespace Ouroboros.Tests.NanoAtoms;

[Trait("Category", "Unit")]
public sealed class NanoAtomConfigTests
{
    [Fact]
    public void Default_ReturnsBalancedConfig()
    {
        // Act
        var config = NanoAtomConfig.Default();

        // Assert
        config.MaxInputTokens.Should().Be(512);
        config.MaxOutputTokens.Should().Be(256);
        config.DigestTargetTokens.Should().Be(128);
        config.MaxParallelAtoms.Should().Be(4);
        config.ConsolidationThreshold.Should().Be(0.6);
        config.EnableSelfCritique.Should().BeTrue();
        config.EnableCircuitBreaker.Should().BeTrue();
        config.CircuitBreakerFailureThreshold.Should().Be(3);
        config.UseGoalDecomposer.Should().BeTrue();
        config.AtomTimeout.Should().BeNull();
    }

    [Fact]
    public void Minimal_ReturnsFastConfig()
    {
        // Act
        var config = NanoAtomConfig.Minimal();

        // Assert
        config.MaxInputTokens.Should().Be(256);
        config.MaxOutputTokens.Should().Be(128);
        config.DigestTargetTokens.Should().Be(64);
        config.MaxParallelAtoms.Should().Be(2);
        config.EnableSelfCritique.Should().BeFalse();
        config.EnableCircuitBreaker.Should().BeFalse();
        config.UseGoalDecomposer.Should().BeFalse();
    }

    [Fact]
    public void HighQuality_ReturnsLargeBudgetConfig()
    {
        // Act
        var config = NanoAtomConfig.HighQuality();

        // Assert
        config.MaxInputTokens.Should().Be(1024);
        config.MaxOutputTokens.Should().Be(512);
        config.DigestTargetTokens.Should().Be(256);
        config.MaxParallelAtoms.Should().Be(6);
        config.EnableSelfCritique.Should().BeTrue();
        config.EnableCircuitBreaker.Should().BeTrue();
        config.UseGoalDecomposer.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithCustomValues_SetsAll()
    {
        // Act
        var config = new NanoAtomConfig(
            MaxInputTokens: 1000,
            MaxOutputTokens: 500,
            DigestTargetTokens: 200,
            MaxParallelAtoms: 8,
            ConsolidationThreshold: 0.9,
            AtomTimeout: TimeSpan.FromSeconds(30));

        // Assert
        config.MaxInputTokens.Should().Be(1000);
        config.MaxOutputTokens.Should().Be(500);
        config.DigestTargetTokens.Should().Be(200);
        config.MaxParallelAtoms.Should().Be(8);
        config.ConsolidationThreshold.Should().Be(0.9);
        config.AtomTimeout.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void DigestPrompt_HasDefaultTemplate()
    {
        // Act
        var config = NanoAtomConfig.Default();

        // Assert
        config.DigestPrompt.Should().Contain("{0}");
        config.DigestPrompt.Should().Contain("{1}");
    }

    [Fact]
    public void ProcessPrompt_HasDefaultTemplate()
    {
        // Act
        var config = NanoAtomConfig.Default();

        // Assert
        config.ProcessPrompt.Should().Contain("{0}");
    }

    [Fact]
    public void WithExpression_CreatesModifiedCopy()
    {
        // Arrange
        var config = NanoAtomConfig.Default();

        // Act
        var modified = config with { MaxInputTokens = 2048 };

        // Assert
        modified.MaxInputTokens.Should().Be(2048);
        modified.MaxOutputTokens.Should().Be(256);
    }
}
