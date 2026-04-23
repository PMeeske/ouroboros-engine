// <copyright file="StubExpressionClassifier.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Numerics;
using Ouroboros.Abstractions.Monads;
using Ouroboros.Tensor.Abstractions;
using Ouroboros.Tensor.Orchestration;

namespace Ouroboros.Tensor.Adapters;

/// <summary>
/// Deterministic hash-bucket <see cref="IExpressionClassifier"/> implementation
/// used by the 260424-00n self-perception drift-logging slice. Identical frame
/// bytes always produce the identical <see cref="AffectiveVector"/>.
/// </summary>
/// <remarks>
/// <para>
/// The classify op is routed through <see cref="GpuScheduler"/> at
/// <see cref="GpuTaskPriority.Background"/> so when a real FER ONNX model replaces
/// this stub, the plumbing (VRAM accounting, priority preemption) is already
/// in place — see the "all heavy GPU work via Tensor adapters" rule.
/// </para>
/// <para>
/// The hash is built by folding the RGBA buffer in 4KB strides using a
/// <c>BitOperations.RotateLeft</c> mixing step. This is CPU-only and O(n/stride);
/// the scheduler routing is a formality for now.
/// </para>
/// </remarks>
public sealed class StubExpressionClassifier : IExpressionClassifier
{
    private const int HashStride = 4096;
    private const int MinRgbaLength = 16;
    private const long EstimatedVramBytes = 0; // CPU-only stub

    private readonly GpuScheduler _scheduler;
    private readonly ILogger<StubExpressionClassifier>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StubExpressionClassifier"/> class.
    /// </summary>
    /// <param name="scheduler">GPU scheduler used to route the classify op (honored for future-proofing).</param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    public StubExpressionClassifier(GpuScheduler scheduler, ILogger<StubExpressionClassifier>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        _scheduler = scheduler;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<Result<AffectiveVector>> ClassifyAsync(
        FrameBuffer frame,
        CancellationToken cancellationToken)
    {
        if (frame is null)
        {
            return Result<AffectiveVector>.Failure("frame null");
        }

        if (frame.Rgba is null || frame.Rgba.Length < MinRgbaLength)
        {
            return Result<AffectiveVector>.Failure(
                $"rgba buffer too small (length={frame.Rgba?.Length ?? 0}, min={MinRgbaLength})");
        }

        try
        {
            AffectiveVector vector = await _scheduler.ScheduleAsync(
                GpuTaskPriority.Background,
                new GpuResourceRequirements(EstimatedVramBytes),
                () => ClassifyCore(frame.Rgba),
                cancellationToken).ConfigureAwait(false);

            return Result<AffectiveVector>.Success(vector);
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
            return Result<AffectiveVector>.Failure($"stub classify: {ex.Message}");
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
