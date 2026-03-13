// <copyright file="ConfidenceRatingTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent;

namespace Ouroboros.Tests;

/// <summary>
/// Unit tests for the <see cref="ConfidenceRating"/> enum.
/// Covers value definitions, count, and ordinal stability.
/// </summary>
[Trait("Category", "Unit")]
public class ConfidenceRatingTests
{
    [Theory]
    [InlineData(ConfidenceRating.Low)]
    [InlineData(ConfidenceRating.Medium)]
    [InlineData(ConfidenceRating.High)]
    public void AllValues_AreDefined(ConfidenceRating rating)
    {
        // Assert
        Enum.IsDefined(rating).Should().BeTrue();
    }

    [Fact]
    public void HasThreeValues()
    {
        // Assert
        Enum.GetValues<ConfidenceRating>().Should().HaveCount(3);
    }

    [Fact]
    public void Low_IsDefaultValue()
    {
        // Arrange
        var defaultRating = default(ConfidenceRating);

        // Assert
        defaultRating.Should().Be(ConfidenceRating.Low);
    }

    [Theory]
    [InlineData(ConfidenceRating.Low, 0)]
    [InlineData(ConfidenceRating.Medium, 1)]
    [InlineData(ConfidenceRating.High, 2)]
    public void OrdinalValues_AreStable(ConfidenceRating rating, int expectedOrdinal)
    {
        // Assert
        ((int)rating).Should().Be(expectedOrdinal);
    }

    [Fact]
    public void ValuesAreOrdered_LowLessThanMediumLessThanHigh()
    {
        // Assert
        ((int)ConfidenceRating.Low).Should().BeLessThan((int)ConfidenceRating.Medium);
        ((int)ConfidenceRating.Medium).Should().BeLessThan((int)ConfidenceRating.High);
    }

    [Fact]
    public void ToString_ReturnsExpectedNames()
    {
        // Assert
        ConfidenceRating.Low.ToString().Should().Be("Low");
        ConfidenceRating.Medium.ToString().Should().Be("Medium");
        ConfidenceRating.High.ToString().Should().Be("High");
    }

    [Fact]
    public void Parse_ValidString_ReturnsCorrectValue()
    {
        // Act & Assert
        Enum.Parse<ConfidenceRating>("Low").Should().Be(ConfidenceRating.Low);
        Enum.Parse<ConfidenceRating>("Medium").Should().Be(ConfidenceRating.Medium);
        Enum.Parse<ConfidenceRating>("High").Should().Be(ConfidenceRating.High);
    }

    [Fact]
    public void Parse_InvalidString_ThrowsArgumentException()
    {
        // Act
        var act = () => Enum.Parse<ConfidenceRating>("VeryHigh");

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}
