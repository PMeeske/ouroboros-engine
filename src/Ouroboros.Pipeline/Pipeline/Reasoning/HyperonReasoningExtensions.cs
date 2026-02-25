using Ouroboros.Core.Hyperon;
using Ouroboros.Tools.MeTTa;

namespace Ouroboros.Pipeline.Reasoning;

/// <summary>
/// Extension methods for integrating Hyperon reasoning into pipelines.
/// </summary>
public static class HyperonReasoningExtensions
{
    /// <summary>
    /// Adds Hyperon reasoning to a pipeline context.
    /// </summary>
    /// <typeparam name="T">The context type.</typeparam>
    /// <param name="context">The pipeline context.</param>
    /// <param name="stepName">Name of the reasoning step.</param>
    /// <param name="reasoningAction">The reasoning to perform.</param>
    /// <returns>The modified context.</returns>
    public static async Task<T> WithHyperonReasoningAsync<T>(
        this T context,
        string stepName,
        Func<HyperonMeTTaEngine, T, Task<T>> reasoningAction)
        where T : class
    {
        using var step = new HyperonReasoningStep(stepName);
        var arrow = step.CreateArrow(reasoningAction);
        return await arrow(context);
    }

    /// <summary>
    /// Creates a reasoning step that enriches context with symbolic inference.
    /// </summary>
    /// <typeparam name="T">The context type.</typeparam>
    /// <param name="stepName">Name of the step.</param>
    /// <param name="knowledgeBase">MeTTa knowledge base to load.</param>
    /// <param name="queries">Queries to execute.</param>
    /// <param name="contextEnricher">Function to enrich context with results.</param>
    /// <returns>A step function.</returns>
    public static Func<T, Task<T>> CreateInferenceStep<T>(
        string stepName,
        string knowledgeBase,
        IEnumerable<string> queries,
        Func<T, IReadOnlyList<string>, T> contextEnricher)
        where T : class
    {
        return async context =>
        {
            using var step = new HyperonReasoningStep(stepName);

            // Load knowledge base
            await step.Engine.LoadMeTTaSourceAsync(knowledgeBase);

            // Execute queries and collect results
            var allResults = new List<string>();
            foreach (var query in queries)
            {
                var results = await step.InferAsync(query);
                allResults.AddRange(results);
            }

            // Enrich context
            return contextEnricher(context, allResults);
        };
    }

    /// <summary>
    /// Creates a pattern-matching step.
    /// </summary>
    /// <typeparam name="T">The context type.</typeparam>
    /// <param name="stepName">Name of the step.</param>
    /// <param name="pattern">Pattern to match.</param>
    /// <param name="onMatch">Action on match.</param>
    /// <param name="onNoMatch">Action on no match.</param>
    /// <returns>A step function.</returns>
    public static Func<T, Task<T>> CreatePatternStep<T>(
        string stepName,
        string pattern,
        Func<T, Substitution, T> onMatch,
        Func<T, T>? onNoMatch = null)
        where T : class
    {
        return async context =>
        {
            using HyperonReasoningStep step = new(stepName);

            Result<string, string> result = await step.Engine.ExecuteQueryAsync(
                $"(match &self {pattern} $result)");

            if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Value) && !result.Value.Contains("Empty"))
            {
                // Create empty substitution - in real usage this would be parsed from result
                Substitution substitution = Substitution.Empty;
                return onMatch(context, substitution);
            }

            return onNoMatch?.Invoke(context) ?? context;
        };
    }
}