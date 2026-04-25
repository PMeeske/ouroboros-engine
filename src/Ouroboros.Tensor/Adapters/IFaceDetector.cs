// <copyright file="IFaceDetector.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Tensor.Abstractions;

namespace Ouroboros.Tensor.Adapters;

/// <summary>
/// Detected face region in pixel coordinates. Landmarks are the 5-point YuNet layout
/// when produced by <c>YuNetOnnxFaceDetector</c> (right-eye, left-eye, nose-tip,
/// right-mouth, left-mouth — each a 2-float (x,y) pair, so 10 floats total); the
/// stub detector returns <see langword="null"/>.
/// </summary>
/// <param name="X">Top-left X in pixels.</param>
/// <param name="Y">Top-left Y in pixels.</param>
/// <param name="Width">Bounding box width in pixels.</param>
/// <param name="Height">Bounding box height in pixels.</param>
/// <param name="Confidence">Detector score in [0, 1].</param>
/// <param name="Landmarks5">Optional 10-float landmark array (5 points × 2 coords).</param>
public sealed record FaceDetection(
    int X,
    int Y,
    int Width,
    int Height,
    float Confidence,
    float[]? Landmarks5);

/// <summary>
/// Phase 237: face detection seam for the user-perception pipeline. Consumes a
/// raw RGBA <see cref="FrameBuffer"/> (the frame must already be authorized by
/// <c>AuthorizedSensorStream</c>) and returns zero or more
/// <see cref="FaceDetection"/> regions.
/// </summary>
/// <remarks>
/// <para>
/// Invariants:
/// <list type="bullet">
///   <item>Implementations MUST NOT persist or log raw frame bytes (Phase 234 CRIT-2).</item>
///   <item>Implementations MUST route ONNX session creation through
///         <c>IPerceptionVramBudget.TryReserveAsync</c> before
///         <c>InferenceSession.Create</c> (Phase 235 GPU-04).</item>
///   <item>Failure modes (cancelled, session down, allocation denied) MUST return
///         an empty list — never throw for recoverable errors;
///         <see cref="OperationCanceledException"/> propagates.</item>
/// </list>
/// </para>
/// <para>
/// Downstream consumers (Phase 238 FER, Phase 239 SFace) crop the frame using
/// the returned bbox and pass only the face region into their model — raw
/// full-frame tensors never leave the detector's scope.
/// </para>
/// </remarks>
public interface IFaceDetector
{
    /// <summary>
    /// Detects faces in <paramref name="frame"/>. Returns the empty list when no
    /// faces are found, when the detector is unavailable, or when a recoverable
    /// inference error occurs.
    /// </summary>
    /// <param name="frame">RGBA frame buffer (Width × Height × 4 bytes).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Read-only list of detections; ordered by descending confidence.</returns>
    Task<IReadOnlyList<FaceDetection>> DetectAsync(
        FrameBuffer frame,
        CancellationToken cancellationToken);
}
