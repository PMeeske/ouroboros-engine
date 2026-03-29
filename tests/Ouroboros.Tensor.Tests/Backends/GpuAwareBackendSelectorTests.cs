// <copyright file="GpuAwareBackendSelectorTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Backends;

[Trait("Category", "Unit")]
public sealed class GpuAwareBackendSelectorTests
{
    [Fact]
    public void SelectBackend_Cpu_AlwaysReturnsCpuBackend()
    {
        var sut = CreateSelector(openCl: null, cuda: null);

        sut.SelectBackend(DeviceType.Cpu).Device.Should().Be(DeviceType.Cpu);
    }

    [Fact]
    public void IsGpuAvailable_WhenNoGpuBackends_ReturnsFalse()
    {
        var sut = CreateSelector(openCl: null, cuda: null);

        sut.IsGpuAvailable.Should().BeFalse();
        sut.IsOpenClAvailable.Should().BeFalse();
        sut.IsCudaAvailable.Should().BeFalse();
    }

    [Fact]
    public void IsGpuAvailable_WhenOpenClAvailable_ReturnsTrue()
    {
        var sut = CreateSelector(openCl: StubBackend(DeviceType.OpenCL), cuda: null);

        sut.IsGpuAvailable.Should().BeTrue();
        sut.IsOpenClAvailable.Should().BeTrue();
        sut.IsCudaAvailable.Should().BeFalse();
    }

    [Fact]
    public void IsGpuAvailable_WhenCudaAvailable_ReturnsTrue()
    {
        var sut = CreateSelector(openCl: null, cuda: StubBackend(DeviceType.Cuda));

        sut.IsGpuAvailable.Should().BeTrue();
        sut.IsOpenClAvailable.Should().BeFalse();
        sut.IsCudaAvailable.Should().BeTrue();
    }

    // ── SelectBackend fallback chain ────────────────────────────────────

    [Fact]
    public void SelectBackend_OpenCl_ReturnsOpenClWhenAvailable()
    {
        var sut = CreateSelector(openCl: StubBackend(DeviceType.OpenCL), cuda: null);

        sut.SelectBackend(DeviceType.OpenCL).Device.Should().Be(DeviceType.OpenCL);
    }

    [Fact]
    public void SelectBackend_OpenCl_FallsToCpuWhenUnavailable()
    {
        var sut = CreateSelector(openCl: null, cuda: null);

        sut.SelectBackend(DeviceType.OpenCL).Device.Should().Be(DeviceType.Cpu);
    }

    [Fact]
    public void SelectBackend_Rocm_RoutesToOpenCl()
    {
        var sut = CreateSelector(openCl: StubBackend(DeviceType.OpenCL), cuda: null);

        sut.SelectBackend(DeviceType.Rocm).Device.Should().Be(DeviceType.OpenCL);
    }

    [Fact]
    public void SelectBackend_Cuda_FallsToOpenClWhenCudaUnavailable()
    {
        var sut = CreateSelector(openCl: StubBackend(DeviceType.OpenCL), cuda: null);

        sut.SelectBackend(DeviceType.Cuda).Device.Should().Be(DeviceType.OpenCL);
    }

    [Fact]
    public void SelectBackend_Cuda_ReturnsCudaWhenAvailable()
    {
        var sut = CreateSelector(
            openCl: StubBackend(DeviceType.OpenCL),
            cuda: StubBackend(DeviceType.Cuda));

        sut.SelectBackend(DeviceType.Cuda).Device.Should().Be(DeviceType.Cuda);
    }

    [Fact]
    public void SelectBackend_DirectML_FallsThroughOpenClThenCudaThenCpu()
    {
        // No GPU at all → CPU
        CreateSelector(openCl: null, cuda: null)
            .SelectBackend(DeviceType.DirectML).Device.Should().Be(DeviceType.Cpu);

        // OpenCL available → OpenCL
        CreateSelector(openCl: StubBackend(DeviceType.OpenCL), cuda: null)
            .SelectBackend(DeviceType.DirectML).Device.Should().Be(DeviceType.OpenCL);
    }

    // ── SelectBestGpu ───────────────────────────────────────────────────

    [Fact]
    public void SelectBestGpu_PrefersOpenCl_OverCuda()
    {
        var sut = CreateSelector(
            openCl: StubBackend(DeviceType.OpenCL),
            cuda: StubBackend(DeviceType.Cuda));

        sut.SelectBestGpu().Device.Should().Be(DeviceType.OpenCL);
    }

    [Fact]
    public void SelectBestGpu_FallsToCuda_WhenNoOpenCl()
    {
        var sut = CreateSelector(openCl: null, cuda: StubBackend(DeviceType.Cuda));

        sut.SelectBestGpu().Device.Should().Be(DeviceType.Cuda);
    }

    [Fact]
    public void SelectBestGpu_FallsToCpu_WhenNoGpu()
    {
        var sut = CreateSelector(openCl: null, cuda: null);

        sut.SelectBestGpu().Device.Should().Be(DeviceType.Cpu);
    }

    [Fact]
    public void SelectBackend_DefaultPreference_ReturnsCpu()
    {
        var sut = CreateSelector(
            openCl: StubBackend(DeviceType.OpenCL),
            cuda: StubBackend(DeviceType.Cuda));

        sut.SelectBackend().Device.Should().Be(DeviceType.Cpu);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static GpuAwareBackendSelector CreateSelector(
        ITensorBackend? openCl, ITensorBackend? cuda)
        => new(CpuTensorBackend.Instance, openCl, cuda);

    private static ITensorBackend StubBackend(DeviceType device)
    {
        var stub = Substitute.For<ITensorBackend>();
        stub.Device.Returns(device);
        return stub;
    }
}
