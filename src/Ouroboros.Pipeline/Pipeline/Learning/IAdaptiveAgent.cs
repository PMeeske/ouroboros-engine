namespace Ouroboros.Pipeline.Learning;

/// <summary>
/// Interface for agents that continuously learn and adapt their behavior.
/// </summary>
public interface IAdaptiveAgent
{
    /// <summary>
    /// Gets the unique identifier of this agent.
    /// </summary>
    Guid AgentId { get; }

    /// <summary>
    /// Records an interaction and updates performance metrics.
    /// </summary>
    /// <param name="input">The input that was processed.</param>
    /// <param name="output">The output that was generated.</param>
    /// <param name="quality">Quality score of the interaction (-1.0 to 1.0).</param>
    /// <returns>Result indicating success or failure of the recording.</returns>
    Result<AgentPerformance, string> RecordInteraction(string input, string output, double quality);

    /// <summary>
    /// Determines if the agent should adapt based on current performance metrics.
    /// </summary>
    /// <returns>True if adaptation is recommended.</returns>
    bool ShouldAdapt();

    /// <summary>
    /// Performs an adaptation to improve agent behavior.
    /// </summary>
    /// <returns>Result containing the adaptation event if successful.</returns>
    Result<AdaptationEvent, string> Adapt();

    /// <summary>
    /// Gets the current performance metrics of the agent.
    /// </summary>
    /// <returns>The current AgentPerformance snapshot.</returns>
    AgentPerformance GetPerformance();

    /// <summary>
    /// Gets the history of adaptation events.
    /// </summary>
    /// <returns>Immutable list of adaptation events in chronological order.</returns>
    ImmutableList<AdaptationEvent> GetAdaptationHistory();

    /// <summary>
    /// Rolls back a specific adaptation by ID.
    /// </summary>
    /// <param name="adaptationId">The ID of the adaptation to rollback.</param>
    /// <returns>Result containing the rollback event if successful.</returns>
    Result<AdaptationEvent, string> Rollback(Guid adaptationId);
}