// <copyright file="DefaultBackendSelectorTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Backends;

[Trait("Category", "Unit")]
public sealed class DefaultBackendSelectorTests
{
    [Fact]
    public void IsGpuAvailable_WhenNoGpuBackend_ReturnsFalse()
    {
        var selector = new DefaultBackendSelector();
        selector.IsGpuAvailable.Should().BeFalse();
    }

    [Fact]
    public void IsGpuAvailable_WhenGpuBackendProvided_ReturnsTrue()
    {
        var fakeGpu = Substitute.For<ITensorBackend>();
        fakeGpu.Device.Returns(DeviceType.Cuda);

        var selector = new DefaultBackendSelector(CpuTensorBackend.Instance, fakeGpu);

        selector.IsGpuAvailable.Should().BeTrue();
    }

    [Fact]
    public void SelectBackend_CpuPreference_ReturnsCpuBackend()
    {
        var selector = new DefaultBackendSelector();
        var backend = selector.SelectBackend(DeviceType.Cpu);
        backend.Device.Should().Be(DeviceType.Cpu);
    }

    [Fact]
    public void SelectBackend_GpuPreferenceWithNoGpu_FallsBackToCpu()
    {
        var selector = new DefaultBackendSelector();
        var backend = selector.SelectBackend(DeviceType.Cuda);
        backend.Device.Should().Be(DeviceType.Cpu);
    }

    [Fact]
    public void SelectBackend_GpuPreferenceWithGpuAvailable_ReturnsGpuBackend()
    {
        var fakeGpu = Substitute.For<ITensorBackend>();
        fakeGpu.Device.Returns(DeviceType.Cuda);

        var selector = new DefaultBackendSelector(CpuTensorBackend.Instance, fakeGpu);

        var backend = selector.SelectBackend(DeviceType.Cuda);
        backend.Device.Should().Be(DeviceType.Cuda);
    }

    [Fact]
    public void SelectBackend_DefaultPreference_ReturnsCpu()
    {
        var selector = new DefaultBackendSelector();
        var backend = selector.SelectBackend(); // Default param
        backend.Device.Should().Be(DeviceType.Cpu);
    }
}
