// <copyright file="StubExpressionClassifier.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Numerics;
using Ouroboros.Abstractions.Monads;
using Ouroboros.Tensor.Abstractions;

namespace Ouroboros.Tensor.Adapters;

/// <summary>
/// Deterministic hash-bucket <see cref="IExpressionClassifier"/> implementation
/// used by the 260424-00n self-perception drift-logging slice. Identical frame
/// bytes always produce the identical <see cref="AffectiveVector"/>.
/// </summary>
/// <remarks>
/// Pure CPU hash-fold. The real FER ONNX replacement will route through the GPU
/// scheduler at Background priority; the stub intentionally does not, so the
/// 5Hz drift loop cannot contend with the Realtime rasterizer on RDNA 4.
/// </remarks>
public sealed class StubExpressionClassifier : IExpressionClassifier
{
    private const int HashStride = 4096;
    private const int MinRgbaLength = 16;

    private readonly ILogger<StubExpressionClassifier>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StubExpressionClassifier"/> class.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    public StubExpressionClassifier(ILogger<StubExpressionClassifier>? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<Engram<AffectiveVector>> ClassifyAsync(
        FrameBuffer frame,
        CancellationToken cancellationToken)
    {
        if (frame is null)
        {
            return Task.FromResult(Engram<AffectiveVector>.Empty());
        }

        if (frame.Rgba is null || frame.Rgba.Length < MinRgbaLength)
        {
            return Task.FromResult(Engram<AffectiveVector>.Empty());
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            AffectiveVector vector = ClassifyCore(frame.Rgba);
            return Task.FromResult(BuildEngram(vector));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // Classifier must never throw on recoverable errors
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger?.LogDebug(ex, "Stub expression classification failed");
            return Task.FromResult(Engram<AffectiveVector>.Empty());
        }
    }

    private static AffectiveVector ClassifyCore(byte[] rgba)
    {
        ulong hash = HashFold(rgba);

        float valence = (((hash & 0xFFFFUL) / 65535f) * 2f) - 1f;       // [-1, 1]
        float arousal = ((hash >> 16) & 0xFFFFUL) / 65535f;              // [0, 1]
        float confidence = ((hash >> 32) & 0xFFFFUL) / 65535f;           // [0, 1]
        float curiosity = ((hash >> 48) & 0xFFFFUL) / 65535f;            // [0, 1]
        float stress = ((hash >> 8) & 0xFFFFUL) / 65535f;                // [0, 1]

        return new AffectiveVector(valence, arousal, confidence, curiosity, stress);
    }

    private static Engram<AffectiveVector> BuildEngram(AffectiveVector vector)
    {
        return Engram<AffectiveVector>.Create(
            vector,
            temporalContext: DateTimeOffset.UtcNow,
            somaticValence: Math.Clamp(vector.Valence, -1f, 1f),
            associativeLinks: Array.Empty<Guid>(),
            identityWeight: Math.Clamp(vector.Confidence, 0f, 1f));
    }

    private static ulong HashFold(byte[] buffer)
    {
        ulong hash = 0xCBF29CE484222325UL; // FNV-1a offset basis (starting seed only)
        for (int i = 0; i < buffer.Length; i += HashStride)
        {
            byte b = buffer[i];
            hash = BitOperations.RotateLeft(hash ^ b, 7) + b;
        }

        return hash;
    }
}
