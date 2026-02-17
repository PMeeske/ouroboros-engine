using Ouroboros.Abstractions;

namespace Ouroboros.Pipeline.Learning;

/// <summary>
/// Provides Kleisli arrow operations for adaptive agent pipelines.
/// </summary>
public static class AdaptiveAgentArrow
{
    /// <summary>
    /// Creates a step that records an interaction and returns updated performance.
    /// </summary>
    /// <param name="agent">The adaptive agent to record interactions for.</param>
    /// <returns>A step that transforms interaction data into performance results.</returns>
    public static Step<(string Input, string Output, double Quality), Result<AgentPerformance, string>> RecordInteractionStep(
        IAdaptiveAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        return tuple => Task.FromResult(agent.RecordInteraction(tuple.Input, tuple.Output, tuple.Quality));
    }

    /// <summary>
    /// Creates a step that checks if adaptation is needed and performs it if so.
    /// </summary>
    /// <param name="agent">The adaptive agent to potentially adapt.</param>
    /// <returns>A step that returns the adaptation event if adaptation occurred, or None if not.</returns>
    public static Step<Unit, Option<AdaptationEvent>> TryAdaptStep(IAdaptiveAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        return _ =>
        {
            if (agent.ShouldAdapt())
            {
                var result = agent.Adapt();
                return Task.FromResult(result.IsSuccess
                    ? Option<AdaptationEvent>.Some(result.Value)
                    : Option<AdaptationEvent>.None());
            }

            return Task.FromResult(Option<AdaptationEvent>.None());
        };
    }

    /// <summary>
    /// Creates a step that retrieves the current performance metrics.
    /// </summary>
    /// <param name="agent">The adaptive agent to query.</param>
    /// <returns>A step that returns the current performance snapshot.</returns>
    public static Step<Unit, AgentPerformance> GetPerformanceStep(IAdaptiveAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        return _ => Task.FromResult(agent.GetPerformance());
    }

    /// <summary>
    /// Creates a step that retrieves the adaptation history.
    /// </summary>
    /// <param name="agent">The adaptive agent to query.</param>
    /// <returns>A step that returns the adaptation history.</returns>
    public static Step<Unit, ImmutableList<AdaptationEvent>> GetAdaptationHistoryStep(IAdaptiveAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        return _ => Task.FromResult(agent.GetAdaptationHistory());
    }

    /// <summary>
    /// Creates a step that rolls back a specific adaptation.
    /// </summary>
    /// <param name="agent">The adaptive agent to perform rollback on.</param>
    /// <returns>A step that returns the rollback event if successful.</returns>
    public static Step<Guid, Result<AdaptationEvent, string>> RollbackStep(IAdaptiveAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        return adaptationId => Task.FromResult(agent.Rollback(adaptationId));
    }

    /// <summary>
    /// Creates a full learning pipeline that records an interaction and optionally triggers adaptation.
    /// </summary>
    /// <param name="agent">The adaptive agent.</param>
    /// <returns>A step that processes interaction data and returns the performance along with any adaptation.</returns>
    public static Step<(string Input, string Output, double Quality), Result<(AgentPerformance Performance, AdaptationEvent? Adaptation), string>> FullLearningPipeline(
        IAdaptiveAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        return async tuple =>
        {
            // Record the interaction
            var recordResult = agent.RecordInteraction(tuple.Input, tuple.Output, tuple.Quality);
            if (recordResult.IsFailure)
            {
                return Result<(AgentPerformance Performance, AdaptationEvent? Adaptation), string>.Failure(recordResult.Error);
            }

            // Check for and perform adaptation if needed
            AdaptationEvent? adaptation = null;
            if (agent.ShouldAdapt())
            {
                var adaptResult = agent.Adapt();
                if (adaptResult.IsSuccess)
                {
                    adaptation = adaptResult.Value;
                }
            }

            await Task.CompletedTask; // Ensure async context
            return Result<(AgentPerformance Performance, AdaptationEvent? Adaptation), string>.Success(
                (recordResult.Value, adaptation));
        };
    }

    /// <summary>
    /// Creates a step that processes a batch of interactions.
    /// </summary>
    /// <param name="agent">The adaptive agent.</param>
    /// <returns>A step that processes multiple interactions and returns aggregated results.</returns>
    public static Step<IEnumerable<(string Input, string Output, double Quality)>, Result<AgentPerformance, string>> ProcessBatchStep(
        IAdaptiveAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        return interactions =>
        {
            Result<AgentPerformance, string> lastResult = Result<AgentPerformance, string>.Failure("No interactions provided");

            foreach (var (input, output, quality) in interactions)
            {
                lastResult = agent.RecordInteraction(input, output, quality);
                if (!lastResult.IsSuccess)
                {
                    return Task.FromResult(lastResult);
                }
            }

            return Task.FromResult(lastResult);
        };
    }

    /// <summary>
    /// Creates a conditional adaptation step that only adapts when a predicate is satisfied.
    /// </summary>
    /// <param name="agent">The adaptive agent.</param>
    /// <param name="predicate">Predicate to determine if adaptation should proceed.</param>
    /// <returns>A step that conditionally adapts based on the predicate.</returns>
    public static Step<AgentPerformance, Option<AdaptationEvent>> ConditionalAdaptStep(
        IAdaptiveAgent agent,
        Func<AgentPerformance, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(predicate);

        return performance =>
        {
            if (predicate(performance) && agent.ShouldAdapt())
            {
                var result = agent.Adapt();
                return Task.FromResult(result.IsSuccess
                    ? Option<AdaptationEvent>.Some(result.Value)
                    : Option<AdaptationEvent>.None());
            }

            return Task.FromResult(Option<AdaptationEvent>.None());
        };
    }
}