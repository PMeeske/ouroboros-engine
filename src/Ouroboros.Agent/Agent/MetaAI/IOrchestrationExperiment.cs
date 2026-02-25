namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Interface for orchestration A/B testing experiments.
/// </summary>
public interface IOrchestrationExperiment
{
    /// <summary>
    /// Runs an A/B test comparing multiple orchestration strategies.
    /// </summary>
    /// <param name="experimentId">Unique identifier for the experiment.</param>
    /// <param name="variants">List of orchestrator variants to compare.</param>
    /// <param name="testPrompts">Test prompts to evaluate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing experiment results or error message.</returns>
    Task<Result<ExperimentResult, string>> RunExperimentAsync(
        string experimentId,
        List<IModelOrchestrator> variants,
        List<string> testPrompts,
        CancellationToken ct = default);
}