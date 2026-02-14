// <copyright file="PlanStrategyGeneTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Ouroboros.Agent.MetaAI.Evolution;

namespace Ouroboros.Tests.Evolution;

/// <summary>
/// Unit tests for PlanStrategyGene record functionality.
/// </summary>
[Trait("Category", "Unit")]
public class PlanStrategyGeneTests
{
    [Fact]
    public void Constructor_WithValidParameters_CreatesGene()
    {
        // Arrange & Act
        var gene = new PlanStrategyGene("TestStrategy", 0.5, "Test description");

        // Assert
        gene.StrategyName.Should().Be("TestStrategy");
        gene.Weight.Should().Be(0.5);
        gene.Description.Should().Be("Test description");
    }

    [Fact]
    public void IsValid_WithValidWeight_ReturnsTrue()
    {
        // Arrange
        var gene = new PlanStrategyGene("TestStrategy", 0.5, "Test description");

        // Act
        bool isValid = gene.IsValid();

        // Assert
        isValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(2.0)]
    public void IsValid_WithInvalidWeight_ReturnsFalse(double invalidWeight)
    {
        // Arrange
        var gene = new PlanStrategyGene("TestStrategy", invalidWeight, "Test description");

        // Act
        bool isValid = gene.IsValid();

        // Assert
        isValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void IsValid_WithInvalidStrategyName_ReturnsFalse(string? invalidName)
    {
        // Arrange
        var gene = new PlanStrategyGene(invalidName!, 0.5, "Test description");

        // Act
        bool isValid = gene.IsValid();

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void WithExpression_ModifiesWeight_CreatesNewGene()
    {
        // Arrange
        var original = new PlanStrategyGene("TestStrategy", 0.5, "Test description");

        // Act
        var modified = original with { Weight = 0.7 };

        // Assert
        modified.Weight.Should().Be(0.7);
        modified.StrategyName.Should().Be("TestStrategy");
        modified.Description.Should().Be("Test description");
        original.Weight.Should().Be(0.5); // Original unchanged
    }

    [Fact]
    public void WithExpression_ModifiesStrategyName_CreatesNewGene()
    {
        // Arrange
        var original = new PlanStrategyGene("TestStrategy", 0.5, "Test description");

        // Act
        var modified = original with { StrategyName = "NewStrategy" };

        // Assert
        modified.StrategyName.Should().Be("NewStrategy");
        modified.Weight.Should().Be(0.5);
        modified.Description.Should().Be("Test description");
        original.StrategyName.Should().Be("TestStrategy"); // Original unchanged
    }

    [Fact]
    public void Equality_WithSameValues_AreEqual()
    {
        // Arrange
        var gene1 = new PlanStrategyGene("TestStrategy", 0.5, "Test description");
        var gene2 = new PlanStrategyGene("TestStrategy", 0.5, "Test description");

        // Act & Assert
        gene1.Should().Be(gene2);
        (gene1 == gene2).Should().BeTrue();
    }

    [Fact]
    public void Equality_WithDifferentWeights_AreNotEqual()
    {
        // Arrange
        var gene1 = new PlanStrategyGene("TestStrategy", 0.5, "Test description");
        var gene2 = new PlanStrategyGene("TestStrategy", 0.6, "Test description");

        // Act & Assert
        gene1.Should().NotBe(gene2);
        (gene1 != gene2).Should().BeTrue();
    }

    [Fact]
    public void Mutate_CreatesNewGeneWithModifiedWeight()
    {
        // Arrange
        var original = new PlanStrategyGene("TestStrategy", 0.5, "Test description");
        var random = new Random(42); // Fixed seed for deterministic test

        // Act
        var mutated = original.Mutate(random, mutationRate: 0.1);

        // Assert
        mutated.StrategyName.Should().Be(original.StrategyName);
        mutated.Description.Should().Be(original.Description);
        mutated.Weight.Should().NotBe(original.Weight); // Weight should change
        mutated.Weight.Should().BeInRange(0.0, 1.0); // Should stay in valid range
    }

    [Fact]
    public void Mutate_WithLargeRate_ClampsToValidRange()
    {
        // Arrange
        var gene = new PlanStrategyGene("TestStrategy", 0.5, "Test description");
        var random = new Random(42);

        // Act
        var mutated = gene.Mutate(random, mutationRate: 1.0); // Large mutation rate

        // Assert
        mutated.Weight.Should().BeInRange(0.0, 1.0); // Should still be clamped
    }

    [Fact]
    public void Strategies_PlanningDepth_CreatesValidGene()
    {
        // Act
        var gene = PlanStrategyGene.Strategies.PlanningDepth(0.7);

        // Assert
        gene.StrategyName.Should().Be("PlanningDepth");
        gene.Weight.Should().Be(0.7);
        gene.Description.Should().NotBeNullOrWhiteSpace();
        gene.IsValid().Should().BeTrue();
    }

    [Fact]
    public void Strategies_ToolVsLLMWeight_CreatesValidGene()
    {
        // Act
        var gene = PlanStrategyGene.Strategies.ToolVsLLMWeight(0.8);

        // Assert
        gene.StrategyName.Should().Be("ToolVsLLMWeight");
        gene.Weight.Should().Be(0.8);
        gene.Description.Should().NotBeNullOrWhiteSpace();
        gene.IsValid().Should().BeTrue();
    }

    [Fact]
    public void Strategies_VerificationStrictness_CreatesValidGene()
    {
        // Act
        var gene = PlanStrategyGene.Strategies.VerificationStrictness(0.6);

        // Assert
        gene.StrategyName.Should().Be("VerificationStrictness");
        gene.Weight.Should().Be(0.6);
        gene.Description.Should().NotBeNullOrWhiteSpace();
        gene.IsValid().Should().BeTrue();
    }

    [Fact]
    public void Strategies_DecompositionGranularity_CreatesValidGene()
    {
        // Act
        var gene = PlanStrategyGene.Strategies.DecompositionGranularity(0.4);

        // Assert
        gene.StrategyName.Should().Be("DecompositionGranularity");
        gene.Weight.Should().Be(0.4);
        gene.Description.Should().NotBeNullOrWhiteSpace();
        gene.IsValid().Should().BeTrue();
    }
}
