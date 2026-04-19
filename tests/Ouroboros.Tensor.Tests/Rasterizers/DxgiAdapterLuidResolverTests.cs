// <copyright file="DxgiAdapterLuidResolverTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Tensor.Configuration;
using Ouroboros.Tensor.Rasterizers;

namespace Ouroboros.Tensor.Tests.Rasterizers;

/// <summary>
/// Phase 196.3-01 — DxgiAdapterLuidResolver maps the packed LUID from
/// <see cref="AdapterInfo.AdapterLuid"/> back to the ordinal index ORT's
/// DML EP will see when it calls <c>EnumAdapters1</c> internally. The
/// resolver is the single seam that guarantees ORT's implicit
/// <c>D3D12CreateDevice(deviceId)</c> call lands on the same adapter LUID
/// that <c>SharedD3D12Device</c> attached to — so D3D12's per-adapter
/// singleton guarantee yields one process-wide device.
/// </summary>
public class DxgiAdapterLuidResolverTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void ResolveDmlDeviceIdForLuid_StubEnumerator_ReturnsMatchingOrdinal()
    {
        AdapterInfo[] adapters =
        [
            new(VendorId: 0x1002u, DeviceId: 0x7480u, Description: "AMD 0", DedicatedVramBytes: 0, IsSoftware: false, AdapterLuid: 0x0000_0001_0000_0001UL),
            new(VendorId: 0x10DEu, DeviceId: 0x2204u, Description: "NVIDIA 1", DedicatedVramBytes: 0, IsSoftware: false, AdapterLuid: 0x0000_0002_0000_0005UL),
            new(VendorId: 0x8086u, DeviceId: 0x0046u, Description: "Intel WARP", DedicatedVramBytes: 0, IsSoftware: true, AdapterLuid: 0x0000_0000_0000_00FFUL),
        ];
        var resolver = new DxgiAdapterLuidResolver(new StubEnumerator(adapters));

        int ordinal = resolver.ResolveDmlDeviceIdForLuid(0x0000_0002_0000_0005UL);

        ordinal.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ResolveDmlDeviceIdForLuid_StubEnumerator_CachesFirstResolution()
    {
        var stub = new StubEnumerator(
        [
            new(VendorId: 0x1002u, DeviceId: 0x0u, Description: "AMD 0", DedicatedVramBytes: 0, IsSoftware: false, AdapterLuid: 0xAAAAUL),
        ]);
        var resolver = new DxgiAdapterLuidResolver(stub);

        int first = resolver.ResolveDmlDeviceIdForLuid(0xAAAAUL);
        int second = resolver.ResolveDmlDeviceIdForLuid(0xAAAAUL);

        first.Should().Be(0);
        second.Should().Be(0);
        stub.EnumerateCallCount.Should().Be(
            1,
            "resolver must cache the first successful resolution for process lifetime");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ResolveDmlDeviceIdForLuid_Miss_ThrowsWithAdapterDiagnostics()
    {
        AdapterInfo[] adapters =
        [
            new(VendorId: 0x1002u, DeviceId: 0u, Description: "AMD Card", DedicatedVramBytes: 0, IsSoftware: false, AdapterLuid: 0x1111UL),
            new(VendorId: 0x10DEu, DeviceId: 0u, Description: "NVIDIA Card", DedicatedVramBytes: 0, IsSoftware: false, AdapterLuid: 0x2222UL),
        ];
        var resolver = new DxgiAdapterLuidResolver(new StubEnumerator(adapters));

        Action act = () => resolver.ResolveDmlDeviceIdForLuid(0xDEADBEEFUL);

        act.Should()
           .Throw<InvalidOperationException>()
           .Where(ex =>
               ex.Message.Contains("DEADBEEF", StringComparison.OrdinalIgnoreCase)
               && ex.Message.Contains("AMD Card", StringComparison.Ordinal)
               && ex.Message.Contains("NVIDIA Card", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ResolveDmlDeviceIdForLuid_EmptyAdapterList_ThrowsInvalidOperation()
    {
        var resolver = new DxgiAdapterLuidResolver(new StubEnumerator([]));

        Action act = () => resolver.ResolveDmlDeviceIdForLuid(0x1UL);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    [Trait("Category", "GPU")]
    public void ResolveDmlDeviceIdForLuid_LiveDxgi_ReturnsNonNegativeOrdinalForFirstAdapter()
    {
        string? env = Environment.GetEnvironmentVariable("GSD_GPU_AVAILABLE");
        if (string.Equals(env, "false", StringComparison.OrdinalIgnoreCase))
        {
            Assert.True(true, "GSD_GPU_AVAILABLE=false — skipped");
            return;
        }
        if (!OperatingSystem.IsWindows())
        {
            Assert.True(true, "live DXGI requires Windows — skipped");
            return;
        }

        var enumerator = new DxgiAdapterEnumerator();
        IReadOnlyList<AdapterInfo> adapters = enumerator.EnumerateAdapters();
        if (adapters.Count == 0)
        {
            Assert.True(true, "no DXGI adapters present — skipped");
            return;
        }

        AdapterInfo first = adapters[0];
        var resolver = new DxgiAdapterLuidResolver(enumerator);

        int ordinal = resolver.ResolveDmlDeviceIdForLuid(first.AdapterLuid);

        ordinal.Should().Be(0);
    }

    private sealed class StubEnumerator : IDxgiAdapterEnumerator
    {
        private readonly IReadOnlyList<AdapterInfo> _adapters;

        public StubEnumerator(IReadOnlyList<AdapterInfo> adapters)
        {
            _adapters = adapters;
        }

        public int EnumerateCallCount { get; private set; }

        public IReadOnlyList<AdapterInfo> EnumerateAdapters()
        {
            EnumerateCallCount++;
            return _adapters;
        }
    }
}
