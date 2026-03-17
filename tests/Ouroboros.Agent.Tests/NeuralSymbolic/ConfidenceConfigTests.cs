// <copyright file="ConfidenceConfigTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.NeuralSymbolic;

namespace Ouroboros.Agent.Tests.NeuralSymbolic;

/// <summary>
/// Unit tests for <see cref="ConfidenceConfig"/>.
/// </summary>
[Trait("Category", "Unit")]
public class ConfidenceConfigTests
{
    [Fact]
    public void DefaultValues_AreCorrectHeuristics()
    {
        // Arrange & Act
        var config = new ConfidenceConfig();

        // Assert
        config.SymbolicVerifiedNeural.Should().Be(0.9);
        config.UnverifiedNeural.Should().Be(0.6);
        config.ParallelAgreement.Should().Be(0.95);
        config.BaseGrounding.Should().Be(0.8);
        config.ConflictPenalty.Should().Be(0.2);
    }

    [Fact]
    public void WithCustomValues_OverridesDefaults()
    {
        // Arrange & Act
        var config = new ConfidenceConfig
        {
            SymbolicVerifiedNeural = 0.99,
            UnverifiedNeural = 0.5,
            ParallelAgreement = 0.85,
            BaseGrounding = 0.7,
            ConflictPenalty = 0.3
        };

        // Assert
        config.SymbolicVerifiedNeural.Should().Be(0.99);
        config.UnverifiedNeural.Should().Be(0.5);
        config.ParallelAgreement.Should().Be(0.85);
        config.BaseGrounding.Should().Be(0.7);
        config.ConflictPenalty.Should().Be(0.3);
    }

    [Fact]
    public void RecordEquality_WithSameValues_AreEqual()
    {
        // Arrange
        var config1 = new ConfidenceConfig { SymbolicVerifiedNeural = 0.85 };
        var config2 = new ConfidenceConfig { SymbolicVerifiedNeural = 0.85 };

        // Assert
        config1.Should().Be(config2);
    }

    [Fact]
    public void RecordEquality_WithDifferentValues_AreNotEqual()
    {
        // Arrange
        var config1 = new ConfidenceConfig { SymbolicVerifiedNeural = 0.85 };
        var config2 = new ConfidenceConfig { SymbolicVerifiedNeural = 0.90 };

        // Assert
        config1.Should().NotBe(config2);
    }

    [Fact]
    public void WithExpression_CreatesModifiedCopy()
    {
        // Arrange
        var original = new ConfidenceConfig();

        // Act
        var modified = original with { ConflictPenalty = 0.5 };

        // Assert
        modified.ConflictPenalty.Should().Be(0.5);
        original.ConflictPenalty.Should().Be(0.2); // original unchanged
    }
}
