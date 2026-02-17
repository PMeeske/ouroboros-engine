namespace Ouroboros.Pipeline.Learning;

/// <summary>
/// Records an adaptation decision made by the agent.
/// Captures the context, rationale, and impact of adaptations for analysis and rollback.
/// </summary>
/// <param name="Id">Unique identifier for this adaptation event.</param>
/// <param name="AgentId">Identifier of the agent that adapted.</param>
/// <param name="EventType">The type of adaptation that occurred.</param>
/// <param name="Description">Human-readable description of what changed and why.</param>
/// <param name="BeforeMetrics">Performance snapshot before the adaptation.</param>
/// <param name="AfterMetrics">Performance snapshot after the adaptation (null if not yet measured).</param>
/// <param name="Timestamp">When the adaptation occurred.</param>
public sealed record AdaptationEvent(
    Guid Id,
    Guid AgentId,
    AdaptationEventType EventType,
    string Description,
    AgentPerformance BeforeMetrics,
    AgentPerformance? AfterMetrics,
    DateTime Timestamp)
{
    /// <summary>
    /// Creates a new adaptation event with auto-generated ID and current timestamp.
    /// </summary>
    /// <param name="agentId">The ID of the adapting agent.</param>
    /// <param name="eventType">The type of adaptation.</param>
    /// <param name="description">Description of the adaptation.</param>
    /// <param name="beforeMetrics">Performance before adaptation.</param>
    /// <returns>A new AdaptationEvent instance.</returns>
    public static AdaptationEvent Create(
        Guid agentId,
        AdaptationEventType eventType,
        string description,
        AgentPerformance beforeMetrics) => new(
        Id: Guid.NewGuid(),
        AgentId: agentId,
        EventType: eventType,
        Description: description,
        BeforeMetrics: beforeMetrics,
        AfterMetrics: null,
        Timestamp: DateTime.UtcNow);

    /// <summary>
    /// Creates a copy with the after-metrics populated.
    /// </summary>
    /// <param name="afterMetrics">The performance metrics after adaptation.</param>
    /// <returns>A new AdaptationEvent with after-metrics set.</returns>
    public AdaptationEvent WithAfterMetrics(AgentPerformance afterMetrics)
        => this with { AfterMetrics = afterMetrics };

    /// <summary>
    /// Calculates the performance delta caused by this adaptation.
    /// </summary>
    /// <returns>The change in average response quality, or null if after-metrics not available.</returns>
    public double? PerformanceDelta => AfterMetrics is not null
        ? AfterMetrics.AverageResponseQuality - BeforeMetrics.AverageResponseQuality
        : null;

    /// <summary>
    /// Determines if this adaptation was beneficial (improved performance).
    /// </summary>
    /// <returns>True if performance improved, false if declined, null if not yet measured.</returns>
    public bool? WasBeneficial => PerformanceDelta.HasValue
        ? PerformanceDelta.Value > 0
        : null;
}