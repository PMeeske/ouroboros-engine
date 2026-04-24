// <copyright file="IPoseEstimator.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Tensor.Abstractions;

namespace Ouroboros.Tensor.Adapters;

/// <summary>A 2D keypoint with its detector confidence in [0, 1].</summary>
/// <param name="X">Pixel X.</param>
/// <param name="Y">Pixel Y.</param>
/// <param name="Confidence">Detector score.</param>
public sealed record PoseKeypoint(float X, float Y, float Confidence);

/// <summary>17-joint MoveNet-style pose estimate.</summary>
/// <param name="Keypoints">Ordered keypoint array (MoveNet convention).</param>
/// <param name="OverallConfidence">Mean keypoint confidence in [0, 1].</param>
public sealed record PoseEstimate(
    IReadOnlyList<PoseKeypoint> Keypoints,
    float OverallConfidence);

/// <summary>
/// Phase 240: pose-estimation seam for body-posture engrams. ONNX implementations
/// MUST register VRAM via <see cref="Orchestration.IPerceptionVramBudget"/>.
/// Raw frame bytes stay inside the estimator; downstream consumers receive only
/// keypoint coords + confidences.
/// </summary>
public interface IPoseEstimator
{
    /// <summary>Estimates body pose from a frame, returning null on recoverable failure.</summary>
    /// <param name="frame">RGBA frame buffer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Non-null estimate on success; null on failure.</returns>
    Task<PoseEstimate?> EstimateAsync(FrameBuffer frame, CancellationToken cancellationToken);
}
