// <copyright file="PlanStrategyFitness.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Ouroboros.Genetic.Abstractions;

namespace Ouroboros.Agent.MetaAI.Evolution;

/// <summary>
/// Fitness function for evaluating planning strategy chromosomes.
/// Evaluates fitness based on: success rate of past experiences, average quality scores, execution speed.
/// Uses the OuroborosAtom.Experiences collection to compute fitness from historical performance.
/// </summary>
public sealed class PlanStrategyFitness : IFitnessFunction<PlanStrategyGene>
{
    private readonly OuroborosAtom _atom;
    private readonly double _successRateWeight;
    private readonly double _qualityWeight;
    private readonly double _speedWeight;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlanStrategyFitness"/> class.
    /// </summary>
    /// <param name="atom">The Ouroboros atom containing experiences to evaluate against.</param>
    /// <param name="successRateWeight">Weight for success rate component (default 0.5).</param>
    /// <param name="qualityWeight">Weight for quality score component (default 0.3).</param>
    /// <param name="speedWeight">Weight for execution speed component (default 0.2).</param>
    public PlanStrategyFitness(
        OuroborosAtom atom,
        double successRateWeight = 0.5,
        double qualityWeight = 0.3,
        double speedWeight = 0.2)
    {
        _atom = atom ?? throw new ArgumentNullException(nameof(atom));
        
        // Normalize weights to sum to 1.0
        double totalWeight = successRateWeight + qualityWeight + speedWeight;
        _successRateWeight = successRateWeight / totalWeight;
        _qualityWeight = qualityWeight / totalWeight;
        _speedWeight = speedWeight / totalWeight;
    }

    /// <inheritdoc/>
    public Task<double> EvaluateAsync(IChromosome<PlanStrategyGene> chromosome, CancellationToken cancellationToken)
    {
        try
        {
            var experiences = _atom.Experiences;
            
            if (experiences.Count == 0)
            {
                // No experiences yet - return neutral fitness
                return Task.FromResult(0.5);
            }

            // Extract strategy parameters from chromosome
            var strategyChromosome = chromosome as PlanStrategyChromosome ?? 
                new PlanStrategyChromosome(chromosome.Genes, chromosome.Fitness);

            double planningDepth = strategyChromosome.GetGene("PlanningDepth")?.Weight ?? 0.5;
            double toolWeight = strategyChromosome.GetGene("ToolVsLLMWeight")?.Weight ?? 0.5;
            double verificationStrictness = strategyChromosome.GetGene("VerificationStrictness")?.Weight ?? 0.5;
            double decompositionGranularity = strategyChromosome.GetGene("DecompositionGranularity")?.Weight ?? 0.5;

            // Evaluate success rate component
            double successRate = CalculateSuccessRate(experiences);

            // Evaluate quality score component
            double averageQuality = CalculateAverageQuality(experiences);

            // Evaluate speed component (inverse of execution time)
            double speedScore = CalculateSpeedScore(experiences);

            // Apply strategy preferences as modifiers
            // Higher planning depth correlates with better quality but slower speed
            double depthModifier = 1.0 + (planningDepth - 0.5) * 0.2;
            averageQuality *= depthModifier;
            speedScore *= (2.0 - depthModifier); // Inverse correlation

            // Tool usage tends to improve success rate but may reduce quality in complex reasoning tasks
            double toolModifier = 1.0 + (toolWeight - 0.5) * 0.15;
            successRate *= toolModifier;

            // Verification strictness improves quality but reduces speed
            double verificationModifier = 1.0 + (verificationStrictness - 0.5) * 0.1;
            averageQuality *= verificationModifier;
            speedScore *= (2.0 - verificationModifier);

            // Weighted combination
            double fitness = (successRate * _successRateWeight) +
                           (averageQuality * _qualityWeight) +
                           (speedScore * _speedWeight);

            return Task.FromResult(Math.Clamp(fitness, 0.0, 1.0));
        }
        catch (Exception)
        {
            // On any error, return neutral fitness
            return Task.FromResult(0.5);
        }
    }

    /// <summary>
    /// Calculates the success rate from experiences.
    /// </summary>
    /// <param name="experiences">The experiences to evaluate.</param>
    /// <returns>Success rate between 0.0 and 1.0.</returns>
    private static double CalculateSuccessRate(IReadOnlyList<OuroborosExperience> experiences)
    {
        if (experiences.Count == 0)
        {
            return 0.5;
        }

        int successCount = experiences.Count(e => e.Success);
        return (double)successCount / experiences.Count;
    }

    /// <summary>
    /// Calculates the average quality score from experiences.
    /// </summary>
    /// <param name="experiences">The experiences to evaluate.</param>
    /// <returns>Average quality score between 0.0 and 1.0.</returns>
    private static double CalculateAverageQuality(IReadOnlyList<OuroborosExperience> experiences)
    {
        if (experiences.Count == 0)
        {
            return 0.5;
        }

        // Quality is based on the QualityScore field
        return experiences.Average(e => e.QualityScore);
    }

    /// <summary>
    /// Calculates a normalized speed score from experiences.
    /// Faster executions get higher scores.
    /// </summary>
    /// <param name="experiences">The experiences to evaluate.</param>
    /// <returns>Speed score between 0.0 and 1.0.</returns>
    private static double CalculateSpeedScore(IReadOnlyList<OuroborosExperience> experiences)
    {
        if (experiences.Count == 0)
        {
            return 0.5;
        }

        // TODO: Implement proper speed scoring when execution duration tracking is added to OuroborosExperience
        // Currently, OuroborosExperience only has Timestamp but not execution Duration.
        // 
        // Proposed enhancement:
        //   1. Add Duration field to OuroborosExperience record
        //   2. Track execution time in OuroborosOrchestrator.RunAsync()
        //   3. Calculate speed score here as: 1.0 / (1.0 + normalized_avg_duration)
        //
        // For now, return a neutral-to-positive constant to slightly favor current strategies
        // without penalizing any particular approach.
        return 0.7; // Placeholder: slightly favor current strategies until duration tracking is implemented
    }
}
