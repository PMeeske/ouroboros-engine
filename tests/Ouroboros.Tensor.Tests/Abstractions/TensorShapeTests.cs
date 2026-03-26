// <copyright file="TensorShapeTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Abstractions;

[Trait("Category", "Unit")]
public sealed class TensorShapeTests
{
    [Fact]
    public void Of_WithValidDimensions_SetsCorrectRankAndCount()
    {
        // Arrange & Act
        var shape = TensorShape.Of(2, 3);

        // Assert
        shape.Rank.Should().Be(2);
        shape.ElementCount.Should().Be(6);
    }

    [Fact]
    public void Of_With1D_ReturnsCorrectElementCount()
    {
        var shape = TensorShape.Of(1024);
        shape.Rank.Should().Be(1);
        shape.ElementCount.Should().Be(1024);
    }

    [Fact]
    public void Of_With3D_ReturnsProductOfDimensions()
    {
        var shape = TensorShape.Of(4, 3, 2);
        shape.ElementCount.Should().Be(24);
    }

    [Fact]
    public void Constructor_WithZeroDimension_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var dims = ImmutableArray.Create(0, 3);

        // Act
        var act = () => new TensorShape(dims);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*All dimensions must be positive*");
    }

    [Fact]
    public void Constructor_WithNegativeDimension_ThrowsArgumentOutOfRangeException()
    {
        var dims = ImmutableArray.Create(2, -1);
        var act = () => new TensorShape(dims);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void IsCompatibleWith_SameShape_ReturnsTrue()
    {
        var a = TensorShape.Of(2, 3);
        var b = TensorShape.Of(2, 3);
        a.IsCompatibleWith(b).Should().BeTrue();
    }

    [Fact]
    public void IsCompatibleWith_DifferentShape_ReturnsFalse()
    {
        var a = TensorShape.Of(2, 3);
        var b = TensorShape.Of(3, 2);
        a.IsCompatibleWith(b).Should().BeFalse();
    }

    [Fact]
    public void IsCompatibleWith_DifferentRank_ReturnsFalse()
    {
        var a = TensorShape.Of(6);
        var b = TensorShape.Of(2, 3);
        a.IsCompatibleWith(b).Should().BeFalse();
    }

    [Fact]
    public void ToString_WithMultipleDimensions_FormatsCorrectly()
    {
        var shape = TensorShape.Of(2, 3, 4);
        shape.ToString().Should().Be("[2, 3, 4]");
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = TensorShape.Of(2, 3);
        var b = TensorShape.Of(2, 3);
        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var a = TensorShape.Of(2, 3);
        var b = TensorShape.Of(4, 5);
        a.Should().NotBe(b);
    }
}
