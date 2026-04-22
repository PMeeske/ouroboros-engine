// <copyright file="VramLayoutPresets.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Configuration;

/// <summary>
/// In-code registry of built-in <see cref="VramLayout"/> presets.
/// <see cref="DxgiVramLayoutProvider"/> resolves one of these after adapter
/// auto-detect and rehydrates the adapter metadata via
/// <see cref="VramLayout.WithAdapter"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Byte-identity regression guard (Phase 188.1-01):</b> the
/// <see cref="RX9060XT_16GB"/> preset preserves the legacy
/// <c>VramBudgetMonitor</c> defaults — <c>TtsLlama = 2 GiB</c>,
/// <c>Avatar.Minimum = 4 GiB</c>, <c>Training = 6 GiB</c>,
/// <c>TotalDeviceBytes = 16 GiB</c> — byte-identical. The Rasterizer bucket
/// (NEW in this phase) claims <c>128 MiB</c> from what was previously
/// opaque headroom; the remaining <c>3 GiB + 896 MiB</c> stays as
/// <see cref="VramBucket.Headroom"/> so the total still sums to 16 GiB.
/// </para>
/// <para>
/// All <see cref="IVramLayout.AdapterLuid"/> values are the <c>0UL</c>
/// sentinel on in-code presets — only
/// <c>DxgiVramLayoutProvider</c> populates real packed LUIDs.
/// </para>
/// </remarks>
public static class VramLayoutPresets
{
    private const long OneGib = 1L * 1024 * 1024 * 1024;
    private const long OneMib = 1L * 1024 * 1024;

    /// <summary>Preset id for the RX 9060 XT 16 GB developer-box layout.</summary>
    public const string RX9060XT_16GB_Id = "RX9060XT_16GB";

    /// <summary>Preset id for the conservative 8 GB layout used as the DXGI fallback default.</summary>
    public const string Generic_8GB_Id = "Generic_8GB";

    /// <summary>Preset id for the roomy 24 GB-class layout (RTX 4090, RX 7900 XTX, etc.).</summary>
    public const string Generic_24GB_Plus_Id = "Generic_24GB_Plus";

    /// <summary>
    /// RX 9060 XT 16 GB preset — byte-identical to legacy
    /// <c>VramBudgetMonitor</c> defaults for TTS / Avatar / Training / Total;
    /// Rasterizer = 128 MiB (Minimum = 64 MiB) carved out of the legacy
    /// 4 GiB headroom. External-tenant buckets (LlmInference=512 MiB,
    /// Embed=128 MiB, Stt=128 MiB) are carved from headroom, leaving
    /// 3 GiB + 128 MiB as <see cref="VramBucket.Headroom"/>.
    /// </summary>
    public static VramLayout RX9060XT_16GB { get; } = new(
        Id: RX9060XT_16GB_Id,
        AdapterDescription: RX9060XT_16GB_Id,
        AdapterLuid: 0UL,
        TotalDeviceBytes: 16L * OneGib,
        Buckets: new Dictionary<VramBucket, VramBucketBudget>
        {
            [VramBucket.TtsLlama] = new(Minimum: 2L * OneGib, Budget: 2L * OneGib, Priority: 60),
            [VramBucket.TtsDac] = new(Minimum: 0L, Budget: 0L, Priority: 0),
            [VramBucket.Avatar] = new(Minimum: 4L * OneGib, Budget: 4L * OneGib, Priority: 80),
            [VramBucket.Rasterizer] = new(Minimum: 64L * OneMib, Budget: 128L * OneMib, Priority: 90),
            [VramBucket.Training] = new(Minimum: 0L, Budget: 6L * OneGib, Priority: 20),
            [VramBucket.LlmInference] = new(Minimum: 0L, Budget: 512L * OneMib, Priority: 30),
            [VramBucket.Embed] = new(Minimum: 0L, Budget: 128L * OneMib, Priority: 10),
            [VramBucket.Stt] = new(Minimum: 0L, Budget: 128L * OneMib, Priority: 10),
            [VramBucket.Headroom] = new(Minimum: 0L, Budget: (3L * OneGib) + (128L * OneMib), Priority: 0),
        });

