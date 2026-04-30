// <copyright file="DxgiVramLayoutProviderTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Ouroboros.Tensor.Abstractions;
using Ouroboros.Tensor.Configuration;

namespace Ouroboros.Tests.Configuration;

/// <summary>
/// Fallback-chain + override + LUID-roundtrip coverage for
/// <see cref="DxgiVramLayoutProvider"/>. Uses a fake enumerator so the test
/// suite never hits the real DXGI layer.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DxgiVramLayoutProviderTests
{
    private const uint AmdVendorId = 0x1002u;
    private const uint NvidiaVendorId = 0x10DEu;
    private const uint IntelVendorId = 0x8086u;
    private const long OneGib = 1L * 1024 * 1024 * 1024;

    [Fact]
    public void Resolve_WithOverrideToRX9060XT_ReturnsRX9060XT_EvenWhenAdapterIsIntel()
    {
        var enumerator = new FakeEnumerator(new AdapterInfo(
            VendorId: IntelVendorId,
            DeviceId: 0x1234u,
            Description: "Intel Arc A770",
            DedicatedVramBytes: 16L * OneGib,
            IsSoftware: false,
            AdapterLuid: 0xAAAA_BBBB_CCCC_DDDDUL));
        var provider = new DxgiVramLayoutProvider(enumerator);
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [DxgiVramLayoutProvider.OverrideConfigKey] = VramLayoutPresets.RX9060XT16GBId,
            })
            .Build();

        IVramLayout layout = provider.Resolve(config);

        layout.Id.Should().Be(VramLayoutPresets.RX9060XT16GBId);
        layout.AdapterDescription.Should().Be($"override:{VramLayoutPresets.RX9060XT16GBId}");
        // Override returns in-code preset (no LUID rehydration from the fake adapter).
        layout.AdapterLuid.Should().Be(0UL);
    }

    [Fact]
    public void Resolve_DeviceNameMatch_RX9060XT_ReturnsRX9060XTPreset()
    {
        var enumerator = new FakeEnumerator(new AdapterInfo(
            VendorId: AmdVendorId,
            DeviceId: 0x7480u,
            Description: "AMD Radeon RX 9060 XT",
            DedicatedVramBytes: 16L * OneGib,
            IsSoftware: false,
            AdapterLuid: 0x1111_2222_3333_4444UL));
        var provider = new DxgiVramLayoutProvider(enumerator);

        IVramLayout layout = provider.Resolve(EmptyConfig());

        layout.Id.Should().Be(VramLayoutPresets.RX9060XT16GBId);
        layout.AdapterDescription.Should().Contain("RX 9060 XT");
    }

    [Fact]
    public void Resolve_VendorAndVramMatch_AmdSixteenGib_ReturnsRX9060XT_EvenWithUnknownDevice()
    {
        var enumerator = new FakeEnumerator(new AdapterInfo(
            VendorId: AmdVendorId,
            DeviceId: 0x7400u,
            Description: "Radeon RX 7800", // NOT in DeviceNameKeys
            DedicatedVramBytes: 16L * OneGib,
            IsSoftware: false,
            AdapterLuid: 0xDEADBEEFUL));
        var provider = new DxgiVramLayoutProvider(enumerator);

        IVramLayout layout = provider.Resolve(EmptyConfig());

        layout.Id.Should().Be(VramLayoutPresets.RX9060XT16GBId);
    }

    [Fact]
    public void Resolve_GenericByVram_Nvidia24Gib_ReturnsGeneric24GBPlus()
    {
        var enumerator = new FakeEnumerator(new AdapterInfo(
            VendorId: NvidiaVendorId,
            DeviceId: 0x2684u,
            Description: "NVIDIA GeForce RTX 4090",
            DedicatedVramBytes: 24L * OneGib,
            IsSoftware: false,
            AdapterLuid: 0xCAFE_BABE_1234_5678UL));
        var provider = new DxgiVramLayoutProvider(enumerator);

        IVramLayout layout = provider.Resolve(EmptyConfig());

        layout.Id.Should().Be(VramLayoutPresets.Generic24GBPlusId);
    }

    [Fact]
    public void Resolve_NoAdaptersAvailable_FallsBackToGeneric8GB()
    {
        var enumerator = new FakeEnumerator(/* no adapters */);
        var provider = new DxgiVramLayoutProvider(enumerator);

        IVramLayout layout = provider.Resolve(EmptyConfig());

        layout.Id.Should().Be(VramLayoutPresets.Generic8GBId);
        layout.AdapterDescription.Should().Be("fallback:no-adapter");
    }

    [Fact]
    public void Resolve_SoftwareAdapter_IsSkipped_FallsToNextOrGeneric()
    {
        var enumerator = new FakeEnumerator(
            new AdapterInfo(
                VendorId: 0x1414u, // Microsoft WARP
                DeviceId: 0x008Cu,
                Description: "Microsoft Basic Render Driver",
                DedicatedVramBytes: 0L,
                IsSoftware: true,
                AdapterLuid: 0x0101_0101_0101_0101UL));
        var provider = new DxgiVramLayoutProvider(enumerator);

        IVramLayout layout = provider.Resolve(EmptyConfig());

        // Only adapter was software — falls through to the no-adapter default.
        layout.Id.Should().Be(VramLayoutPresets.Generic8GBId);
        layout.AdapterDescription.Should().Be("fallback:no-adapter");
    }

    [Fact]
    public void Resolve_SoftwareAdapterFollowedByReal_PicksTheRealOne()
    {
        var enumerator = new FakeEnumerator(
            new AdapterInfo(0x1414u, 0x008Cu, "Microsoft Basic Render Driver", 0L, true, 0x01UL),
            new AdapterInfo(NvidiaVendorId, 0x2684u, "NVIDIA GeForce RTX 4090", 24L * OneGib, false, 0xCAFEUL));
        var provider = new DxgiVramLayoutProvider(enumerator);

        IVramLayout layout = provider.Resolve(EmptyConfig());

        layout.Id.Should().Be(VramLayoutPresets.Generic24GBPlusId);
        layout.AdapterLuid.Should().Be(0xCAFEUL);
    }

    [Fact]
    public void Resolve_AdapterLuid_RoundTripsThroughResolvedLayout_N03b()
    {
        // Plan 188.1-03 needs IVramLayout.AdapterLuid to carry the real packed LUID
        // so SharedD3D12Device can reattach to the same IDXGIAdapter1 without a second
        // enumeration (N-03-b in the PLAN revision-2 notes).
        const ulong expectedLuid = 0xDEADBEEF_CAFEBABEUL;

        var enumerator = new FakeEnumerator(new AdapterInfo(
            VendorId: AmdVendorId,
            DeviceId: 0x7480u,
            Description: "Radeon RX 9060 XT",
            DedicatedVramBytes: 16L * OneGib,
            IsSoftware: false,
            AdapterLuid: expectedLuid));
        var provider = new DxgiVramLayoutProvider(enumerator);

        IVramLayout layout = provider.Resolve(EmptyConfig());

        layout.AdapterLuid.Should().Be(expectedLuid,
            because: "plan 03's SharedD3D12Device reads AdapterLuid to reattach to the same adapter");
    }

    [Fact]
    public void Resolve_UnknownOverrideId_FallsThroughToAutoDetect()
    {
        var enumerator = new FakeEnumerator(new AdapterInfo(
            VendorId: NvidiaVendorId, DeviceId: 0x2684u,
            Description: "NVIDIA GeForce RTX 4090",
            DedicatedVramBytes: 24L * OneGib,
            IsSoftware: false,
            AdapterLuid: 0xFEED_FACEUL));
        var provider = new DxgiVramLayoutProvider(enumerator);
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [DxgiVramLayoutProvider.OverrideConfigKey] = "NotARealPresetId",
            })
            .Build();

        IVramLayout layout = provider.Resolve(config);

        layout.Id.Should().Be(VramLayoutPresets.Generic24GBPlusId);
    }

    private static IConfiguration EmptyConfig() => new ConfigurationBuilder().Build();

    private sealed class FakeEnumerator : IDxgiAdapterEnumerator
    {
        private readonly AdapterInfo[] _adapters;

        public FakeEnumerator(params AdapterInfo[] adapters)
        {
            _adapters = adapters;
        }

        public IReadOnlyList<AdapterInfo> EnumerateAdapters() => _adapters;
    }
}
