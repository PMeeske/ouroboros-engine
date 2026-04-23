// <copyright file="IExpressionClassifier.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Abstractions.Monads;
using Ouroboros.Tensor.Abstractions;

namespace Ouroboros.Tensor.Adapters;

/// <summary>
/// Classifies a rendered avatar frame into a 5D <see cref="AffectiveVector"/>.
/// </summary>
/// <remarks>
/// This is the seam between the live 3DGS frame tap and the persona's
/// self-perception loop. The 260424-00n slice ships a deterministic stub
/// implementation (<c>StubExpressionClassifier</c>) so the plumbing can be
/// exercised end-to-end; v14.0 replaces the stub with a real FER ONNX model
/// without changing this contract.
/// </remarks>
public interface IExpressionClassifier
{
    /// <summary>
    /// Classifies a frame into an <see cref="AffectiveVector"/>.
    /// </summary>
    /// <param name="frame">RGBA frame buffer (Width * Height * 4 bytes).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// Success: the measured affective vector. Failure: a reason string
    /// (e.g. "frame null", "rgba too small"). Never throws for recoverable
    /// errors; <see cref="OperationCanceledException"/> is propagated.
    /// </returns>
    Task<Result<AffectiveVector>> ClassifyAsync(
        FrameBuffer frame,
        CancellationToken cancellationToken);
}