    /// <summary>
    /// Generic 8 GB-class preset (DXGI fallback default). Splits:
    /// TTS 1 GiB / Avatar 2 GiB / Rasterizer 128 MiB / Training 2 GiB /
    /// LlmInference 256 MiB / Embed 64 MiB / Stt 64 MiB /
    /// Headroom 2 GiB + 512 MiB.
    /// </summary>
    public static VramLayout Generic_8GB { get; } = new(
        Id: Generic_8GB_Id,
        AdapterDescription: Generic_8GB_Id,
        AdapterLuid: 0UL,
        TotalDeviceBytes: 8L * OneGib,
        Buckets: new Dictionary<VramBucket, VramBucketBudget>
        {
            [VramBucket.TtsLlama] = new(Minimum: 1L * OneGib, Budget: 1L * OneGib, Priority: 60),
            [VramBucket.TtsDac] = new(Minimum: 0L, Budget: 0L, Priority: 0),
            [VramBucket.Avatar] = new(Minimum: 2L * OneGib, Budget: 2L * OneGib, Priority: 80),
            [VramBucket.Rasterizer] = new(Minimum: 64L * OneMib, Budget: 128L * OneMib, Priority: 90),
            [VramBucket.Training] = new(Minimum: 0L, Budget: 2L * OneGib, Priority: 20),
            [VramBucket.LlmInference] = new(Minimum: 0L, Budget: 256L * OneMib, Priority: 30),
            [VramBucket.Embed] = new(Minimum: 0L, Budget: 64L * OneMib, Priority: 10),
            [VramBucket.Stt] = new(Minimum: 0L, Budget: 64L * OneMib, Priority: 10),
            [VramBucket.Headroom] = new(Minimum: 0L, Budget: (2L * OneGib) + (512L * OneMib), Priority: 0),
        });

    /// <summary>
    /// Generic 24 GB+ preset for workstation-class GPUs. Splits:
    /// TTS 3 GiB / Avatar 6 GiB / Rasterizer 256 MiB / Training 10 GiB /
    /// LlmInference 1 GiB / Embed 256 MiB / Stt 256 MiB /
    /// Headroom 3 GiB + 256 MiB.
    /// </summary>
    public static VramLayout Generic_24GB_Plus { get; } = new(
        Id: Generic_24GB_Plus_Id,
        AdapterDescription: Generic_24GB_Plus_Id,
        AdapterLuid: 0UL,
        TotalDeviceBytes: 24L * OneGib,
        Buckets: new Dictionary<VramBucket, VramBucketBudget>
        {
            [VramBucket.TtsLlama] = new(Minimum: 3L * OneGib, Budget: 3L * OneGib, Priority: 60),
            [VramBucket.TtsDac] = new(Minimum: 0L, Budget: 0L, Priority: 0),
            [VramBucket.Avatar] = new(Minimum: 6L * OneGib, Budget: 6L * OneGib, Priority: 80),
            [VramBucket.Rasterizer] = new(Minimum: 128L * OneMib, Budget: 256L * OneMib, Priority: 90),
            [VramBucket.Training] = new(Minimum: 0L, Budget: 10L * OneGib, Priority: 20),
            [VramBucket.LlmInference] = new(Minimum: 0L, Budget: 1L * OneGib, Priority: 30),
            [VramBucket.Embed] = new(Minimum: 0L, Budget: 256L * OneMib, Priority: 10),
            [VramBucket.Stt] = new(Minimum: 0L, Budget: 256L * OneMib, Priority: 10),
            [VramBucket.Headroom] = new(Minimum: 0L, Budget: (3L * OneGib) + (256L * OneMib), Priority: 0),
        });

    /// <summary>Enumerates all built-in presets (stable order: RX9060XT_16GB, Generic_8GB, Generic_24GB_Plus).</summary>
    /// <returns>All in-code presets.</returns>
    public static IReadOnlyList<VramLayout> All { get; } = new[]
    {
        RX9060XT_16GB,
        Generic_8GB,
        Generic_24GB_Plus,
    };

    /// <summary>
    /// Looks up a preset by id (case-insensitive). Returns <see langword="null"/>
    /// when no preset matches — callers treat null as "fall through to auto-detect".
    /// </summary>
    /// <param name="id">Preset id to resolve.</param>
    /// <returns>The matching <see cref="VramLayout"/> or <see langword="null"/>.</returns>
    public static VramLayout? TryGet(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        foreach (VramLayout preset in All)
        {
            if (string.Equals(preset.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return preset;
            }
        }

        return null;
    }
}
