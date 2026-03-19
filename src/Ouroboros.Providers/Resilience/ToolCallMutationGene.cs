// <copyright file="ToolCallMutationGene.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Providers.Resilience;

/// <summary>
/// Represents a gene encoding an evolvable parameter for tool call mutation strategies.
/// Analogous to <c>PlanStrategyGene</c> in <c>Ouroboros.Agent.MetaAI.Evolution</c>.
/// </summary>
/// <param name="ParameterName">The name of the parameter being evolved.</param>
/// <param name="Weight">The numerical value of this parameter (0.0 to 1.0).</param>
/// <param name="Description">Human-readable description of what this parameter controls.</param>
public sealed record ToolCallMutationGene(
    string ParameterName,
    double Weight,
    string Description)
{
    /// <summary>
    /// Validates that the weight is within the valid range.
    /// </summary>
    public bool IsValid() => Weight is >= 0.0 and <= 1.0 && !string.IsNullOrWhiteSpace(ParameterName);

    /// <summary>
    /// Creates a mutated version of this gene with a small random variation.
    /// </summary>
    /// <param name="random">Random number generator.</param>
    /// <param name="mutationRate">Maximum mutation amount (default 0.1).</param>
    /// <returns>A new gene with mutated weight.</returns>
    public ToolCallMutationGene Mutate(System.Random random, double mutationRate = 0.1)
    {
        double variation = (random.NextDouble() - 0.5) * 2 * mutationRate;
        double newWeight = Math.Clamp(Weight + variation, 0.0, 1.0);
        return this with { Weight = newWeight };
    }

    /// <summary>
    /// Factory methods for standard tool call mutation genes.
    /// </summary>
    public static class Parameters
    {
        /// <summary>
        /// Controls how aggressively to modify the prompt with format hints.
        /// Higher = more format instruction injection.
        /// </summary>
        public static ToolCallMutationGene FormatHintAggression(double weight) =>
            new("FormatHintAggression", weight, "How aggressively to inject format hints (0=subtle, 1=explicit)");

        /// <summary>
        /// Temperature adjustment amplitude per generation.
        /// Higher = larger temperature swings during mutation.
        /// </summary>
        public static ToolCallMutationGene TemperatureAmplitude(double weight) =>
            new("TemperatureAmplitude", weight, "Amplitude of temperature changes (0=small, 1=large)");

        /// <summary>
        /// Tool simplification aggressiveness.
        /// Higher = more aggressive tool count reduction.
        /// </summary>
        public static ToolCallMutationGene SimplificationRate(double weight) =>
            new("SimplificationRate", weight, "How aggressively to reduce tool definitions (0=keep most, 1=minimize)");

        /// <summary>
        /// Preference for trying format switches before other mutations.
        /// Higher = try format switching earlier in the mutation pipeline.
        /// </summary>
        public static ToolCallMutationGene FormatSwitchPreference(double weight) =>
            new("FormatSwitchPreference", weight, "Preference for format switching over other strategies (0=last resort, 1=first choice)");

        /// <summary>
        /// LLM variator usage weight — how often to use LLM-based prompt rephrasing.
        /// Higher = use LLM variator more frequently.
        /// </summary>
        public static ToolCallMutationGene LlmVariatorWeight(double weight) =>
            new("LlmVariatorWeight", weight, "How often to use LLM-based prompt rephrasing (0=never, 1=always)");
    }
}
