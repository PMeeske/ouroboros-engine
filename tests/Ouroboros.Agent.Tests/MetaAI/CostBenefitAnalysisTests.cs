// <copyright file="CostBenefitAnalysisTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;

namespace Ouroboros.Agent.Tests.MetaAI;

/// <summary>
/// Unit tests for the CostBenefitAnalysis record.
/// </summary>
[Trait("Category", "Unit")]
public class CostBenefitAnalysisTests
{
    [Fact]
    public void Constructor_WithValidParameters_SetsAllProperties()
    {
        // Arrange & Act
        var sut = new CostBenefitAnalysis(
            RecommendedRoute: "gpt-4",
            EstimatedCost: 0.05,
            EstimatedQuality: 0.95,
            ValueScore: 19.0,
            Rationale: "Best quality per cost");

        // Assert
        sut.RecommendedRoute.Should().Be("gpt-4");
        sut.EstimatedCost.Should().Be(0.05);
        sut.EstimatedQuality.Should().Be(0.95);
        sut.ValueScore.Should().Be(19.0);
        sut.Rationale.Should().Be("Best quality per cost");
    }

    [Fact]
    public void Equality_TwoIdenticalRecords_AreEqual()
    {
        // Arrange
        var a = new CostBenefitAnalysis("route-a", 1.0, 0.8, 0.8, "reason");
        var b = new CostBenefitAnalysis("route-a", 1.0, 0.8, 0.8, "reason");

        // Act & Assert
        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentRecords_AreNotEqual()
    {
        // Arrange
        var a = new CostBenefitAnalysis("route-a", 1.0, 0.8, 0.8, "reason");
        var b = new CostBenefitAnalysis("route-b", 2.0, 0.9, 0.45, "other reason");

        // Act & Assert
        a.Should().NotBe(b);
    }

    [Fact]
    public void With_ModifiedProperty_CreatesNewRecord()
    {
        // Arrange
        var original = new CostBenefitAnalysis("route-a", 1.0, 0.8, 0.8, "reason");

        // Act
        var modified = original with { EstimatedCost = 2.0 };

        // Assert
        modified.EstimatedCost.Should().Be(2.0);
        modified.RecommendedRoute.Should().Be("route-a");
        original.EstimatedCost.Should().Be(1.0);
    }

    [Fact]
    public void Constructor_WithZeroValues_SetsCorrectly()
    {
        // Arrange & Act
        var sut = new CostBenefitAnalysis("free-tier", 0.0, 0.0, 0.0, "");

        // Assert
        sut.EstimatedCost.Should().Be(0.0);
        sut.EstimatedQuality.Should().Be(0.0);
        sut.ValueScore.Should().Be(0.0);
        sut.Rationale.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithNegativeValues_SetsCorrectly()
    {
        // Arrange & Act
        var sut = new CostBenefitAnalysis("route", -1.0, -0.5, -2.0, "negative");

        // Assert
        sut.EstimatedCost.Should().Be(-1.0);
        sut.ValueScore.Should().Be(-2.0);
    }
}
