// <copyright file="DxgiVramLayoutProvider.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.Configuration;
using Ouroboros.Tensor.Abstractions;
using Silk.NET.Core.Native;
using Silk.NET.DXGI;

namespace Ouroboros.Tensor.Configuration;

/// <summary>
/// Describes one adapter enumerated by DXGI. The provider consumes this
/// through the <see cref="IDxgiAdapterEnumerator"/> seam so tests can feed a
/// deterministic adapter list without hitting the real GPU.
/// </summary>
/// <param name="VendorId">PCI vendor id (e.g. <c>0x1002</c> for AMD, <c>0x10DE</c> for NVIDIA, <c>0x8086</c> for Intel).</param>
/// <param name="DeviceId">PCI device id.</param>
/// <param name="Description">Driver-reported adapter description string (may be arbitrary unicode).</param>
/// <param name="DedicatedVramBytes">Dedicated VRAM in bytes (<c>DedicatedVideoMemory</c>).</param>
/// <param name="IsSoftware">True when the adapter is WARP / software-rendering.</param>
/// <param name="AdapterLuid">Packed LUID — <c>(ulong)LowPart | ((ulong)(uint)HighPart &lt;&lt; 32)</c>.</param>
public sealed record AdapterInfo(
    uint VendorId,
    uint DeviceId,
    string Description,
    long DedicatedVramBytes,
    bool IsSoftware,
    ulong AdapterLuid);

/// <summary>
/// Seam over DXGI adapter enumeration. The real implementation,
/// <see cref="DxgiAdapterEnumerator"/>, uses Silk.NET.DXGI. Tests substitute
/// an in-memory list via <see cref="DxgiVramLayoutProvider"/>'s ctor.
/// </summary>
public interface IDxgiAdapterEnumerator
{
    /// <summary>
    /// Enumerates adapters in DXGI order (primary first). MUST NOT throw —
    /// on failure return an empty list so the provider can degrade to
    /// <see cref="VramLayoutPresets.Generic_8GB"/>.
    /// </summary>
    /// <returns>Adapters seen by DXGI, or an empty list on failure.</returns>
    IReadOnlyList<AdapterInfo> EnumerateAdapters();
}

/// <summary>
/// Default <see cref="IDxgiAdapterEnumerator"/> — wraps Silk.NET.DXGI
/// (<c>CreateDXGIFactory1</c> → <c>EnumAdapters1</c> → <c>GetDesc1</c>).
/// Any native failure is swallowed and logged as an empty enumeration so
/// host startup never crashes on a DXGI hiccup (Phase 188.1 T-01-02).
/// </summary>
public sealed class DxgiAdapterEnumerator : IDxgiAdapterEnumerator
{
    private readonly ILogger<DxgiAdapterEnumerator>? _logger;

    /// <summary>Initializes a new instance of the <see cref="DxgiAdapterEnumerator"/> class.Constructs the enumerator with an optional logger for DXGI failures.</summary>
    /// <param name="logger">Logger for native-layer diagnostics.</param>
    public DxgiAdapterEnumerator(ILogger<DxgiAdapterEnumerator>? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design", "CA1031:Do not catch general exception types",
        Justification = "Native DXGI errors must degrade to an empty list — host startup must not fail on GPU enumeration.")]
    public unsafe IReadOnlyList<AdapterInfo> EnumerateAdapters()
    {
        var list = new List<AdapterInfo>();

        DXGI? dxgi = null;
        ComPtr<IDXGIFactory1> factory = default;

        try
        {
            dxgi = DXGI.GetApi();
            int hr = dxgi.CreateDXGIFactory1(out factory);
            if (hr < 0 || factory.Handle == null)
            {
                _logger?.LogWarning("[DxgiAdapterEnumerator] CreateDXGIFactory1 failed (hr=0x{Hr:X8}) — returning empty adapter list", hr);
                return list;
            }

            uint i = 0;
            while (true)
            {
                var adapter = default(ComPtr<IDXGIAdapter1>);
                int enumHr = factory.EnumAdapters1(i, ref adapter);
                if (enumHr < 0)
                {
                    break; // DXGI_ERROR_NOT_FOUND terminates the loop
                }

                try
                {
                    var desc = default(AdapterDesc1);
                    adapter.GetDesc1(ref desc);

                    string description = new string(desc.Description);

                    // Trim embedded NULs that Silk.NET leaves in the char buffer.
                    int nul = description.IndexOf('\0');
                    if (nul >= 0)
                    {
                        description = description.Substring(0, nul);
                    }

                    // Pack LUID into a ulong. High part reinterpreted as uint first to
                    // preserve bit pattern for negative values.
                    ulong packedLuid = (ulong)desc.AdapterLuid.Low
                                     | ((ulong)(uint)desc.AdapterLuid.High << 32);

                    // DXGI_ADAPTER_FLAG_SOFTWARE == 2; use bitmask rather than enum to avoid
                    // version-skew in Silk.NET enum naming.
                    bool isSoftware = (desc.Flags & 0x2u) != 0u;

                    list.Add(new AdapterInfo(
                        VendorId: desc.VendorId,
                        DeviceId: desc.DeviceId,
                        Description: description,
                        DedicatedVramBytes: (long)desc.DedicatedVideoMemory,
                        IsSoftware: isSoftware,
                        AdapterLuid: packedLuid));
                }
                finally
                {
                    adapter.Dispose();
                }

                i++;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[DxgiAdapterEnumerator] DXGI enumeration threw — returning {Count} partial adapters", list.Count);
        }
        finally
        {
            factory.Dispose();
            dxgi?.Dispose();
        }

        return list;
    }
}

