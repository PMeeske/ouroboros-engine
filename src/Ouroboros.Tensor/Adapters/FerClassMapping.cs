// <copyright file="FerClassMapping.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Adapters;

/// <summary>
/// Maps an FER+ 8-class softmax probability distribution to a 5D
/// <see cref="AffectiveVector"/> via Russell's circumplex (Valence × Arousal)
/// plus three affective extensions (Confidence, Curiosity, Stress).
/// </summary>
/// <remarks>
/// The FER+ class order is fixed by the ONNX model:
/// <c>[neutral, happiness, surprise, sadness, anger, disgust, fear, contempt]</c>.
/// Per-class weights in <see cref="Weights"/> are pre-clamped to each
/// dimension's range so the weighted sum is in-range without further clamping
/// (defensive <see cref="Math.Clamp(float, float, float)"/> is applied anyway
/// to future-proof against table edits).
/// </remarks>
internal static class FerClassMapping
{
    /// <summary>The number of FER+ output classes.</summary>
    public const int ClassCount = 8;

    /// <summary>
    /// Per-class affective weights, in FER+ output order:
    /// <c>[neutral, happiness, surprise, sadness, anger, disgust, fear, contempt]</c>.
    /// Each row is <c>(Valence, Arousal, Confidence, Curiosity, Stress)</c>.
    /// </summary>
    private static readonly float[,] Weights = new float[ClassCount, 5]
    {
        // Valence, Arousal, Confidence, Curiosity, Stress
        { +0.00f, 0.30f, 0.60f, 0.20f, 0.10f }, // neutral
        { +0.85f, 0.65f, 0.85f, 0.40f, 0.05f }, // happiness
        { +0.10f, 0.85f, 0.40f, 0.95f, 0.40f }, // surprise
        { -0.70f, 0.30f, 0.30f, 0.10f, 0.50f }, // sadness
        { -0.65f, 0.85f, 0.70f, 0.15f, 0.85f }, // anger
        { -0.55f, 0.55f, 0.55f, 0.10f, 0.60f }, // disgust
        { -0.75f, 0.85f, 0.20f, 0.30f, 0.95f }, // fear
        { -0.40f, 0.45f, 0.75f, 0.10f, 0.40f }, // contempt
    };

    /// <summary>
    /// Maps an FER+ softmax distribution to a 5D <see cref="AffectiveVector"/>
    /// by weighted sum across classes.
    /// </summary>
    /// <param name="probabilities">
    /// Softmax probabilities in FER+ class order. Length must equal
    /// <see cref="ClassCount"/>; otherwise <see cref="AffectiveVector.Neutral"/>
    /// is returned.
    /// </param>
    /// <returns>The mapped affective vector, with each component clamped defensively to its declared range.</returns>
    public static AffectiveVector Map(ReadOnlySpan<float> probabilities)
    {
        if (probabilities.Length != ClassCount)
        {
            return AffectiveVector.Neutral;
        }

        float v = 0f, a = 0f, c = 0f, cu = 0f, s = 0f;
        for (int i = 0; i < ClassCount; i++)
        {
            float p = probabilities[i];
            v += p * Weights[i, 0];
            a += p * Weights[i, 1];
            c += p * Weights[i, 2];
            cu += p * Weights[i, 3];
            s += p * Weights[i, 4];
        }

        return new AffectiveVector(
            Valence: Math.Clamp(v, -1f, 1f),
            Arousal: Math.Clamp(a, 0f, 1f),
            Confidence: Math.Clamp(c, 0f, 1f),
            Curiosity: Math.Clamp(cu, 0f, 1f),
            Stress: Math.Clamp(s, 0f, 1f));
    }
}
