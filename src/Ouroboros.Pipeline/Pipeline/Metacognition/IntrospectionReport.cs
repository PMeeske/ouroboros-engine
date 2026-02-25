namespace Ouroboros.Pipeline.Metacognition;

/// <summary>
/// Report generated from introspection analysis of an internal state.
/// Contains observations, detected anomalies, and recommendations.
/// </summary>
public sealed record IntrospectionReport(
    InternalState StateSnapshot,
    ImmutableList<string> Observations,
    ImmutableList<string> Anomalies,
    ImmutableList<string> Recommendations,
    double SelfAssessmentScore,
    DateTime GeneratedAt)
{
    /// <summary>
    /// Creates an empty report for a given state.
    /// </summary>
    /// <param name="state">The state to report on.</param>
    /// <returns>An empty introspection report.</returns>
    public static IntrospectionReport Empty(InternalState state) => new(
        state,
        ImmutableList<string>.Empty,
        ImmutableList<string>.Empty,
        ImmutableList<string>.Empty,
        0.5,
        DateTime.UtcNow);

    /// <summary>
    /// Indicates whether the report contains any anomalies.
    /// </summary>
    public bool HasAnomalies => !Anomalies.IsEmpty;

    /// <summary>
    /// Indicates whether the report contains recommendations.
    /// </summary>
    public bool HasRecommendations => !Recommendations.IsEmpty;

    /// <summary>
    /// Adds an observation to the report.
    /// </summary>
    /// <param name="observation">The observation to add.</param>
    /// <returns>New report with the observation added.</returns>
    public IntrospectionReport WithObservation(string observation) =>
        this with { Observations = Observations.Add(observation) };

    /// <summary>
    /// Adds an anomaly to the report.
    /// </summary>
    /// <param name="anomaly">The anomaly to add.</param>
    /// <returns>New report with the anomaly added.</returns>
    public IntrospectionReport WithAnomaly(string anomaly) =>
        this with { Anomalies = Anomalies.Add(anomaly) };

    /// <summary>
    /// Adds a recommendation to the report.
    /// </summary>
    /// <param name="recommendation">The recommendation to add.</param>
    /// <returns>New report with the recommendation added.</returns>
    public IntrospectionReport WithRecommendation(string recommendation) =>
        this with { Recommendations = Recommendations.Add(recommendation) };
}