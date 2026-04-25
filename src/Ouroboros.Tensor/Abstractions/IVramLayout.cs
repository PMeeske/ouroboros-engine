// <copyright file="IVramLayout.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Abstractions;

/// <summary>
/// Immutable per-session VRAM partition for the host GPU. Resolved once at
/// startup by an <see cref="IVramLayoutProvider"/> (auto-detect or
/// <c>Avatar:VramLayoutOverride</c> config forced preset) and injected into
/// allocation-tracking consumers such as <c>VramBudgetMonitor</c>.
/// </summary>
/// <remarks>
/// <para>
/// Introduced in Phase 188.1 (AVA-07) to replace <c>VramBudgetMonitor</c>'s
/// hard-coded RX 9060 XT <c>init</c> properties with a named-bucket map. The
/// abstraction lives in <see cref="Ouroboros.Tensor.Abstractions"/> so both
/// the Tensor-layer rasterizer and App-layer orchestrators can share a
/// single layout instance.
/// </para>
/// <para>
/// <see cref="AdapterLuid"/> is populated from <c>DXGI_ADAPTER_DESC1.AdapterLuid</c>
/// so Plan 188.1-03's <c>SharedD3D12Device</c> can reattach to the same
/// <c>IDXGIAdapter1</c> without a second DXGI enumeration pass (N-03-b).
/// Presets created from the in-code registry use <c>0UL</c> as a sentinel;
/// only <c>DxgiVramLayoutProvider</c> produces real LUIDs.
/// </para>
/// </remarks>
public interface IVramLayout
{
    /// <summary>Gets preset id — e.g. <c>RX9060XT_16GB</c>, <c>Generic_8GB</c>, <c>Generic_24GB_Plus</c>.</summary>
    string Id { get; }

    /// <summary>
    /// Gets human-readable description of the resolved adapter, or
    /// <c>"override:{Id}"</c> when <c>Avatar:VramLayoutOverride</c> forced the preset.
    /// </summary>
    string AdapterDescription { get; }

    /// <summary>
    /// Gets packed LUID of the <c>IDXGIAdapter1</c> this layout was resolved for,
    /// computed as <c>(ulong)(LowPart) | ((ulong)HighPart &lt;&lt; 32)</c>.
    /// <c>0UL</c> for in-code presets that did not come from DXGI enumeration.
    /// </summary>
    ulong AdapterLuid { get; }

    /// <summary>Gets total dedicated VRAM on the adapter in bytes.</summary>
    long TotalDeviceBytes { get; }

    /// <summary>Gets named-bucket map (<see cref="VramBucket"/> → <see cref="VramBucketBudget"/>).</summary>
    IReadOnlyDictionary<VramBucket, VramBucketBudget> Buckets { get; }
}
