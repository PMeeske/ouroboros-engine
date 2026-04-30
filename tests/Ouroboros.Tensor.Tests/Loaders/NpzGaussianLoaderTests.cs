// <copyright file="NpzGaussianLoaderTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Ouroboros.Tensor.Abstractions;
using Ouroboros.Tensor.Loaders;

namespace Ouroboros.Tensor.Tests.Loaders;

public class NpzGaussianLoaderTests
{
    [Fact]
    public void Load_ValidNpz_ReturnsGaussianSetWithExpectedCount()
    {
        byte[] npz = BuildMinimalNpz(count: 3);
        using var ms = new MemoryStream(npz);

        GaussianSet set = NpzGaussianLoader.Load(ms);

        set.Count.Should().Be(3);
        set.Positions.Should().HaveCount(9);
        set.Scales.Should().HaveCount(9);
        set.Rotations.Should().HaveCount(12);
        set.Opacities.Should().HaveCount(3);
        set.Colors.Should().HaveCount(9);
        set.TriangleIndices.Should().HaveCount(3);
        set.BarycentricWeights.Should().HaveCount(9);
    }

    [Fact]
    public void Load_MissingRequiredEntry_ThrowsInvalidData()
    {
        byte[] npz = BuildMinimalNpz(count: 2, skipKey: "opacities");
        using var ms = new MemoryStream(npz);

        Action act = () => NpzGaussianLoader.Load(ms);

        act.Should().Throw<InvalidDataException>()
            .WithMessage("*opacities*");
    }

