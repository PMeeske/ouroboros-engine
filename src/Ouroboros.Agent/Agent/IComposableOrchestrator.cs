namespace Ouroboros.Agent;

/// <summary>
/// Represents a composable orchestrator that can be chained with other orchestrators.
/// </summary>
/// <typeparam name="TInput">Input type.</typeparam>
/// <typeparam name="TOutput">Output type.</typeparam>
public interface IComposableOrchestrator<TInput, TOutput> : IOrchestrator<TInput, TOutput>
{
    /// <summary>
    /// Chains this orchestrator with another orchestrator.
    /// </summary>
    /// <typeparam name="TNext">Output type of the next orchestrator.</typeparam>
    /// <param name="next">The next orchestrator in the chain.</param>
    /// <returns>A composed orchestrator.</returns>
    IComposableOrchestrator<TInput, TNext> Then<TNext>(
        IComposableOrchestrator<TOutput, TNext> next);

    /// <summary>
    /// Maps the output of this orchestrator to a different type.
    /// </summary>
    /// <typeparam name="TNext">The mapped output type.</typeparam>
    /// <param name="mapper">Function to transform the output.</param>
    /// <returns>A composed orchestrator with mapped output.</returns>
    IComposableOrchestrator<TInput, TNext> Map<TNext>(
        Func<TOutput, TNext> mapper);

    /// <summary>
    /// Adds a side effect to execute after successful orchestration.
    /// </summary>
    /// <param name="sideEffect">Action to execute with the output.</param>
    /// <returns>The same orchestrator for chaining.</returns>
    IComposableOrchestrator<TInput, TOutput> Tap(
        Action<TOutput> sideEffect);
}