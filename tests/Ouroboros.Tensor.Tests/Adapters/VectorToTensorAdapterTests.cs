// <copyright file="VectorToTensorAdapterTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Adapters;

[Trait("Category", "Unit")]
public sealed class VectorToTensorAdapterTests
{
    private readonly VectorToTensorAdapter _sut = new(CpuTensorBackend.Instance);

    [Fact]
    public void Convert_SingleVector_Returns1DTensor()
    {
        // Arrange
        var vector = new float[] { 0.1f, 0.2f, 0.3f };

        // Act
        using var tensor = _sut.Convert(vector);

        // Assert
        tensor.Shape.Should().Be(TensorShape.Of(3));
        tensor.AsSpan().ToArray().Should().Equal(vector);
    }

    [Fact]
    public void Convert_EmptyVector_ThrowsArgumentException()
    {
        _sut.Invoking(a => a.Convert(Array.Empty<float>()))
            .Should().Throw<ArgumentException>().WithMessage("*empty*");
    }

    [Fact]
    public void Convert_NullVector_ThrowsArgumentNullException()
    {
        _sut.Invoking(a => a.Convert(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ConvertBatch_UniformVectors_Returns2DTensor()
    {
        // Arrange
        var vectors = new List<float[]>
        {
            new[] { 1f, 2f, 3f },
            new[] { 4f, 5f, 6f },
        };

        // Act
        using var tensor = _sut.ConvertBatch(vectors);

        // Assert
        tensor.Shape.Should().Be(TensorShape.Of(2, 3));
        tensor.AsSpan().ToArray().Should().Equal(1f, 2f, 3f, 4f, 5f, 6f);
    }

    [Fact]
    public void ConvertBatch_EmptyList_ThrowsArgumentException()
    {
        _sut.Invoking(a => a.ConvertBatch(new List<float[]>()))
            .Should().Throw<ArgumentException>().WithMessage("*empty*");
    }

    [Fact]
    public void ConvertBatch_InconsistentDimensions_ThrowsArgumentException()
    {
        var vectors = new List<float[]>
        {
            new[] { 1f, 2f, 3f },
            new[] { 4f, 5f },   // wrong dimension
        };

        _sut.Invoking(a => a.ConvertBatch(vectors))
            .Should().Throw<ArgumentException>().WithMessage("*dimension*");
    }
}