/// <summary>
/// DXGI-backed <see cref="IVramLayoutProvider"/>. Resolves the active
/// adapter and maps it to a <see cref="VramLayoutPresets"/> preset via the
/// <c>DeviceNameMatch → VendorAndVramMatch → GenericByVram</c> fallback
/// chain. <c>Avatar:VramLayoutOverride</c> short-circuits the chain when set
/// to a known preset id.
/// </summary>
public sealed class DxgiVramLayoutProvider : IVramLayoutProvider
{
    /// <summary>Configuration key that forces a specific preset id.</summary>
    public const string OverrideConfigKey = "Avatar:VramLayoutOverride";

    // AMD vendor id for the 16 GB-class VendorAndVramMatch heuristic.
    private const uint AmdVendorId = 0x1002u;

    // 16 GB-class match window: 14–18 GiB of dedicated VRAM. Loose upper bound
    // accommodates overclocked/driver-reported variance.
    private static readonly long FourteenGib = 14L * 1024 * 1024 * 1024;
    private static readonly long EighteenGib = 18L * 1024 * 1024 * 1024;

    // GenericByVram thresholds.
    private static readonly long TwelveGib = 12L * 1024 * 1024 * 1024;
    private static readonly long TwentyGib = 20L * 1024 * 1024 * 1024;

    private static readonly string[] DeviceNameKeys = { "Radeon RX 9060 XT", "RX 9060 XT" };

    private readonly IDxgiAdapterEnumerator _enumerator;
    private readonly ILogger<DxgiVramLayoutProvider>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DxgiVramLayoutProvider"/> class.
    /// Constructs the provider with an optional logger. The default
    /// <see cref="DxgiAdapterEnumerator"/> performs real DXGI queries; tests
    /// inject a stub via the other overload.
    /// </summary>
    /// <param name="logger">Optional logger for preset-resolution diagnostics.</param>
    public DxgiVramLayoutProvider(ILogger<DxgiVramLayoutProvider>? logger = null)
        : this(new DxgiAdapterEnumerator(null), logger)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DxgiVramLayoutProvider"/> class.
    /// Constructs the provider with an explicit enumerator (test seam) and
    /// optional logger.
    /// </summary>
    /// <param name="enumerator">Adapter enumeration seam.</param>
    /// <param name="logger">Optional logger for preset-resolution diagnostics.</param>
    public DxgiVramLayoutProvider(
        IDxgiAdapterEnumerator enumerator,
        ILogger<DxgiVramLayoutProvider>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(enumerator);
        _enumerator = enumerator;
        _logger = logger;
    }

    /// <inheritdoc/>
    public IVramLayout Resolve(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // 1. Config override wins unconditionally (when it names a known preset).
        string? overrideId = configuration[OverrideConfigKey];
        if (!string.IsNullOrWhiteSpace(overrideId))
        {
            VramLayout? overridden = VramLayoutPresets.TryGet(overrideId);
            if (overridden is not null)
            {
                VramLayout withAdapter = overridden.WithAdapter($"override:{overridden.Id}", 0UL);
                _logger?.LogInformation(
                    "[VramLayout] {Key}='{OverrideId}' forced preset {PresetId} (adapter enumeration skipped)",
                    OverrideConfigKey, overrideId, overridden.Id);
                return withAdapter;
            }

            _logger?.LogWarning(
                "[VramLayout] {Key}='{OverrideId}' does not match a known preset id — falling through to auto-detect",
                OverrideConfigKey, overrideId);
        }

        // 2. Enumerate adapters via the seam; pick the first non-software adapter.
        IReadOnlyList<AdapterInfo> adapters = _enumerator.EnumerateAdapters();
        AdapterInfo? primary = null;
        foreach (AdapterInfo adapter in adapters)
        {
            if (!adapter.IsSoftware)
            {
                primary = adapter;
                break;
            }
        }

        if (primary is null)
        {
            _logger?.LogWarning(
                "[VramLayout] No non-software DXGI adapter found — falling back to {PresetId}",
                VramLayoutPresets.Generic_8GB.Id);
            return VramLayoutPresets.Generic_8GB.WithAdapter("fallback:no-adapter", 0UL);
        }

        VramLayout resolved = ResolvePreset(primary);
        VramLayout withLuid = resolved.WithAdapter(primary.Description, primary.AdapterLuid);
        _logger?.LogInformation(
            "[VramLayout] Resolved preset {PresetId} for adapter '{Description}' (vendor=0x{VendorId:X4} dedicated={VramMB}MB luid=0x{Luid:X16})",
            withLuid.Id, primary.Description, primary.VendorId,
            primary.DedicatedVramBytes / (1024 * 1024), primary.AdapterLuid);
        return withLuid;
    }

    private static VramLayout ResolvePreset(AdapterInfo adapter)
    {
        // 3. DeviceNameMatch — case-insensitive substring against known device keys.
        foreach (string key in DeviceNameKeys)
        {
            if (adapter.Description.Contains(key, StringComparison.OrdinalIgnoreCase))
            {
                return VramLayoutPresets.RX9060XT_16GB;
            }
        }

        // 4. VendorAndVramMatch — AMD + 16 GB class → RX9060XT_16GB.
        if (adapter.VendorId == AmdVendorId
            && adapter.DedicatedVramBytes >= FourteenGib
            && adapter.DedicatedVramBytes <= EighteenGib)
        {
            return VramLayoutPresets.RX9060XT_16GB;
        }

        // 5. GenericByVram — bucket by VRAM size.
        if (adapter.DedicatedVramBytes < TwelveGib)
        {
            return VramLayoutPresets.Generic_8GB;
        }

        if (adapter.DedicatedVramBytes < TwentyGib)
        {
            return VramLayoutPresets.RX9060XT_16GB;
        }

        return VramLayoutPresets.Generic_24GB_Plus;
    }
}
