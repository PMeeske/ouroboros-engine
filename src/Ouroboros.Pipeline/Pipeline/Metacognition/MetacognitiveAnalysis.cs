namespace Ouroboros.Pipeline.Metacognition;

/// <summary>
/// Represents a comprehensive metacognitive analysis of a reasoning trace.
/// Combines reflection results with style analysis and improvement suggestions.
/// </summary>
/// <param name="Trace">The original reasoning trace that was analyzed.</param>
/// <param name="Reflection">The reflection result with quality metrics.</param>
/// <param name="Style">The thinking style profile.</param>
/// <param name="Improvements">List of improvement suggestions.</param>
/// <param name="AnalyzedAt">When the analysis was performed.</param>
public sealed record MetacognitiveAnalysis(
    ReasoningTrace Trace,
    ReflectionResult Reflection,
    ThinkingStyle Style,
    ImmutableList<string> Improvements,
    DateTime AnalyzedAt)
{
    /// <summary>
    /// Gets a summary of the analysis quality.
    /// </summary>
    public string QualitySummary => Reflection.QualityScore switch
    {
        >= 0.9 => "Excellent reasoning quality",
        >= 0.7 => "Good reasoning quality",
        >= 0.5 => "Moderate reasoning quality - improvements recommended",
        >= 0.3 => "Poor reasoning quality - significant improvements needed",
        _ => "Very poor reasoning quality - fundamental issues detected",
    };

    /// <summary>
    /// Gets whether this analysis indicates acceptable reasoning.
    /// </summary>
    public bool IsAcceptable => Reflection.MeetsQualityThreshold(0.6);

    /// <summary>
    /// Gets the primary areas needing improvement.
    /// </summary>
    public IEnumerable<string> PriorityImprovements => Improvements.Take(3);
}