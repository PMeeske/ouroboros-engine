// <copyright file="PlanStrategyGene.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Agent.MetaAI.Evolution;

/// <summary>
/// Represents a gene encoding a planning strategy parameter that can be evolved.
/// Genes are the atomic units of evolution in the genetic algorithm.
/// </summary>
/// <param name="StrategyName">The name of the strategy parameter (e.g., "PlanningDepth", "ToolVsLLMWeight").</param>
/// <param name="Weight">The numerical value of this parameter (0.0 to 1.0).</param>
/// <param name="Description">Human-readable description of what this parameter controls.</param>
public sealed record PlanStrategyGene(
    string StrategyName,
    double Weight,
    string Description)
{
    /// <summary>
    /// Validates that the weight is within the valid range.
    /// </summary>
    /// <returns>True if the gene is valid.</returns>
    public bool IsValid() => Weight is >= 0.0 and <= 1.0 && !string.IsNullOrWhiteSpace(StrategyName);

    /// <summary>
    /// Creates a mutated version of this gene with a small random variation.
    /// </summary>
    /// <param name="random">Random number generator.</param>
    /// <param name="mutationRate">Maximum mutation amount (default 0.1).</param>
    /// <returns>A new gene with mutated weight.</returns>
    public PlanStrategyGene Mutate(Random random, double mutationRate = 0.1)
    {
        double variation = (random.NextDouble() - 0.5) * 2 * mutationRate;
        double newWeight = Math.Clamp(Weight + variation, 0.0, 1.0);
        return this with { Weight = newWeight };
    }

    /// <summary>
    /// Creates a gene with predefined strategy types.
    /// </summary>
    public static class Strategies
    {
        /// <summary>
        /// Preference for deeper planning (more steps, more decomposition).
        /// Higher values = more planning depth. Range: 0.0 (shallow) to 1.0 (deep).
        /// </summary>
        public static PlanStrategyGene PlanningDepth(double weight) =>
            new("PlanningDepth", weight, "Controls how deeply to decompose goals (0=shallow, 1=deep)");

        /// <summary>
        /// Routing weight between tool usage and LLM reasoning.
        /// Higher values = prefer tools. Range: 0.0 (prefer LLM) to 1.0 (prefer tools).
        /// </summary>
        public static PlanStrategyGene ToolVsLLMWeight(double weight) =>
            new("ToolVsLLMWeight", weight, "Balance between tool execution and LLM reasoning (0=LLM, 1=tools)");

        /// <summary>
        /// Verification strictness level.
        /// Higher values = more rigorous verification. Range: 0.0 (lenient) to 1.0 (strict).
        /// </summary>
        public static PlanStrategyGene VerificationStrictness(double weight) =>
            new("VerificationStrictness", weight, "How strict to be in result verification (0=lenient, 1=strict)");

        /// <summary>
        /// Decomposition granularity (size of plan steps).
        /// Higher values = finer-grained steps. Range: 0.0 (coarse) to 1.0 (fine).
        /// </summary>
        public static PlanStrategyGene DecompositionGranularity(double weight) =>
            new("DecompositionGranularity", weight, "Size of plan steps (0=large steps, 1=small steps)");
    }
}
