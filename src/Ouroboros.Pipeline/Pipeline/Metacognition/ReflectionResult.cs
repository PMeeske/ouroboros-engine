namespace Ouroboros.Pipeline.Metacognition;

/// <summary>
/// Represents the result of reflecting on a reasoning trace.
/// Captures quality metrics, identified issues, and improvement suggestions.
/// </summary>
/// <param name="OriginalTrace">The reasoning trace that was analyzed.</param>
/// <param name="QualityScore">Overall quality score (0.0 to 1.0).</param>
/// <param name="LogicalSoundness">Score for logical consistency and validity (0.0 to 1.0).</param>
/// <param name="EvidenceSupport">Score for how well conclusions are supported by evidence (0.0 to 1.0).</param>
/// <param name="IdentifiedFallacies">List of logical fallacies detected in the reasoning.</param>
/// <param name="MissedConsiderations">Factors that should have been considered but weren't.</param>
/// <param name="AlternativeConclusions">Other valid conclusions that could have been drawn.</param>
/// <param name="Improvements">Specific suggestions for improving the reasoning.</param>
public sealed record ReflectionResult(
    ReasoningTrace OriginalTrace,
    double QualityScore,
    double LogicalSoundness,
    double EvidenceSupport,
    ImmutableList<string> IdentifiedFallacies,
    ImmutableList<string> MissedConsiderations,
    ImmutableList<string> AlternativeConclusions,
    ImmutableList<string> Improvements)
{
    /// <summary>
    /// Creates a reflection result indicating high-quality reasoning.
    /// </summary>
    /// <param name="trace">The original trace.</param>
    /// <returns>A positive reflection result.</returns>
    public static ReflectionResult HighQuality(ReasoningTrace trace) => new(
        OriginalTrace: trace,
        QualityScore: 0.9,
        LogicalSoundness: 0.95,
        EvidenceSupport: 0.85,
        IdentifiedFallacies: ImmutableList<string>.Empty,
        MissedConsiderations: ImmutableList<string>.Empty,
        AlternativeConclusions: ImmutableList<string>.Empty,
        Improvements: ImmutableList<string>.Empty);

    /// <summary>
    /// Creates a reflection result for an empty or invalid trace.
    /// </summary>
    /// <param name="trace">The original trace.</param>
    /// <returns>A reflection result indicating invalid reasoning.</returns>
    public static ReflectionResult Invalid(ReasoningTrace trace) => new(
        OriginalTrace: trace,
        QualityScore: 0.0,
        LogicalSoundness: 0.0,
        EvidenceSupport: 0.0,
        IdentifiedFallacies: ImmutableList.Create("Invalid or empty reasoning trace"),
        MissedConsiderations: ImmutableList<string>.Empty,
        AlternativeConclusions: ImmutableList<string>.Empty,
        Improvements: ImmutableList.Create("Provide a complete reasoning trace with observations, inferences, and a conclusion"));

    /// <summary>
    /// Gets whether the reasoning quality meets a minimum threshold.
    /// </summary>
    /// <param name="threshold">The minimum acceptable quality score.</param>
    /// <returns>True if quality meets or exceeds the threshold.</returns>
    public bool MeetsQualityThreshold(double threshold = 0.7)
        => QualityScore >= threshold;

    /// <summary>
    /// Gets whether there are any identified issues with the reasoning.
    /// </summary>
    public bool HasIssues => IdentifiedFallacies.Count > 0 || MissedConsiderations.Count > 0;

    /// <summary>
    /// Adds an identified fallacy to the result.
    /// </summary>
    /// <param name="fallacy">The fallacy to add.</param>
    /// <returns>A new ReflectionResult with the added fallacy.</returns>
    public ReflectionResult WithFallacy(string fallacy)
        => this with { IdentifiedFallacies = IdentifiedFallacies.Add(fallacy) };

    /// <summary>
    /// Adds a missed consideration to the result.
    /// </summary>
    /// <param name="consideration">The consideration to add.</param>
    /// <returns>A new ReflectionResult with the added consideration.</returns>
    public ReflectionResult WithMissedConsideration(string consideration)
        => this with { MissedConsiderations = MissedConsiderations.Add(consideration) };

    /// <summary>
    /// Adds an improvement suggestion to the result.
    /// </summary>
    /// <param name="improvement">The improvement to add.</param>
    /// <returns>A new ReflectionResult with the added improvement.</returns>
    public ReflectionResult WithImprovement(string improvement)
        => this with { Improvements = Improvements.Add(improvement) };
}