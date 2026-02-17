// <copyright file="SelfAssessment.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Abstractions;

namespace Ouroboros.Pipeline.Metacognition;

using System.Collections.Immutable;
using Ouroboros.Core.Monads;

/// <summary>
/// Represents a score for a single performance dimension with confidence and evidence.
/// Follows Bayesian principles where scores represent posterior beliefs given evidence.
/// </summary>
/// <param name="Dimension">The performance dimension being scored.</param>
/// <param name="Score">The estimated score in [0, 1] where 1 is optimal performance.</param>
/// <param name="Confidence">The confidence in the score estimate in [0, 1].</param>
/// <param name="Evidence">Supporting evidence for the score (observations, metrics, etc.).</param>
/// <param name="Trend">The direction of change in this dimension over time.</param>
public sealed record DimensionScore(
    PerformanceDimension Dimension,
    double Score,
    double Confidence,
    ImmutableList<string> Evidence,
    Trend Trend)
{
    /// <summary>
    /// Creates an unknown dimension score with no evidence.
    /// Represents maximum entropy (complete uncertainty) about the dimension.
    /// </summary>
    /// <param name="dimension">The dimension to create an unknown score for.</param>
    /// <returns>A DimensionScore with 0.5 score, 0 confidence, and unknown trend.</returns>
    public static DimensionScore Unknown(PerformanceDimension dimension) => new(
        Dimension: dimension,
        Score: 0.5,  // Maximum entropy prior
        Confidence: 0.0,
        Evidence: ImmutableList<string>.Empty,
        Trend: Trend.Unknown);

    /// <summary>
    /// Creates a dimension score with initial evidence.
    /// </summary>
    /// <param name="dimension">The dimension being scored.</param>
    /// <param name="score">The initial score estimate.</param>
    /// <param name="confidence">The confidence in the estimate.</param>
    /// <param name="evidence">Initial evidence supporting the score.</param>
    /// <returns>A new DimensionScore with unknown trend.</returns>
    public static DimensionScore Create(
        PerformanceDimension dimension,
        double score,
        double confidence,
        IEnumerable<string> evidence) => new(
            Dimension: dimension,
            Score: Math.Clamp(score, 0.0, 1.0),
            Confidence: Math.Clamp(confidence, 0.0, 1.0),
            Evidence: evidence.ToImmutableList(),
            Trend: Trend.Unknown);

    /// <summary>
    /// Updates the score with new evidence using Bayesian update rules.
    /// </summary>
    /// <param name="newScore">The new observed score.</param>
    /// <param name="observationWeight">Weight of the new observation (0-1).</param>
    /// <param name="newEvidence">New evidence to add.</param>
    /// <returns>Updated DimensionScore with combined beliefs.</returns>
    public DimensionScore WithBayesianUpdate(
        double newScore,
        double observationWeight,
        string newEvidence)
    {
        // Bayesian update: weighted combination of prior and likelihood
        var priorWeight = Confidence;
        var totalWeight = priorWeight + observationWeight;

        var updatedScore = totalWeight > 0
            ? ((Score * priorWeight) + (newScore * observationWeight)) / totalWeight
            : newScore;

        // Confidence increases with more evidence but with diminishing returns
        var updatedConfidence = Math.Min(1.0, Confidence + (observationWeight * (1.0 - Confidence) * 0.1));

        // Determine trend from score change
        var scoreDelta = updatedScore - Score;
        var updatedTrend = DetermineTrend(scoreDelta, Trend);

        return this with
        {
            Score = Math.Clamp(updatedScore, 0.0, 1.0),
            Confidence = updatedConfidence,
            Evidence = Evidence.Add(newEvidence),
            Trend = updatedTrend,
        };
    }

    /// <summary>
    /// Validates the dimension score values.
    /// </summary>
    /// <returns>A Result indicating validity or validation error.</returns>
    public Result<Unit, string> Validate()
    {
        if (Score < 0.0 || Score > 1.0)
        {
            return Result<Unit, string>.Failure($"Score must be in [0, 1], got {Score}.");
        }

        if (Confidence < 0.0 || Confidence > 1.0)
        {
            return Result<Unit, string>.Failure($"Confidence must be in [0, 1], got {Confidence}.");
        }

        return Result<Unit, string>.Success(Unit.Value);
    }

    private static Trend DetermineTrend(double scoreDelta, Trend currentTrend)
    {
        const double threshold = 0.02;

        if (Math.Abs(scoreDelta) < threshold)
        {
            return currentTrend == Trend.Unknown ? Trend.Stable : currentTrend;
        }

        return scoreDelta > 0 ? Trend.Improving : Trend.Declining;
    }
}