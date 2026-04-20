// <copyright file="CostOptimizationStrategyTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;

namespace Ouroboros.Agent.Tests.MetaAI;

/// <summary>
/// Unit tests for the CostOptimizationStrategy enum.
/// </summary>
[Trait("Category", "Unit")]
public class CostOptimizationStrategyTests
{
    [Theory]
    [InlineData(CostOptimizationStrategy.MinimizeCost, 0)]
    [InlineData(CostOptimizationStrategy.MaximizeQuality, 1)]
    [InlineData(CostOptimizationStrategy.Balanced, 2)]
    [InlineData(CostOptimizationStrategy.MaximizeValue, 3)]
    public void EnumValues_HaveExpectedNumericValues(CostOptimizationStrategy strategy, int expected)
    {
        // Act & Assert
        ((int)strategy).Should().Be(expected);
    }

    [Fact]
    public void Enum_HasFourValues()
    {
        // Act
        var values = Enum.GetValues<CostOptimizationStrategy>();

        // Assert
        values.Should().HaveCount(4);
    }

    [Fact]
    public void Enum_DefaultValue_IsMinimizeCost()
    {
        // Arrange & Act
        CostOptimizationStrategy defaultValue = default;

        // Assert
        defaultValue.Should().Be(CostOptimizationStrategy.MinimizeCost);
    }

    [Theory]
    [InlineData("MinimizeCost", true)]
    [InlineData("MaximizeQuality", true)]
    [InlineData("Balanced", true)]
    [InlineData("MaximizeValue", true)]
    [InlineData("InvalidStrategy", false)]
    public void TryParse_ReturnsExpected(string input, bool expectedResult)
    {
        // Act
        bool result = Enum.TryParse<CostOptimizationStrategy>(input, out _);

        // Assert
        result.Should().Be(expectedResult);
    }
}
