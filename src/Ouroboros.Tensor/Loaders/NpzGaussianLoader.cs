// <copyright file="NpzGaussianLoader.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.IO.Compression;
using Ouroboros.Tensor.Abstractions;

namespace Ouroboros.Tensor.Loaders;

/// <summary>
/// Loads a mesh-bound 3DGS checkpoint from a NumPy <c>.npz</c> archive into a
/// <see cref="GaussianSet"/>. Rev 2 pivot target after repo scan: the real
/// Iaret checkpoint at <c>data/iaret-model/canonical_gaussians_4422.npy</c>
/// is pickled-dict numpy, but the paired <c>.npz</c> archive stores each
/// field as a primitive <c>.npy</c> entry inside a ZIP — safely parseable
/// from C# with no pickle runtime.
/// </summary>
/// <remarks>
/// <para>Required entries (ZIP entry name without <c>.npy</c> suffix):</para>
/// <list type="bullet">
///   <item><c>positions</c>            — float32 (N, 3)</item>
///   <item><c>scales</c>               — float32 (N, 3)</item>
///   <item><c>quaternions</c>          — float32 (N, 4) in <c>(w, x, y, z)</c> order</item>
///   <item><c>opacities</c>            — float32 (N,)</item>
///   <item><c>colors</c>               — float32 (N, 3) direct RGB in <c>[0, 1]</c></item>
///   <item><c>triangle_indices</c>     — int64 (N,)</item>
///   <item><c>barycentric_weights</c>  — float32 (N, 3)</item>
/// </list>
/// </remarks>
public static class NpzGaussianLoader
{
    private const string PositionsKey = "positions";
    private const string ScalesKey = "scales";
    private const string QuaternionsKey = "quaternions";
    private const string OpacitiesKey = "opacities";
    private const string ColorsKey = "colors";
    private const string TriangleIndicesKey = "triangle_indices";
    private const string BarycentricWeightsKey = "barycentric_weights";

    /// <summary>Loads a <see cref="GaussianSet"/> from a <c>.npz</c> file on disk.</summary>
    /// <param name="path">Absolute or working-directory-relative path to the <c>.npz</c> archive.</param>
    /// <returns>The decoded <see cref="GaussianSet"/>.</returns>
    public static GaussianSet LoadFromFile(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($".npz checkpoint not found: {path}", path);
        }

        using FileStream fs = File.OpenRead(path);
        return Load(fs);
    }

    /// <summary>Loads a <see cref="GaussianSet"/> from an open <c>.npz</c> stream.</summary>
    /// <param name="stream">Seekable stream positioned at the start of the ZIP archive.</param>
    /// <returns>The decoded <see cref="GaussianSet"/>.</returns>
    public static GaussianSet Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        // ZipArchive needs a seekable stream.
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);

        float[] positions = ReadFloat32(zip, PositionsKey);
        float[] scales = ReadFloat32(zip, ScalesKey);
        float[] quaternions = ReadFloat32(zip, QuaternionsKey);
        float[] opacities = ReadFloat32(zip, OpacitiesKey);
        float[] colors = ReadFloat32(zip, ColorsKey);
        long[] triangleIndices = ReadInt64(zip, TriangleIndicesKey);
        float[] barycentricWeights = ReadFloat32(zip, BarycentricWeightsKey);

        if (opacities.Length == 0)
        {
            throw new InvalidDataException("opacities array is empty; cannot determine gaussian count.");
        }

        return new GaussianSet(
            count: opacities.Length,
            positions: positions,
            scales: scales,
            rotations: quaternions,
            opacities: opacities,
            colors: colors,
            triangleIndices: triangleIndices,
            barycentricWeights: barycentricWeights);
    }

    private static float[] ReadFloat32(ZipArchive zip, string key)
    {
        byte[] body = ReadEntryBytes(zip, key);
        NpyHeader.HeaderInfo info = NpyHeader.Read(body);
        return NpyHeader.ReadFloat32Body(body, info);
    }

    private static long[] ReadInt64(ZipArchive zip, string key)
    {
        byte[] body = ReadEntryBytes(zip, key);
        NpyHeader.HeaderInfo info = NpyHeader.Read(body);
        return NpyHeader.ReadInt64Body(body, info);
    }

    private static byte[] ReadEntryBytes(ZipArchive zip, string key)
    {
        ZipArchiveEntry? entry = zip.GetEntry(key + ".npy");
        if (entry is null)
        {
            throw new InvalidDataException($"Missing required entry '{key}.npy' in .npz archive.");
        }

        using Stream stream = entry.Open();
        using var ms = new MemoryStream(capacity: (int)Math.Min(entry.Length, int.MaxValue));
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
