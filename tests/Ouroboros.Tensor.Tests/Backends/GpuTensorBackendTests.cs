// <copyright file="GpuTensorBackendTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Backends;

[Trait("Category", "Unit")]
public sealed class GpuTensorBackendTests
{
    private readonly GpuTensorBackend _sut = new();

    [Fact]
    public void Device_IsCuda()
    {
        _sut.Device.Should().Be(DeviceType.Cuda);
    }

    [Fact]
    public void Create_ThrowsNotSupportedException()
    {
        _sut.Invoking(b => b.Create(TensorShape.Of(2), new float[] { 1f, 2f }))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*TorchSharp*");
    }

    [Fact]
    public void MatMul_ReturnsFailureResult()
    {
        var fakeTensor = Substitute.For<ITensor<float>>();
        fakeTensor.Shape.Returns(TensorShape.Of(2, 2));
        fakeTensor.Device.Returns(DeviceType.Cuda);

        var result = _sut.MatMul(fakeTensor, fakeTensor);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("TorchSharp");
    }

    [Fact]
    public void Add_ReturnsFailureResult()
    {
        var fakeTensor = Substitute.For<ITensor<float>>();
        fakeTensor.Shape.Returns(TensorShape.Of(2));
        fakeTensor.Device.Returns(DeviceType.Cuda);

        var result = _sut.Add(fakeTensor, fakeTensor);

        result.IsSuccess.Should().BeFalse();
    }
}
