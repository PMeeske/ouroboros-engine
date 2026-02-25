namespace Ouroboros.Agent;

/// <summary>
/// Extension methods for orchestrator composition.
/// </summary>
public static class OrchestratorCompositionExtensions
{
    /// <summary>
    /// Converts an orchestrator to a composable orchestrator.
    /// </summary>
    public static IComposableOrchestrator<TInput, TOutput> AsComposable<TInput, TOutput>(
        this IOrchestrator<TInput, TOutput> orchestrator)
    {
        return CompositeOrchestrator<TInput, TOutput>.From(orchestrator);
    }

    /// <summary>
    /// Chains this orchestrator with another using Kleisli composition.
    /// </summary>
    public static IComposableOrchestrator<TInput, TNext> Bind<TInput, TOutput, TNext>(
        this IComposableOrchestrator<TInput, TOutput> orchestrator,
        Func<TOutput, Task<TNext>> binder)
    {
        ArgumentNullException.ThrowIfNull(orchestrator);
        ArgumentNullException.ThrowIfNull(binder);

        return orchestrator.Then(
            CompositeOrchestrator<TOutput, TNext>.FromFunc(
                "bound_orchestrator",
                (output, _) => binder(output)));
    }

    /// <summary>
    /// Filters orchestrator execution based on a predicate.
    /// </summary>
    public static IComposableOrchestrator<TInput, TOutput?> Where<TInput, TOutput>(
        this IComposableOrchestrator<TInput, TOutput> orchestrator,
        Func<TOutput, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(orchestrator);
        ArgumentNullException.ThrowIfNull(predicate);

        return orchestrator.Then(
            CompositeOrchestrator<TOutput, TOutput?>.FromFunc(
                "filtered_orchestrator",
                (output, _) => Task.FromResult(predicate(output) ? output : default(TOutput?))));
    }
}