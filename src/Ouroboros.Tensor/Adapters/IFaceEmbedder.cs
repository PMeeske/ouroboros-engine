// <copyright file="IFaceEmbedder.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Tensor.Abstractions;

namespace Ouroboros.Tensor.Adapters;

/// <summary>
/// Phase 239: face-embedding seam for multi-user identity. Produces a native
/// 128-dimension feature vector (SFace convention) from a face-cropped
/// <see cref="FrameBuffer"/>. The cropped region is the face bbox produced by
/// <see cref="IFaceDetector"/>.
/// </summary>
/// <remarks>
/// <para>
/// Invariants:
/// <list type="bullet">
///   <item>Embeddings are transient in-process values. They MUST NOT be written
///         to disk or stored outside the <c>iaret-user-identities</c> Qdrant
///         collection (Phase 234 CRIT-2).</item>
///   <item>ONNX implementations MUST pass through
///         <c>IPerceptionVramBudget.TryReserveAsync</c> before
///         <c>InferenceSession.Create</c> (Phase 235 GPU-04).</item>
///   <item>Failure modes (cancellation excepted) return <c>null</c>; callers
///         fall back to the "unknown" identity path.</item>
/// </list>
/// </para>
/// </remarks>
public interface IFaceEmbedder
{
    /// <summary>
    /// Gets the dimensionality of the produced embedding. SFace = 128.
    /// </summary>
    int Dimensions { get; }

    /// <summary>
    /// Embeds <paramref name="face"/> into a <see cref="Dimensions"/>-length
    /// L2-normalized feature vector, or returns <c>null</c> on recoverable
    /// failure (null / too-small frame, detector miss, inference error,
    /// admission denial).
    /// </summary>
    /// <param name="face">Face-cropped RGBA frame.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>128-length (or <see cref="Dimensions"/>) float array, or <c>null</c>.</returns>
    Task<float[]?> EmbedAsync(
        FrameBuffer face,
        CancellationToken cancellationToken);
}