    [Fact]
    public void Load_MismatchedArrayLengths_ThrowsArgumentException()
    {
        // Build an .npz where positions has N=3 but opacities has N=2.
        byte[] npz = BuildNpzWithMismatch();
        using var ms = new MemoryStream(npz);

        Action act = () => NpzGaussianLoader.Load(ms);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ReadHeader_PickledObjectArray_Rejected()
    {
        // Build a minimal .npy header with dtype "|O" (pickled object) — must refuse.
        byte[] npy = BuildObjectArrayNpy();

        Action act = () => NpyHeader.Read(npy);

        act.Should().Throw<InvalidDataException>()
            .WithMessage("*pickled*");
    }

    [Fact]
    public void ReadHeader_FortranOrder_Rejected()
    {
        byte[] npy = BuildFloat32Npy(
            shape: [3, 3],
            body: new float[9],
            fortranOrder: true);

        Action act = () => NpyHeader.Read(npy);

        act.Should().Throw<InvalidDataException>()
            .WithMessage("*Fortran*");
    }

    [Fact]
    public void ReadHeader_BadMagic_Rejected()
    {
        byte[] npy = new byte[20]; // all zeros — no magic bytes

        Action act = () => NpyHeader.Read(npy);

        act.Should().Throw<InvalidDataException>()
            .WithMessage("*magic*");
    }

    [Fact]
    public void Load_RoundTripFloatValues_PreservedExactly()
    {
        float[] positions =
        [
            1.0f, 2.0f, 3.0f,
            4.0f, 5.0f, 6.0f,
        ];

        byte[] npz = BuildMinimalNpz(count: 2, positionsOverride: positions);
        using var ms = new MemoryStream(npz);

        GaussianSet set = NpzGaussianLoader.Load(ms);

        set.Positions.Should().BeEquivalentTo(positions);
    }

    // ── Test fixtures: hand-built numpy-compatible bytes ──────────────────

    private static byte[] BuildMinimalNpz(int count, string? skipKey = null, float[]? positionsOverride = null)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddIf(zip, skipKey, "positions", BuildFloat32Npy([count, 3], positionsOverride ?? new float[count * 3]));
            AddIf(zip, skipKey, "scales", BuildFloat32Npy([count, 3], new float[count * 3]));
            AddIf(zip, skipKey, "quaternions", BuildFloat32Npy([count, 4], new float[count * 4]));
            AddIf(zip, skipKey, "opacities", BuildFloat32Npy([count], new float[count]));
            AddIf(zip, skipKey, "colors", BuildFloat32Npy([count, 3], new float[count * 3]));
            AddIf(zip, skipKey, "triangle_indices", BuildInt64Npy([count], new long[count]));
            AddIf(zip, skipKey, "barycentric_weights", BuildFloat32Npy([count, 3], new float[count * 3]));
        }
        return ms.ToArray();
    }

    private static byte[] BuildNpzWithMismatch()
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddIf(zip, null, "positions", BuildFloat32Npy([3, 3], new float[9]));
            AddIf(zip, null, "scales", BuildFloat32Npy([3, 3], new float[9]));
            AddIf(zip, null, "quaternions", BuildFloat32Npy([3, 4], new float[12]));
            AddIf(zip, null, "opacities", BuildFloat32Npy([2], new float[2])); // mismatch!
            AddIf(zip, null, "colors", BuildFloat32Npy([3, 3], new float[9]));
            AddIf(zip, null, "triangle_indices", BuildInt64Npy([3], new long[3]));
            AddIf(zip, null, "barycentric_weights", BuildFloat32Npy([3, 3], new float[9]));
        }
        return ms.ToArray();
    }

    private static void AddIf(ZipArchive zip, string? skipKey, string name, byte[] bytes)
    {
        if (skipKey == name) return;
        ZipArchiveEntry entry = zip.CreateEntry(name + ".npy", CompressionLevel.NoCompression);
        using Stream stream = entry.Open();
        stream.Write(bytes, 0, bytes.Length);
    }

    private static byte[] BuildFloat32Npy(int[] shape, float[] body, bool fortranOrder = false)
    {
        string shapeStr = shape.Length == 1
            ? $"({shape[0]},)"
            : "(" + string.Join(", ", shape) + ")";
        string fortranStr = fortranOrder ? "True" : "False";
        string headerInner = $"{{'descr': '<f4', 'fortran_order': {fortranStr}, 'shape': {shapeStr}, }}";
        return BuildNpy(headerInner, body.Length * 4, writer =>
        {
            Span<byte> buf = stackalloc byte[4];
            foreach (float v in body)
            {
                BinaryPrimitives.WriteSingleLittleEndian(buf, v);
                writer.Write(buf);
            }
        });
    }

    private static byte[] BuildInt64Npy(int[] shape, long[] body)
    {
        string shapeStr = shape.Length == 1
            ? $"({shape[0]},)"
            : "(" + string.Join(", ", shape) + ")";
        string headerInner = $"{{'descr': '<i8', 'fortran_order': False, 'shape': {shapeStr}, }}";
        return BuildNpy(headerInner, body.Length * 8, writer =>
        {
            Span<byte> buf = stackalloc byte[8];
            foreach (long v in body)
            {
                BinaryPrimitives.WriteInt64LittleEndian(buf, v);
                writer.Write(buf);
            }
        });
    }

    private static byte[] BuildObjectArrayNpy()
    {
        string headerInner = "{'descr': '|O', 'fortran_order': False, 'shape': (), }";
        return BuildNpy(headerInner, 0, _ => { });
    }

    private static byte[] BuildNpy(string headerInner, int bodyLength, Action<BinaryWriter> writeBody)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // Magic + version 1.0
        writer.Write(new byte[] { 0x93, (byte)'N', (byte)'U', (byte)'M', (byte)'P', (byte)'Y' });
        writer.Write((byte)1);
        writer.Write((byte)0);

        // Pad header to 16-byte alignment (matches numpy's on-disk convention).
        // headerLenBytesTotal = 10 (magic+ver+len) + header_bytes
        byte[] headerBytes = Encoding.ASCII.GetBytes(headerInner + "\n");
        int unaligned = 10 + headerBytes.Length;
        int padding = (16 - (unaligned % 16)) % 16;
        byte[] paddedHeader = new byte[headerBytes.Length + padding];
        Buffer.BlockCopy(headerBytes, 0, paddedHeader, 0, headerBytes.Length);
        // Numpy pads with spaces ending in newline; our parser only reads ASCII
        // so any filler is fine. Mirror numpy: space pad, newline terminator.
        for (int i = headerBytes.Length; i < paddedHeader.Length; i++)
            paddedHeader[i] = (byte)' ';
        if (paddedHeader.Length > 0)
            paddedHeader[^1] = (byte)'\n';

        Span<byte> lenBuf = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(lenBuf, (ushort)paddedHeader.Length);
        writer.Write(lenBuf);
        writer.Write(paddedHeader);

        writeBody(writer);
        writer.Flush();
        return ms.ToArray();
    }
}
