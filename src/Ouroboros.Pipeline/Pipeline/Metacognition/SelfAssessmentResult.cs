namespace Ouroboros.Pipeline.Metacognition;

/// <summary>
/// Represents a complete self-assessment result across all performance dimensions.
/// Aggregates individual dimension scores into a holistic evaluation.
/// </summary>
/// <param name="Id">Unique identifier for this assessment.</param>
/// <param name="AssessmentTime">When the assessment was performed.</param>
/// <param name="DimensionScores">Scores for each performance dimension.</param>
/// <param name="OverallScore">Weighted aggregate score across all dimensions.</param>
/// <param name="OverallConfidence">Aggregate confidence in the overall assessment.</param>
/// <param name="Strengths">Identified areas of strong performance.</param>
/// <param name="Weaknesses">Identified areas needing improvement.</param>
/// <param name="ImprovementAreas">Prioritized list of areas for development.</param>
public sealed record SelfAssessmentResult(
    Guid Id,
    DateTime AssessmentTime,
    ImmutableDictionary<PerformanceDimension, DimensionScore> DimensionScores,
    double OverallScore,
    double OverallConfidence,
    ImmutableList<string> Strengths,
    ImmutableList<string> Weaknesses,
    ImmutableList<string> ImprovementAreas)
{
    /// <summary>
    /// Creates an empty assessment with no data.
    /// </summary>
    /// <returns>An empty SelfAssessmentResult.</returns>
    public static SelfAssessmentResult Empty() => new(
        Id: Guid.NewGuid(),
        AssessmentTime: DateTime.UtcNow,
        DimensionScores: ImmutableDictionary<PerformanceDimension, DimensionScore>.Empty,
        OverallScore: 0.5,
        OverallConfidence: 0.0,
        Strengths: ImmutableList<string>.Empty,
        Weaknesses: ImmutableList<string>.Empty,
        ImprovementAreas: ImmutableList<string>.Empty);

    /// <summary>
    /// Creates a new assessment from dimension scores.
    /// Automatically computes overall scores and identifies strengths/weaknesses.
    /// </summary>
    /// <param name="dimensionScores">The scores for each dimension.</param>
    /// <returns>A complete SelfAssessmentResult.</returns>
    public static SelfAssessmentResult FromDimensionScores(
        ImmutableDictionary<PerformanceDimension, DimensionScore> dimensionScores)
    {
        if (dimensionScores.IsEmpty)
        {
            return Empty();
        }

        // Compute weighted overall score (confidence-weighted mean)
        var totalWeight = dimensionScores.Values.Sum(s => s.Confidence);
        var overallScore = totalWeight > 0
            ? dimensionScores.Values.Sum(s => s.Score * s.Confidence) / totalWeight
            : 0.5;

        // Overall confidence is geometric mean of individual confidences
        var confidenceProduct = dimensionScores.Values.Aggregate(1.0, (acc, s) => acc * Math.Max(s.Confidence, 0.01));
        var overallConfidence = Math.Pow(confidenceProduct, 1.0 / dimensionScores.Count);

        // Identify strengths (high score with reasonable confidence)
        var strengths = dimensionScores.Values
            .Where(s => s.Score >= 0.7 && s.Confidence >= 0.3)
            .OrderByDescending(s => s.Score * s.Confidence)
            .Select(s => $"{s.Dimension}: {s.Score:P0} (confidence: {s.Confidence:P0})")
            .ToImmutableList();

        // Identify weaknesses (low score with reasonable confidence)
        var weaknesses = dimensionScores.Values
            .Where(s => s.Score < 0.5 && s.Confidence >= 0.3)
            .OrderBy(s => s.Score)
            .Select(s => $"{s.Dimension}: {s.Score:P0} (confidence: {s.Confidence:P0})")
            .ToImmutableList();

        // Prioritize improvement areas (declining trends or low scores with high impact potential)
        var improvementAreas = dimensionScores.Values
            .Where(s => s.Trend == Trend.Declining || s.Score < 0.6)
            .OrderBy(s => s.Score)
            .ThenBy(s => s.Trend == Trend.Declining ? 0 : 1)
            .Select(s => $"{s.Dimension} ({s.Trend})")
            .ToImmutableList();

        return new SelfAssessmentResult(
            Id: Guid.NewGuid(),
            AssessmentTime: DateTime.UtcNow,
            DimensionScores: dimensionScores,
            OverallScore: overallScore,
            OverallConfidence: overallConfidence,
            Strengths: strengths,
            Weaknesses: weaknesses,
            ImprovementAreas: improvementAreas);
    }

    /// <summary>
    /// Gets the score for a specific dimension if available.
    /// </summary>
    /// <param name="dimension">The dimension to query.</param>
    /// <returns>Some(score) if available, None otherwise.</returns>
    public Option<DimensionScore> GetDimensionScore(PerformanceDimension dimension)
        => DimensionScores.TryGetValue(dimension, out var score)
            ? Option<DimensionScore>.Some(score)
            : Option<DimensionScore>.None();

    /// <summary>
    /// Creates a copy with an updated dimension score.
    /// </summary>
    /// <param name="score">The new or updated dimension score.</param>
    /// <returns>A new SelfAssessmentResult with the updated score.</returns>
    public SelfAssessmentResult WithDimensionScore(DimensionScore score)
        => FromDimensionScores(DimensionScores.SetItem(score.Dimension, score));
}