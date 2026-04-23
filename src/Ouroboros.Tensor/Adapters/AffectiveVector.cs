// <copyright file="AffectiveVector.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Adapters;

/// <summary>
/// Five-dimensional affective vector used for self-perception drift logging.
/// Bridges the stub-classifier output (measured expression) and the
/// persona's intended emotional state (PersonalityEngine.CurrentConsciousness).
/// </summary>
/// <remarks>
/// Component ranges mirror <c>EmotionalContext</c> in the Application layer:
/// <list type="bullet">
///   <item><description><see cref="Valence"/>: [-1, 1] (negative → positive affect)</description></item>
///   <item><description><see cref="Arousal"/>: [0, 1] (calm → excited)</description></item>
///   <item><description><see cref="Confidence"/>: [0, 1]</description></item>
///   <item><description><see cref="Curiosity"/>: [0, 1]</description></item>
///   <item><description><see cref="Stress"/>: [0, 1]</description></item>
/// </list>
/// This is the permanent wire-format for self-perception. The stub classifier
/// (260424-00n) is throwaway; v14.0 FER replaces it but keeps this shape.
/// </remarks>
/// <param name="Valence">Valence dimension in <c>[-1, 1]</c>.</param>
/// <param name="Arousal">Arousal dimension in <c>[0, 1]</c>.</param>
/// <param name="Confidence">Confidence dimension in <c>[0, 1]</c>.</param>
/// <param name="Curiosity">Curiosity dimension in <c>[0, 1]</c>.</param>
/// <param name="Stress">Stress dimension in <c>[0, 1]</c>.</param>
public sealed record AffectiveVector(
    float Valence,
    float Arousal,
    float Confidence,
    float Curiosity,
    float Stress)
{
    /// <summary>
    /// Canonical neutral baseline — mirrors <c>EmotionalContext.Neutral</c>
    /// (Valence=0, Arousal=0.5, Confidence=0.5, Curiosity=0.3, Stress=0).
    /// </summary>
    public static AffectiveVector Neutral => new(0f, 0.5f, 0.5f, 0.3f, 0f);

    /// <summary>Component-wise signed delta: <c>this - other</c>.</summary>
    /// <param name="other">The vector to subtract.</param>
    /// <returns>A new <see cref="AffectiveVector"/> with each component being <c>this - other</c>.</returns>
    public AffectiveVector Delta(AffectiveVector other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new AffectiveVector(
            Valence - other.Valence,
            Arousal - other.Arousal,
            Confidence - other.Confidence,
            Curiosity - other.Curiosity,
            Stress - other.Stress);
    }

    /// <summary>L2 norm of the vector treated as a 5-tuple.</summary>
    /// <returns>The Euclidean length of the 5D vector.</returns>
    public float L2Norm()
        => MathF.Sqrt(
            (Valence * Valence)
            + (Arousal * Arousal)
            + (Confidence * Confidence)
            + (Curiosity * Curiosity)
            + (Stress * Stress));
}
