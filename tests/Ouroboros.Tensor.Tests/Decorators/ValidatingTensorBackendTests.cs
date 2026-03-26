// <copyright file="ValidatingTensorBackendTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Decorators;

[Trait("Category", "Unit")]
public sealed class ValidatingTensorBackendTests
{
    private readonly ValidatingTensorBackend _sut = new(CpuTensorBackend.Instance);

    [Fact]
    public void Add_SameShape_Delegates()
    {
        using var a = _sut.Create(TensorShape.Of(3), new float[] { 1f, 2f, 3f });
        using var b = _sut.Create(TensorShape.Of(3), new float[] { 4f, 5f, 6f });

        var result = _sut.Add(a, b);

        result.IsSuccess.Should().BeTrue();
        result.Value.AsSpan().ToArray().Should().Equal(5f, 7f, 9f);
        result.Value.Dispose();
    }

    [Fact]
    public void Add_MismatchedShape_ReturnsFailureBeforeInnerCall()
    {
        // Arrange: wrap an inner that would panic if called
        var inner = Substitute.For<ITensorBackend>();
        inner.Device.Returns(DeviceType.Cpu);
        var validating = new ValidatingTensorBackend(inner);

        var ta = Substitute.For<ITensor<float>>();
        ta.Shape.Returns(TensorShape.Of(3));
        ta.Device.Returns(DeviceType.Cpu);

        var tb = Substitute.For<ITensor<float>>();
        tb.Shape.Returns(TensorShape.Of(4));
        tb.Device.Returns(DeviceType.Cpu);

        // Act
        var result = validating.Add(ta, tb);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("mismatch");
        inner.DidNotReceive().Add(Arg.Any<ITensor<float>>(), Arg.Any<ITensor<float>>());
    }

    [Fact]
    public void MatMul_ValidShapes_Delegates()
    {
        using var a = _sut.Create(TensorShape.Of(2, 3), new float[6]);
        using var b = _sut.Create(TensorShape.Of(3, 4), new float[12]);

        var result = _sut.MatMul(a, b);

        result.IsSuccess.Should().BeTrue();
        result.Value.Shape.Should().Be(TensorShape.Of(2, 4));
        result.Value.Dispose();
    }

    [Fact]
    public void MatMul_InnerDimensionMismatch_ReturnsFailure()
    {
        using var a = _sut.Create(TensorShape.Of(2, 3), new float[6]);
        using var b = _sut.Create(TensorShape.Of(4, 2), new float[8]);

        var result = _sut.MatMul(a, b);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("mismatch");
    }

    [Fact]
    public void MatMul_WrongDevice_ReturnsFailure()
    {
        var inner = Substitute.For<ITensorBackend>();
        inner.Device.Returns(DeviceType.Cpu);
        var validating = new ValidatingTensorBackend(inner);

        var gpuTensor = Substitute.For<ITensor<float>>();
        gpuTensor.Shape.Returns(TensorShape.Of(2, 2));
        gpuTensor.Device.Returns(DeviceType.Cuda);

        var cpuTensor = Substitute.For<ITensor<float>>();
        cpuTensor.Shape.Returns(TensorShape.Of(2, 2));
        cpuTensor.Device.Returns(DeviceType.Cpu);

        var result = validating.MatMul(gpuTensor, cpuTensor);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Cuda");
    }

    [Fact]
    public void Create_WrongDataLength_ThrowsArgumentException()
    {
        _sut.Invoking(b => b.Create(TensorShape.Of(4), new float[] { 1f, 2f }))
            .Should().Throw<ArgumentException>().WithMessage("*length*");
    }
}
