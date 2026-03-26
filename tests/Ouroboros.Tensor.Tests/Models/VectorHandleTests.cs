// <copyright file="VectorHandleTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Models;

[Trait("Category", "Unit")]
public sealed class VectorHandleTests
{
    [Fact]
    public void VectorHandle_IsValueType_NoHeapAllocation()
    {
        // struct — sizeof test ensures value type semantics
        typeof(VectorHandle).IsValueType.Should().BeTrue();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        // Arrange
        var a = new VectorHandle("qdrant", "docs", "id-1", 768);
        var b = new VectorHandle("qdrant", "docs", "id-1", 768);

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentVectorId_AreNotEqual()
    {
        var a = new VectorHandle("qdrant", "docs", "id-1", 768);
        var b = new VectorHandle("qdrant", "docs", "id-2", 768);
        a.Should().NotBe(b);
    }

    [Fact]
    public void Validate_WithValidValues_DoesNotThrow()
    {
        var handle = new VectorHandle("qdrant", "collection", "vec-001", 1536);
        handle.Invoking(h => h.Validate()).Should().NotThrow();
    }

    [Fact]
    public void Validate_WithEmptyProviderId_ThrowsArgumentException()
    {
        var handle = new VectorHandle("", "collection", "vec-001", 768);
        handle.Invoking(h => h.Validate())
            .Should().Throw<ArgumentException>()
            .WithMessage("*ProviderId*");
    }

    [Fact]
    public void Validate_WithZeroDimension_ThrowsArgumentOutOfRangeException()
    {
        var handle = new VectorHandle("qdrant", "collection", "vec-001", 0);
        handle.Invoking(h => h.Validate())
            .Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Dimension*");
    }

    [Fact]
    public void Validate_WithNegativeDimension_ThrowsArgumentOutOfRangeException()
    {
        var handle = new VectorHandle("qdrant", "collection", "vec-001", -1);
        handle.Invoking(h => h.Validate())
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ToString_FormatsCorrectly()
    {
        var handle = new VectorHandle("qdrant", "docs", "abc", 768);
        handle.ToString().Should().Be("qdrant/docs/abc[768]");
    }
}
