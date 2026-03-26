// <copyright file="CpuTensorBackendTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Backends;

[Trait("Category", "Unit")]
public sealed class CpuTensorBackendTests
{
    private readonly CpuTensorBackend _sut = CpuTensorBackend.Instance;

    [Fact]
    public void Device_IsCpu()
    {
        _sut.Device.Should().Be(DeviceType.Cpu);
    }

    [Fact]
    public void Create_CopiesDataIntoTensor()
    {
        // Arrange
        var data = new float[] { 1f, 2f, 3f, 4f };
        var shape = TensorShape.Of(2, 2);

        // Act
        using var tensor = _sut.Create(shape, data.AsSpan());

        // Assert
        tensor.Shape.Should().Be(shape);
        tensor.AsSpan().ToArray().Should().Equal(data);
    }

    [Fact]
    public void CreateUninitialized_ReturnsCorrectShape()
    {
        var shape = TensorShape.Of(3, 4);
        using var tensor = _sut.CreateUninitialized(shape);
        tensor.Shape.Should().Be(shape);
    }

    [Fact]
    public void FromMemory_WrapsWithoutCopy()
    {
        // Arrange
        var data = new float[] { 10f, 20f, 30f };
        var memory = data.AsMemory();
        var shape = TensorShape.Of(3);

        // Act
        using var tensor = _sut.FromMemory(memory, shape);

        // Assert — same values visible through tensor
        tensor.AsSpan().ToArray().Should().Equal(data);
    }

    // ── Add ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Add_SameShape_ProducesElementWiseSum()
    {
        // Arrange
        var aData = new float[] { 1f, 2f, 3f };
        var bData = new float[] { 4f, 5f, 6f };
        using var a = _sut.Create(TensorShape.Of(3), aData.AsSpan());
        using var b = _sut.Create(TensorShape.Of(3), bData.AsSpan());

        // Act
        var result = _sut.Add(a, b);

        // Assert
        result.IsSuccess.Should().BeTrue();
        using var tensor = result.Value;
        tensor.AsSpan().ToArray().Should().Equal(5f, 7f, 9f);
    }

    [Fact]
    public void Add_MismatchedShapes_ReturnsFailure()
    {
        using var a = _sut.Create(TensorShape.Of(3), new float[] { 1f, 2f, 3f });
        using var b = _sut.Create(TensorShape.Of(2), new float[] { 1f, 2f });

        var result = _sut.Add(a, b);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("shape mismatch");
    }

    // ── MatMul ───────────────────────────────────────────────────────────────

    [Fact]
    public void MatMul_SquareMatrix_ProducesCorrectResult()
    {
        // Arrange: [[1,2],[3,4]] × [[5,6],[7,8]] = [[19,22],[43,50]]
        var aData = new float[] { 1f, 2f, 3f, 4f };
        var bData = new float[] { 5f, 6f, 7f, 8f };
        using var a = _sut.Create(TensorShape.Of(2, 2), aData.AsSpan());
        using var b = _sut.Create(TensorShape.Of(2, 2), bData.AsSpan());

        // Act
        var result = _sut.MatMul(a, b);

        // Assert
        result.IsSuccess.Should().BeTrue();
        using var tensor = result.Value;
        tensor.Shape.Should().Be(TensorShape.Of(2, 2));
        tensor.AsSpan().ToArray().Should().Equal(19f, 22f, 43f, 50f);
    }

    [Fact]
    public void MatMul_NonSquare_ProducesCorrectShape()
    {
        // 2×3 × 3×4 = 2×4
        using var a = _sut.Create(TensorShape.Of(2, 3), new float[] { 1f, 0f, 0f, 0f, 1f, 0f });
        using var b = _sut.Create(TensorShape.Of(3, 4), Enumerable.Range(1, 12).Select(i => (float)i).ToArray());

        var result = _sut.MatMul(a, b);

        result.IsSuccess.Should().BeTrue();
        result.Value.Shape.Should().Be(TensorShape.Of(2, 4));
        result.Value.Dispose();
    }

    [Fact]
    public void MatMul_IncompatibleInnerDimensions_ReturnsFailure()
    {
        using var a = _sut.Create(TensorShape.Of(2, 3), new float[6]);
        using var b = _sut.Create(TensorShape.Of(4, 2), new float[8]);

        var result = _sut.MatMul(a, b);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("mismatch");
    }

    [Fact]
    public void MatMul_LessThanRank2_ReturnsFailure()
    {
        using var a = _sut.Create(TensorShape.Of(3), new float[] { 1f, 2f, 3f });
        using var b = _sut.Create(TensorShape.Of(3), new float[] { 4f, 5f, 6f });

        var result = _sut.MatMul(a, b);

        result.IsSuccess.Should().BeFalse();
    }
}
