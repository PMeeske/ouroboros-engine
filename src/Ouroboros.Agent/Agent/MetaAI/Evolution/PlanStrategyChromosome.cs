// <copyright file="PlanStrategyChromosome.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Ouroboros.Genetic.Abstractions;

namespace Ouroboros.Agent.MetaAI.Evolution;

/// <summary>
/// Represents a chromosome encoding a complete planning strategy configuration.
/// Contains genes for: planning depth, tool-vs-LLM routing, verification strictness, decomposition granularity.
/// </summary>
public sealed class PlanStrategyChromosome : IChromosome<PlanStrategyGene>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PlanStrategyChromosome"/> class.
    /// </summary>
    /// <param name="genes">The genes that make up this chromosome.</param>
    /// <param name="fitness">The fitness score of this chromosome.</param>
    public PlanStrategyChromosome(IReadOnlyList<PlanStrategyGene> genes, double fitness = 0.0)
    {
        Genes = genes ?? throw new ArgumentNullException(nameof(genes));
        Fitness = fitness;
    }

    /// <inheritdoc/>
    public IReadOnlyList<PlanStrategyGene> Genes { get; }

    /// <inheritdoc/>
    public double Fitness { get; }

    /// <inheritdoc/>
    public IChromosome<PlanStrategyGene> WithGenes(IReadOnlyList<PlanStrategyGene> genes)
    {
        return new PlanStrategyChromosome(genes, Fitness);
    }

    /// <inheritdoc/>
    public IChromosome<PlanStrategyGene> WithFitness(double fitness)
    {
        return new PlanStrategyChromosome(Genes, fitness);
    }

    /// <summary>
    /// Creates a default chromosome with balanced strategy parameters.
    /// </summary>
    /// <returns>A chromosome with default gene values.</returns>
    public static PlanStrategyChromosome CreateDefault()
    {
        var genes = new List<PlanStrategyGene>
        {
            PlanStrategyGene.Strategies.PlanningDepth(0.5),              // Moderate planning depth
            PlanStrategyGene.Strategies.ToolVsLLMWeight(0.7),            // Prefer tools
            PlanStrategyGene.Strategies.VerificationStrictness(0.6),     // Moderate strictness
            PlanStrategyGene.Strategies.DecompositionGranularity(0.5),   // Medium-sized steps
        };

        return new PlanStrategyChromosome(genes);
    }

    /// <summary>
    /// Creates a chromosome from the current OuroborosAtom capabilities.
    /// Extracts strategy parameters from capabilities and converts them to genes.
    /// </summary>
    /// <param name="atom">The Ouroboros atom to extract parameters from.</param>
    /// <returns>A chromosome based on current atom capabilities.</returns>
    public static PlanStrategyChromosome FromAtom(OuroborosAtom atom)
    {
        ArgumentNullException.ThrowIfNull(atom);

        // Get default chromosome to extract gene names and default weights
        var defaultChromosome = CreateDefault();
        
        // Extract evolved strategy capabilities from atom and create genes
        var genes = new List<PlanStrategyGene>();
        
        foreach (var defaultGene in defaultChromosome.Genes)
        {
            // Look for capability with Strategy_ prefix
            string capabilityName = $"Strategy_{defaultGene.StrategyName}";
            var capability = atom.Capabilities.FirstOrDefault(c => c.Name == capabilityName);
            
            // Use capability's ConfidenceLevel as weight, or fall back to default
            double weight = capability?.ConfidenceLevel ?? defaultGene.Weight;
            
            // Clamp weight to valid range [0.0, 1.0]
            weight = Math.Clamp(weight, 0.0, 1.0);
            
            // Create gene using the appropriate factory method
            PlanStrategyGene gene = defaultGene.StrategyName switch
            {
                "PlanningDepth" => PlanStrategyGene.Strategies.PlanningDepth(weight),
                "ToolVsLLMWeight" => PlanStrategyGene.Strategies.ToolVsLLMWeight(weight),
                "VerificationStrictness" => PlanStrategyGene.Strategies.VerificationStrictness(weight),
                "DecompositionGranularity" => PlanStrategyGene.Strategies.DecompositionGranularity(weight),
                _ => defaultGene with { Weight = weight },
            };
            
            genes.Add(gene);
        }
        
        return new PlanStrategyChromosome(genes);
    }

    /// <summary>
    /// Creates a random chromosome with randomized gene values.
    /// </summary>
    /// <param name="random">Random number generator.</param>
    /// <returns>A chromosome with random gene values.</returns>
    public static PlanStrategyChromosome CreateRandom(Random random)
    {
        var genes = new List<PlanStrategyGene>
        {
            PlanStrategyGene.Strategies.PlanningDepth(random.NextDouble()),
            PlanStrategyGene.Strategies.ToolVsLLMWeight(random.NextDouble()),
            PlanStrategyGene.Strategies.VerificationStrictness(random.NextDouble()),
            PlanStrategyGene.Strategies.DecompositionGranularity(random.NextDouble()),
        };

        return new PlanStrategyChromosome(genes);
    }

    /// <summary>
    /// Gets a specific gene by strategy name.
    /// </summary>
    /// <param name="strategyName">The name of the strategy to retrieve.</param>
    /// <returns>The gene if found, null otherwise.</returns>
    public PlanStrategyGene? GetGene(string strategyName)
    {
        return Genes.FirstOrDefault(g => g.StrategyName == strategyName);
    }

    /// <summary>
    /// Returns a string representation of this chromosome showing all gene values.
    /// </summary>
    /// <returns>A formatted string describing the chromosome.</returns>
    public override string ToString()
    {
        var geneStrings = Genes.Select(g => $"{g.StrategyName}={g.Weight:F2}");
        return $"PlanStrategyChromosome(Fitness={Fitness:F3}, Genes=[{string.Join(", ", geneStrings)}])";
    }
}
