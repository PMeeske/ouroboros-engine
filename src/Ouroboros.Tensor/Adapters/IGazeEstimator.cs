// <copyright file="IGazeEstimator.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Tensor.Abstractions;

namespace Ouroboros.Tensor.Adapters;

/// <summary>
/// Gaze direction estimate.
/// </summary>
/// <param name="YawRadians">Horizontal gaze angle (positive = right).</param>
/// <param name="PitchRadians">Vertical gaze angle (positive = up).</param>
/// <param name="Confidence">Estimator confidence in [0, 1].</param>
public sealed record GazeEstimate(
    float YawRadians,
    float PitchRadians,
    float Confidence);

/// <summary>
/// Phase 240: gaze-direction seam for <c>role="user-gaze"</c> engrams. Consumes
/// a face-cropped RGBA frame and returns the (yaw, pitch) angles with a
/// detector confidence. Same VRAM-gate + stub-fallback pattern as
/// <see cref="IFaceDetector"/> and <see cref="IPoseEstimator"/>.
/// </summary>
public interface IGazeEstimator
{
    /// <summary>
    /// Estimates gaze direction; returns null on recoverable failure.
    /// </summary>
    /// <param name="face">Face-cropped frame.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Non-null estimate on success; null on failure.</returns>
    Task<GazeEstimate?> EstimateAsync(FrameBuffer face, CancellationToken cancellationToken);
}
