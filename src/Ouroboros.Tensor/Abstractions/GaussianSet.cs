// <copyright file="GaussianSet.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Abstractions;

/// <summary>
/// Immutable mesh-bound 3D Gaussian Splatting (3DGS) state passed to the
/// <see cref="IGaussianRasterizer"/> adapter. Schema matches the repo's
/// canonical_gaussians_4422.npz checkpoint produced by the Python training
/// pipeline: degree-0 direct-RGB colors (no spherical harmonics) plus a
/// per-gaussian mesh-triangle binding + barycentric coordinate trio so
/// plan 05's deformation layer can project per-vertex deltas onto the bound
/// gaussian.
/// </summary>
/// <remarks>
/// <para>
/// All arrays are stored in the raw on-disk encoding — opacities are
/// logit-space and scales are log-space; the rasterizer applies
/// <c>sigmoid</c> / <c>exp</c> respectively. Colors are already in
/// <c>[0, 1]</c> direct-RGB (degree-0 SH projection is pre-baked into the
/// checkpoint). Quaternions are stored as <c>(w, x, y, z)</c>.
/// </para>
/// <para>
/// <see cref="TriangleIndices"/> and <see cref="BarycentricWeights"/> are
/// pass-through for the rasterizer itself; they exist on <c>GaussianSet</c>
/// because the deformation stage consumes them. The HLSL rasterizer in plan
/// 03 only reads <see cref="Positions"/>, <see cref="Scales"/>,
/// <see cref="Rotations"/>, <see cref="Opacities"/>, and <see cref="Colors"/>.
/// </para>
/// </remarks>
public sealed record GaussianSet
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GaussianSet"/> class.
    /// Initializes a new <see cref="GaussianSet"/>. Throws
    /// <see cref="ArgumentException"/> when array lengths disagree with
    /// <paramref name="count"/> or when any position/scale/opacity value is
    /// non-finite (NaN / Inf — mitigates T-188.1-02-03).
    /// </summary>
    /// <param name="count">Number of gaussians. Must be positive.</param>
    /// <param name="positions">Flat <c>(N*3)</c> xyz positions in world space.</param>
    /// <param name="scales">Flat <c>(N*3)</c> log-space per-axis scales.</param>
    /// <param name="rotations">Flat <c>(N*4)</c> quaternions in <c>(w, x, y, z)</c> order.</param>
    /// <param name="opacities">Flat <c>(N,)</c> logit-space opacities.</param>
    /// <param name="colors">Flat <c>(N*3)</c> direct-RGB colors in <c>[0, 1]</c>.</param>
    /// <param name="triangleIndices">Flat <c>(N,)</c> int64 mesh triangle bindings.</param>
    /// <param name="barycentricWeights">Flat <c>(N*3)</c> barycentric coordinates on the bound triangle.</param>
    public GaussianSet(
        int count,
        float[] positions,
        float[] scales,
        float[] rotations,
        float[] opacities,
        float[] colors,
        long[] triangleIndices,
        float[] barycentricWeights)
    {
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(scales);
        ArgumentNullException.ThrowIfNull(rotations);
        ArgumentNullException.ThrowIfNull(opacities);
        ArgumentNullException.ThrowIfNull(colors);
        ArgumentNullException.ThrowIfNull(triangleIndices);
        ArgumentNullException.ThrowIfNull(barycentricWeights);

        if (count <= 0)
        {
            throw new ArgumentException("Count must be positive (zero-gaussian sets are rejected).", nameof(count));
        }

        ValidateLength(positions.Length, count * 3, nameof(positions));
        ValidateLength(scales.Length, count * 3, nameof(scales));
        ValidateLength(rotations.Length, count * 4, nameof(rotations));
        ValidateLength(opacities.Length, count, nameof(opacities));
        ValidateLength(colors.Length, count * 3, nameof(colors));
        ValidateLength(triangleIndices.Length, count, nameof(triangleIndices));
        ValidateLength(barycentricWeights.Length, count * 3, nameof(barycentricWeights));

        // T-188.1-02-03 mitigation: reject non-finite values that would
        // cause NaN covariance → infinite loops at raster time.
        ValidateFinite(positions, nameof(positions));
        ValidateFinite(scales, nameof(scales));
        ValidateFinite(opacities, nameof(opacities));

        Count = count;
        Positions = positions;
        Scales = scales;
        Rotations = rotations;
        Opacities = opacities;
        Colors = colors;
        TriangleIndices = triangleIndices;
        BarycentricWeights = barycentricWeights;
    }

    /// <summary>Gets the number of gaussians in the set.</summary>
    public int Count { get; }

    /// <summary>Gets the flat <c>(N*3)</c> xyz position buffer.</summary>
    public float[] Positions { get; }

    /// <summary>Gets the flat <c>(N*3)</c> log-space per-axis scale buffer.</summary>
    public float[] Scales { get; }

    /// <summary>Gets the flat <c>(N*4)</c> rotation quaternion buffer in <c>(w, x, y, z)</c> order.</summary>
    public float[] Rotations { get; }

    /// <summary>Gets the flat <c>(N,)</c> logit-space opacity buffer.</summary>
    public float[] Opacities { get; }

    /// <summary>Gets the flat <c>(N*3)</c> direct-RGB color buffer in <c>[0, 1]</c>.</summary>
    public float[] Colors { get; }

    /// <summary>Gets the flat <c>(N,)</c> int64 mesh-triangle binding buffer.</summary>
    public long[] TriangleIndices { get; }

    /// <summary>Gets the flat <c>(N*3)</c> barycentric-weights buffer.</summary>
    public float[] BarycentricWeights { get; }

    private static void ValidateLength(int actual, int expected, string name)
    {
        if (actual != expected)
        {
            throw new ArgumentException($"Expected length {expected}, got {actual}.", name);
        }
    }

    private static void ValidateFinite(float[] buffer, string name)
    {
        for (int i = 0; i < buffer.Length; i++)
        {
            float v = buffer[i];
            if (!float.IsFinite(v))
            {
                throw new ArgumentException($"Non-finite value ({v}) at index {i}.", name);
            }
        }
    }
}
