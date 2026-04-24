// <copyright file="StubPoseEstimator.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Tensor.Abstractions;

namespace Ouroboros.Tensor.Adapters;

/// <summary>
/// Phase 240 stub <see cref="IPoseEstimator"/>: returns a deterministic 17-keypoint
/// estimate centred on the frame with low but non-zero confidence. Gets the
/// posture-engram pipeline wired end-to-end without MoveNet.
/// </summary>
public sealed class StubPoseEstimator : IPoseEstimator
{
    private const int KeypointCount = 17;
    private const float StubConfidence = 0.2f;

    private readonly ILogger<StubPoseEstimator>? _logger;

    /// <summary>Initializes a new instance of the <see cref="StubPoseEstimator"/> class.</summary>
    /// <param name="logger">Optional logger.</param>
    public StubPoseEstimator(ILogger<StubPoseEstimator>? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<PoseEstimate?> EstimateAsync(FrameBuffer frame, CancellationToken cancellationToken)
    {
        if (frame is null || frame.Width <= 0 || frame.Height <= 0)
        {
            return Task.FromResult<PoseEstimate?>(null);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var keypoints = new PoseKeypoint[KeypointCount];
            float cx = frame.Width * 0.5f;
            float cy = frame.Height * 0.5f;
            for (int i = 0; i < KeypointCount; i++)
            {
                float jitter = (i % 5) * 0.02f;
                keypoints[i] = new PoseKeypoint(
                    X: cx * (1f + jitter),
                    Y: cy * (1f + jitter),
                    Confidence: StubConfidence);
            }

            return Task.FromResult<PoseEstimate?>(
                new PoseEstimate(keypoints, StubConfidence));
        }
        catch (OperationCanceledException) { throw; }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger?.LogDebug(ex, "[StubPoseEstimator] stub failed");
            return Task.FromResult<PoseEstimate?>(null);
        }
    }
}
