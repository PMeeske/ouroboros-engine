// <copyright file="VramBucket.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Abstractions;

/// <summary>
/// String-backed identifier for a VRAM allocation bucket inside an
/// <see cref="IVramLayout"/>. Well-known instances cover the canonical
/// subsystems (<see cref="TtsLlama"/>, <see cref="Avatar"/>,
/// <see cref="Rasterizer"/>, etc.), but arbitrary string ids remain valid
/// so new buckets can be added without breaking the contract.
/// </summary>
/// <param name="Id">
/// Stable bucket identifier. Compared case-sensitively by the default
/// record-struct equality.
/// </param>
public readonly record struct VramBucket(string Id)
{
    /// <summary>TTS LLAMA-class inference (semantic token generation, DirectML).</summary>
    public static readonly VramBucket TtsLlama = new("TtsLlama");

    /// <summary>TTS DAC decoder (typically CPU today, reserved for future GPU move).</summary>
    public static readonly VramBucket TtsDac = new("TtsDac");

    /// <summary>Avatar rendering pipeline (deformation net + RIFE + pipeline buffers).</summary>
    public static readonly VramBucket Avatar = new("Avatar");

    /// <summary>
    /// 3DGS compute-shader rasterizer bucket introduced in Phase 188.1.
    /// First-class slot for the new <c>DirectComputeGaussianRasterizer</c>.
    /// </summary>
    public static readonly VramBucket Rasterizer = new("Rasterizer");

    /// <summary>Online / background training workloads.</summary>
    public static readonly VramBucket Training = new("Training");

    /// <summary>Unallocated headroom reserved for the OS compositor + transient spikes.</summary>
    public static readonly VramBucket Headroom = new("Headroom");

    /// <summary>Returns the bucket id so diagnostic logs print <c>TtsLlama</c> rather than <c>VramBucket { Id = TtsLlama }</c>.</summary>
    /// <returns>The underlying <see cref="Id"/>.</returns>
    public override string ToString() => Id;
}
