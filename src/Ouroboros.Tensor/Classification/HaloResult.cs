// <copyright file="HaloResult.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Classification;

/// <summary>
/// Result of HALO-Loss classification with out-of-distribution detection.
///
/// <para>
/// The HALO (Harmonious and Adaptive Loss function for Out-of-distribution) classification
/// produces a bounded confidence score by replacing dot-product logits with negative squared
/// L2 distance from class centroids. The origin sink provides a parameter-free "I don't know"
/// path: inputs far from all known centroids fall into the sink, producing
/// <see cref="IsOutOfDistribution"/> = true.
/// </para>
///
/// <para>
/// This maps to the Laws of Form three-valued logic:
/// <list type="bullet">
///   <item><description>Mark (Fire) = confident classification with high <see cref="Confidence"/></description></item>
///   <item><description>Void (Ignore) = low probability across all classes</description></item>
///   <item><description>Imaginary (Clarify) = OOD, origin sink wins = <see cref="IsOutOfDistribution"/> true</description></item>
/// </list>
/// </para>
/// </summary>
public readonly record struct HaloResult
{
    /// <summary>
    /// Gets index of the winning class, or -1 if the input is out-of-distribution (origin sink wins).
    /// When <see cref="IsOutOfDistribution"/> is true, this is always -1.
    /// </summary>
    public int ClassIndex { get; init; }

    /// <summary>
    /// Gets name of the winning class, or "unknown" if the input is out-of-distribution.
    /// When <see cref="IsOutOfDistribution"/> is true, this is always "unknown".
    /// </summary>
    public string ClassName { get; init; }

    /// <summary>
    /// Gets confidence score for the winning class, in range [0, 1].
    /// This is the softmax probability of the winning class.
    /// For in-distribution inputs near a centroid, this approaches 1.0.
    /// For equidistant inputs, this is approximately 1/K (uniform distribution).
    /// </summary>
    public float Confidence { get; init; }

    /// <summary>
    /// Gets probability mass assigned to the origin sink.
    /// The origin sink represents the "none of the above" category -- its logit is always 0
    /// after shift-invariant transformation, meaning it captures inputs that are far from all
    /// class centroids. High <see cref="SinkProbability"/> indicates OOD detection.
    /// When <see cref="IsOutOfDistribution"/> is true, this exceeds the probability of any class.
    /// </summary>
    public float SinkProbability { get; init; }

    /// <summary>
    /// Gets a value indicating whether whether the input was classified as out-of-distribution.
    /// True when the origin sink probability exceeds all class probabilities,
    /// meaning the input is far from all known centroids and the model signals
    /// "I don't know" rather than forcing a low-confidence classification.
    /// </summary>
    public bool IsOutOfDistribution { get; init; }

    /// <summary>
    /// Gets full softmax probability distribution over all classes and the origin sink.
    /// Length is K+1 when origin sink is included (last element is sink probability),
    /// or K when origin sink is excluded.
    /// </summary>
    public IReadOnlyList<float> AllProbabilities { get; init; }
}
