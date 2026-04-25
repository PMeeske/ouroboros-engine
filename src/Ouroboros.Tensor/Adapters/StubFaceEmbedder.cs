// <copyright file="StubFaceEmbedder.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Numerics;
using Ouroboros.Tensor.Abstractions;

namespace Ouroboros.Tensor.Adapters;

/// <summary>
/// Phase 239 stub <see cref="IFaceEmbedder"/>: deterministic hash-fold of the
/// input pixels into a 128d unit vector. Identical frame bytes always yield
/// the identical embedding — useful for wiring the identity registry end-to-end
/// without the SFace ONNX model file on the device.
/// </summary>
public sealed class StubFaceEmbedder : IFaceEmbedder
{
    /// <summary>SFace default embedding dimensionality.</summary>
    public const int EmbeddingDimensions = 128;

    private const int HashStride = 32;
    private const int MinRgbaLength = 16;

    private readonly ILogger<StubFaceEmbedder>? _logger;

    /// <summary>Initializes a new instance of the <see cref="StubFaceEmbedder"/> class.</summary>
    /// <param name="logger">Optional logger.</param>
    public StubFaceEmbedder(ILogger<StubFaceEmbedder>? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public int Dimensions => EmbeddingDimensions;

    /// <inheritdoc/>
    public Task<float[]?> EmbedAsync(FrameBuffer face, CancellationToken cancellationToken)
    {
        if (face is null || face.Rgba is null || face.Rgba.Length < MinRgbaLength)
        {
            return Task.FromResult<float[]?>(null);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            float[] embedding = EmbedCore(face.Rgba);
            return Task.FromResult<float[]?>(embedding);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // Embedder must never throw on recoverable errors.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger?.LogDebug(ex, "[StubFaceEmbedder] embed failed");
            return Task.FromResult<float[]?>(null);
        }
    }

    private static float[] EmbedCore(byte[] rgba)
    {
        float[] output = new float[EmbeddingDimensions];
        ulong hash = 0xCBF29CE484222325UL;
        int p = 0;
        for (int i = 0; i < EmbeddingDimensions; i++)
        {
            for (int j = 0; j < HashStride && p < rgba.Length; j++, p++)
            {
                byte b = rgba[p];
                hash = BitOperations.RotateLeft(hash ^ b, 5) + b;
            }

            // Map to [-1, 1] — spread two hash lanes across sign + magnitude.
            float signLane = ((hash & 0xFFFFUL) / 65535f * 2f) - 1f;
            float magLane = ((hash >> 32) & 0xFFFFUL) / 65535f;
            output[i] = signLane * magLane;

            if (p >= rgba.Length)
            {
                p = 0;
            }
        }

        // L2-normalize so cosine == dot.
        double normSq = 0;
        for (int i = 0; i < output.Length; i++)
        {
            normSq += output[i] * output[i];
        }

        float norm = (float)Math.Sqrt(normSq);
        if (norm > 1e-9f)
        {
            float inv = 1f / norm;
            for (int i = 0; i < output.Length; i++)
            {
                output[i] *= inv;
            }
        }

        return output;
    }
}
