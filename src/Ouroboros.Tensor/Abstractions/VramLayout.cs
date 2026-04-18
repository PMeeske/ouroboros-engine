// <copyright file="VramLayout.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Abstractions;

/// <summary>
/// Default <see cref="IVramLayout"/> implementation — an immutable value
/// carrying the preset id, resolved adapter description, packed LUID, total
/// dedicated-VRAM byte count, and the <see cref="VramBucket"/> →
/// <see cref="VramBucketBudget"/> map.
/// </summary>
/// <param name="Id">Stable preset id (e.g. <c>RX9060XT_16GB</c>).</param>
/// <param name="AdapterDescription">Driver-reported adapter description, or <c>override:{Id}</c> for forced presets.</param>
/// <param name="AdapterLuid">Packed DXGI adapter LUID; <c>0UL</c> for in-code presets.</param>
/// <param name="TotalDeviceBytes">Total dedicated VRAM in bytes.</param>
/// <param name="Buckets">Named-bucket map — must be non-null.</param>
public sealed record VramLayout(
    string Id,
    string AdapterDescription,
    ulong AdapterLuid,
    long TotalDeviceBytes,
    IReadOnlyDictionary<VramBucket, VramBucketBudget> Buckets) : IVramLayout
{
    /// <summary>
    /// Returns a clone with <see cref="IVramLayout.AdapterDescription"/> and
    /// <see cref="IVramLayout.AdapterLuid"/> replaced — used by
    /// <c>DxgiVramLayoutProvider</c> to rehydrate an in-code preset with the
    /// live adapter metadata it just enumerated.
    /// </summary>
    /// <param name="adapterDescription">Driver-reported adapter description.</param>
    /// <param name="adapterLuid">Packed DXGI adapter LUID.</param>
    /// <returns>A new <see cref="VramLayout"/> with the updated fields.</returns>
    public VramLayout WithAdapter(string adapterDescription, ulong adapterLuid) =>
        this with { AdapterDescription = adapterDescription, AdapterLuid = adapterLuid };
}
