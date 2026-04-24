// <copyright file="IExpressionClassifier.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Abstractions.Monads;
using Ouroboros.Tensor.Abstractions;

namespace Ouroboros.Tensor.Adapters;

/// <summary>
/// Classifies a rendered avatar frame into a 5D <see cref="AffectiveVector"/>
/// wrapped in an <see cref="Engram{T}"/> so each classification carries
/// experiential context (somatic valence, identity weight, temporal stamp)
/// that flows downstream into the manifold.
/// </summary>
/// <remarks>
/// <para>
/// Each non-empty engram is a recallable memory of "what I looked like at
/// frame N" -- the natural feedstock for the dream cycle, manifold ranking,
/// and the future training-seam. Mapping convention:
/// <list type="bullet">
///   <item><description><see cref="Engram{T}.SomaticValence"/> = <see cref="AffectiveVector.Valence"/></description></item>
///   <item><description><see cref="Engram{T}.IdentityWeight"/> = <see cref="AffectiveVector.Confidence"/></description></item>
///   <item><description><see cref="Engram{T}.TemporalContext"/> = <c>DateTimeOffset.UtcNow</c> at the moment classification completes</description></item>
///   <item><description><see cref="Engram{T}.AssociativeLinks"/> = <c>Array.Empty&lt;Guid&gt;()</c> (Hebbian linking is owned by downstream consumers)</description></item>
/// </list>
/// </para>
/// <para>
/// Failure modes (null frame, dispose, scheduler back-pressure, inference
/// error) return <see cref="Engram{T}.Empty"/>. Implementations MUST NOT throw
/// for recoverable errors; only <see cref="OperationCanceledException"/> is
/// allowed to propagate.
/// </para>
/// </remarks>
public interface IExpressionClassifier
{
    /// <summary>
    /// Classifies a frame into an <see cref="Engram{T}"/> of <see cref="AffectiveVector"/>.
    /// </summary>
    /// <param name="frame">RGBA frame buffer (Width * Height * 4 bytes).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A non-empty engram on success carrying the measured affective vector
    /// with somatic / identity / temporal context. An empty engram
    /// (<see cref="Engram{T}.Empty"/>) on any recoverable failure.
    /// </returns>
    Task<Engram<AffectiveVector>> ClassifyAsync(
        FrameBuffer frame,
        CancellationToken cancellationToken);
}
