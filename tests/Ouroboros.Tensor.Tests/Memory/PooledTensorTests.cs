// <copyright file="PooledTensorTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Memory;

[Trait("Category", "Unit")]
public sealed class PooledTensorTests
{
    [Fact]
    public void Rent_CreatesCorrectShape()
    {
        // Arrange
        var shape = TensorShape.Of(3, 4);

        // Act
        using var tensor = TensorMemoryPool.Rent<float>(shape);

        // Assert
        tensor.Shape.Should().Be(shape);
        tensor.Device.Should().Be(DeviceType.Cpu);
    }

    [Fact]
    public void RentAndFill_CopiesDataCorrectly()
    {
        // Arrange
        var data = new float[] { 1f, 2f, 3f, 4f };
        var shape = TensorShape.Of(4);

        // Act
        using var tensor = TensorMemoryPool.RentAndFill(shape, data.AsSpan());

        // Assert
        tensor.AsSpan().ToArray().Should().Equal(data);
    }

    [Fact]
    public void RentAndFill_MismatchedLength_ThrowsArgumentException()
    {
        var data = new float[] { 1f, 2f, 3f };
        var shape = TensorShape.Of(4);

        var act = () => TensorMemoryPool.RentAndFill(shape, data.AsSpan());

        act.Should().Throw<ArgumentException>().WithMessage("*length*");
    }

    [Fact]
    public void AsMemory_ReturnsCorrectLength()
    {
        var data = new float[] { 1f, 2f, 3f };
        using var tensor = TensorMemoryPool.RentAndFill(TensorShape.Of(3), data.AsSpan());
        tensor.AsMemory().Length.Should().Be(3);
    }

    [Fact]
    public void ToCpu_ReturnsSelf()
    {
        using var tensor = TensorMemoryPool.Rent<float>(TensorShape.Of(4));
        tensor.ToCpu().Should().BeSameAs(tensor);
    }

    [Fact]
    public void ToGpu_ThrowsNotSupportedException()
    {
        using var tensor = TensorMemoryPool.Rent<float>(TensorShape.Of(4));
        tensor.Invoking(t => t.ToGpu())
            .Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void AsSpan_AfterDispose_ThrowsObjectDisposedException()
    {
        var tensor = TensorMemoryPool.Rent<float>(TensorShape.Of(2));
        tensor.Dispose();

        tensor.Invoking(t => t.AsSpan())
            .Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void AsMemory_AfterDispose_ThrowsObjectDisposedException()
    {
        var tensor = TensorMemoryPool.Rent<float>(TensorShape.Of(2));
        tensor.Dispose();

        tensor.Invoking(t => t.AsMemory())
            .Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var tensor = TensorMemoryPool.Rent<float>(TensorShape.Of(2));
        tensor.Dispose();

        // Second dispose must be idempotent
        tensor.Invoking(t => t.Dispose()).Should().NotThrow();
    }
}
