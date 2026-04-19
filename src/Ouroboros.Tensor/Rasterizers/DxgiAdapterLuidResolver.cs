// <copyright file="DxgiAdapterLuidResolver.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Ouroboros.Tensor.Configuration;

namespace Ouroboros.Tensor.Rasterizers;

/// <summary>
/// Maps a packed <see cref="AdapterInfo.AdapterLuid"/> to the zero-based
/// DXGI ordinal that ORT's DirectML EP will pass into
/// <c>D3D12CreateDevice(deviceId)</c>. Reuses the existing
/// <see cref="IDxgiAdapterEnumerator"/> seam so tests can feed a deterministic
/// adapter list and production walks <c>IDXGIFactory1::EnumAdapters1</c> in
/// the same order ORT's <c>CreateD3D12Device(device_id)</c> does.
/// </summary>
/// <remarks>
/// <para>
/// Phase 196.3-01 (COORD-DEV-01). The resolver is the load-bearing hop
/// that makes the "one process-wide <c>ID3D12Device</c>" exit criterion
/// achievable without P/Invoke — if ORT's internal <c>EnumAdapters1</c>
/// walk lands on the same LUID <see cref="SharedD3D12Device"/> attached
/// to, D3D12's per-adapter singleton contract (see
/// <a href="https://learn.microsoft.com/en-us/windows/win32/api/d3d12/nf-d3d12-d3d12createdevice">
/// D3D12CreateDevice docs</a>) returns the same <c>ID3D12Device</c> object.
/// </para>
/// <para>
/// Successful resolutions are cached per LUID for the process lifetime —
/// the enumerator is queried only on the first miss against a previously
/// unseen LUID, not on every <c>SessionOptions</c> build.
/// </para>
/// </remarks>
public sealed class DxgiAdapterLuidResolver
{
    private readonly IDxgiAdapterEnumerator _enumerator;
    private readonly ConcurrentDictionary<ulong, int> _cache = new();

    /// <summary>
    /// Production ctor — walks real DXGI via
    /// <see cref="DxgiAdapterEnumerator"/>.
    /// </summary>
    public DxgiAdapterLuidResolver()
        : this(new DxgiAdapterEnumerator(null))
    {
    }

    /// <summary>
    /// Test-seam ctor — accepts any <see cref="IDxgiAdapterEnumerator"/>
    /// so unit tests can drive deterministic adapter lists.
    /// </summary>
    /// <param name="enumerator">Adapter enumeration seam.</param>
    public DxgiAdapterLuidResolver(IDxgiAdapterEnumerator enumerator)
    {
        ArgumentNullException.ThrowIfNull(enumerator);
        _enumerator = enumerator;
    }

    /// <summary>
    /// Returns the zero-based <c>EnumAdapters1</c> ordinal whose
    /// packed LUID equals <paramref name="luid"/>. Caches the first
    /// successful resolution for process lifetime.
    /// </summary>
    /// <param name="luid">
    /// Packed LUID in the same shape <see cref="AdapterInfo.AdapterLuid"/>
    /// produces — <c>(ulong)low | ((ulong)(uint)high &lt;&lt; 32)</c>.
    /// </param>
    /// <returns>DXGI ordinal (deviceId for <c>AppendExecutionProvider_DML</c>).</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no enumerated adapter matches <paramref name="luid"/>.
    /// Message lists all enumerated ordinals + LUIDs + descriptions to
    /// make misconfiguration diagnosable.
    /// </exception>
    public int ResolveDmlDeviceIdForLuid(ulong luid)
    {
        if (_cache.TryGetValue(luid, out int cached))
        {
            return cached;
        }

        IReadOnlyList<AdapterInfo> adapters = _enumerator.EnumerateAdapters();
        for (int i = 0; i < adapters.Count; i++)
        {
            if (adapters[i].AdapterLuid == luid)
            {
                _cache[luid] = i;
                return i;
            }
        }

        throw new InvalidOperationException(BuildMissDiagnostic(luid, adapters));
    }

    private static string BuildMissDiagnostic(ulong luid, IReadOnlyList<AdapterInfo> adapters)
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"No DXGI adapter found with LUID 0x{luid:X16}. ");
        if (adapters.Count == 0)
        {
            sb.Append("Available adapters: <none> (IDxgiAdapterEnumerator returned an empty list).");
            return sb.ToString();
        }

        sb.Append("Available adapters:");
        for (int i = 0; i < adapters.Count; i++)
        {
            AdapterInfo a = adapters[i];
            sb.Append(CultureInfo.InvariantCulture,
                $" [{i}] LUID=0x{a.AdapterLuid:X16} vendor=0x{a.VendorId:X4} '{a.Description}'");
            if (i < adapters.Count - 1)
            {
                sb.Append(';');
            }
        }
        sb.Append('.');
        return sb.ToString();
    }
}
