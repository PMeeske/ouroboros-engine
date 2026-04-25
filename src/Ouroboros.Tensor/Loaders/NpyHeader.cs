// <copyright file="NpyHeader.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Buffers.Binary;
using System.Text;

namespace Ouroboros.Tensor.Loaders;

/// <summary>
/// Minimal parser for the NumPy <c>.npy</c> file-header format, supporting
/// only primitive array dtypes (<c>&lt;f4</c>, <c>&lt;i8</c>). Used by
/// <see cref="NpzGaussianLoader"/> to decode the seven per-field arrays inside
/// a mesh-bound 3DGS <c>.npz</c> checkpoint. Pickled object arrays
/// (<c>|O</c>) are explicitly rejected — we do not interpret Python pickles.
/// </summary>
public static class NpyHeader
{
    /// <summary>Supported numpy dtypes.</summary>
    public enum DType
    {
        /// <summary>Little-endian 32-bit IEEE-754 float.</summary>
        Float32,

        /// <summary>Little-endian 64-bit signed integer.</summary>
        Int64,
    }

    /// <summary>Decoded header metadata.</summary>
    /// <param name="DType">Element dtype.</param>
    /// <param name="Shape">Array shape (row-major, Fortran order rejected).</param>
    /// <param name="BodyOffset">Byte offset in the underlying stream where the raw numeric body begins.</param>
    public sealed record HeaderInfo(DType DType, int[] Shape, int BodyOffset);

    /// <summary>Reads + validates the header of an <c>.npy</c> stream.</summary>
    /// <param name="buffer">Full <c>.npy</c> payload as a byte span.</param>
    /// <returns>Decoded <see cref="HeaderInfo"/>.</returns>
    /// <exception cref="InvalidDataException">Thrown when the header is malformed, Fortran-ordered, or references an unsupported dtype (including pickled object arrays).</exception>
    public static HeaderInfo Read(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < 10)
        {
            throw new InvalidDataException("Buffer too small to contain an .npy header.");
        }

        // Magic: \x93NUMPY
        if (buffer[0] != 0x93 || buffer[1] != (byte)'N' || buffer[2] != (byte)'U'
            || buffer[3] != (byte)'M' || buffer[4] != (byte)'P' || buffer[5] != (byte)'Y')
        {
            throw new InvalidDataException("Not an .npy file (magic bytes mismatch).");
        }

        byte major = buffer[6];
        int headerLenBytes;
        int headerStart;
        int headerLen;
        if (major == 1)
        {
            headerLenBytes = 2;
            headerStart = 10;
            headerLen = BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(8, 2));
        }
        else
        {
            headerLenBytes = 4;
            headerStart = 12;
            headerLen = (int)BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(8, 4));
        }

        if (headerStart + headerLen > buffer.Length)
        {
            throw new InvalidDataException("Header length exceeds buffer.");
        }

        string header = Encoding.ASCII.GetString(buffer.Slice(headerStart, headerLen));

        string dtypeStr = ExtractField(header, "descr");
        string fortran = ExtractField(header, "fortran_order");
        string shapeStr = ExtractField(header, "shape");

        if (fortran.Contains("True", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Fortran-ordered .npy not supported.");
        }

        DType dtype;
        if (dtypeStr.Contains("<f4", StringComparison.Ordinal))
        {
            dtype = DType.Float32;
        }
        else if (dtypeStr.Contains("<i8", StringComparison.Ordinal))
        {
            dtype = DType.Int64;
        }
        else if (dtypeStr.Contains("|O", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Pickled object-array .npy is not supported — use .npz with primitive arrays.");
        }
        else
        {
            throw new InvalidDataException($"Unsupported dtype '{dtypeStr}'. Only <f4 and <i8 are accepted.");
        }

        int[] shape = ParseShape(shapeStr);
        int bodyOffset = headerStart + headerLen;
        return new HeaderInfo(dtype, shape, bodyOffset);
    }

    /// <summary>Reads a primitive float32 array out of a decoded header.</summary>
    /// <returns></returns>
    public static float[] ReadFloat32Body(ReadOnlySpan<byte> buffer, HeaderInfo info)
    {
        if (info.DType != DType.Float32)
        {
            throw new InvalidDataException($"Expected float32 body, got {info.DType}.");
        }

        int count = TotalElements(info.Shape);
        ReadOnlySpan<byte> body = buffer.Slice(info.BodyOffset, count * 4);
        float[] result = new float[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = BinaryPrimitives.ReadSingleLittleEndian(body.Slice(i * 4, 4));
        }

        return result;
    }

    /// <summary>Reads a primitive int64 array out of a decoded header.</summary>
    /// <returns></returns>
    public static long[] ReadInt64Body(ReadOnlySpan<byte> buffer, HeaderInfo info)
    {
        if (info.DType != DType.Int64)
        {
            throw new InvalidDataException($"Expected int64 body, got {info.DType}.");
        }

        int count = TotalElements(info.Shape);
        ReadOnlySpan<byte> body = buffer.Slice(info.BodyOffset, count * 8);
        long[] result = new long[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = BinaryPrimitives.ReadInt64LittleEndian(body.Slice(i * 8, 8));
        }

        return result;
    }

    private static int TotalElements(int[] shape)
    {
        int count = 1;
        for (int i = 0; i < shape.Length; i++)
        {
            if (shape[i] < 0)
            {
                throw new InvalidDataException($"Negative dimension {shape[i]} at axis {i}.");
            }

            count *= shape[i];
        }

        return count;
    }

    private static string ExtractField(string header, string field)
    {
        int idx = header.IndexOf($"'{field}'", StringComparison.Ordinal);
        if (idx < 0)
        {
            return string.Empty;
        }

        int colon = header.IndexOf(':', idx);
        int start = colon + 1;
        while (start < header.Length && header[start] == ' ')
        {
            start++;
        }

        if (start >= header.Length)
        {
            return string.Empty;
        }

        if (header[start] == '\'')
        {
            int end = header.IndexOf('\'', start + 1);
            return end < 0 ? string.Empty : header[(start + 1)..end];
        }

        if (header[start] == '(')
        {
            int end = header.IndexOf(')', start);
            return end < 0 ? string.Empty : header[start..(end + 1)];
        }

        int terminator = header.IndexOfAny([',', '}'], start);
        return terminator < 0 ? header[start..].Trim() : header[start..terminator].Trim();
    }

    private static int[] ParseShape(string shapeStr)
    {
        string inner = shapeStr.Trim('(', ')', ' ');
        if (string.IsNullOrEmpty(inner))
        {
            return [1];
        }

        string[] parts = inner.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        int[] result = new int[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            result[i] = int.Parse(parts[i], System.Globalization.CultureInfo.InvariantCulture);
        }

        return result;
    }
}
