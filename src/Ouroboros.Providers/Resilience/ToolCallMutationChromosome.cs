// <copyright file="ToolCallMutationChromosome.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Providers.Resilience;

/// <summary>
/// A chromosome encoding evolvable parameters for tool call mutation strategies.
/// Analogous to <c>PlanStrategyChromosome</c> from <c>Ouroboros.Agent.MetaAI.Evolution</c>.
/// </summary>
/// <remarks>
/// Each chromosome represents a complete configuration of mutation strategy parameters.
/// The evolutionary retry policy maintains a population of chromosomes and selects
/// the fittest configuration for each retry generation, enabling learned adaptation
/// across retry sessions.
/// <para>
/// Mirrors <c>IChromosome{TGene}</c> from <c>Ouroboros.Genetic.Abstractions</c>
/// (foundation submodule) without creating a direct dependency.
/// </para>
/// </remarks>
public sealed class ToolCallMutationChromosome
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ToolCallMutationChromosome"/> class.
    /// </summary>
    /// <param name="genes">The genes that make up this chromosome.</param>
    /// <param name="fitness">The fitness score (0.0 to 1.0).</param>
    public ToolCallMutationChromosome(IReadOnlyList<ToolCallMutationGene> genes, double fitness = 0.0)
    {
        ArgumentNullException.ThrowIfNull(genes);
        Genes = genes;
        Fitness = fitness;
    }

    /// <summary>
    /// Gets the genes encoding the mutation strategy parameters.
    /// </summary>
    public IReadOnlyList<ToolCallMutationGene> Genes { get; }

    /// <summary>
    /// Gets the fitness score of this chromosome based on historical tool-call success.
    /// </summary>
    public double Fitness { get; }

    /// <summary>
    /// Creates a new chromosome with the same fitness but different genes.
    /// </summary>
    public ToolCallMutationChromosome WithGenes(IReadOnlyList<ToolCallMutationGene> genes) =>
        new(genes, Fitness);

    /// <summary>
    /// Creates a new chromosome with the same genes but different fitness.
    /// </summary>
    public ToolCallMutationChromosome WithFitness(double fitness) =>
        new(Genes, fitness);

    /// <summary>
    /// Creates a default chromosome with balanced parameters.
    /// </summary>
    public static ToolCallMutationChromosome CreateDefault() => new(
    [
        ToolCallMutationGene.Parameters.FormatHintAggression(0.5),
        ToolCallMutationGene.Parameters.TemperatureAmplitude(0.3),
        ToolCallMutationGene.Parameters.SimplificationRate(0.4),
        ToolCallMutationGene.Parameters.FormatSwitchPreference(0.5),
        ToolCallMutationGene.Parameters.LlmVariatorWeight(0.2),
    ]);

    /// <summary>
    /// Creates a chromosome with randomized gene values.
    /// </summary>
    public static ToolCallMutationChromosome CreateRandom(Random random) => new(
    [
        ToolCallMutationGene.Parameters.FormatHintAggression(random.NextDouble()),
        ToolCallMutationGene.Parameters.TemperatureAmplitude(random.NextDouble()),
        ToolCallMutationGene.Parameters.SimplificationRate(random.NextDouble()),
        ToolCallMutationGene.Parameters.FormatSwitchPreference(random.NextDouble()),
        ToolCallMutationGene.Parameters.LlmVariatorWeight(random.NextDouble()),
    ]);

    /// <summary>
    /// Mutates all genes with the given mutation rate, producing a child chromosome.
    /// </summary>
    /// <param name="random">Random number generator.</param>
    /// <param name="mutationRate">Maximum per-gene mutation amplitude (default 0.1).</param>
    /// <returns>A new chromosome with mutated genes.</returns>
    public ToolCallMutationChromosome MutateAll(Random random, double mutationRate = 0.1)
    {
        var mutatedGenes = Genes.Select(g => g.Mutate(random, mutationRate)).ToList();
        return new ToolCallMutationChromosome(mutatedGenes);
    }

    /// <summary>
    /// Performs single-point crossover with another chromosome.
    /// </summary>
    /// <param name="other">The other parent chromosome.</param>
    /// <param name="random">Random number generator.</param>
    /// <returns>Two offspring chromosomes.</returns>
    public (ToolCallMutationChromosome Child1, ToolCallMutationChromosome Child2) Crossover(
        ToolCallMutationChromosome other, Random random)
    {
        int crossoverPoint = random.Next(1, Math.Min(Genes.Count, other.Genes.Count));

        var child1Genes = Genes.Take(crossoverPoint).Concat(other.Genes.Skip(crossoverPoint)).ToList();
        var child2Genes = other.Genes.Take(crossoverPoint).Concat(Genes.Skip(crossoverPoint)).ToList();

        return (new ToolCallMutationChromosome(child1Genes), new ToolCallMutationChromosome(child2Genes));
    }

    /// <summary>
    /// Gets a specific gene by parameter name.
    /// </summary>
    public ToolCallMutationGene? GetGene(string parameterName) =>
        Genes.FirstOrDefault(g => g.ParameterName == parameterName);

    /// <inheritdoc/>
    public override string ToString()
    {
        var geneStrings = Genes.Select(g => $"{g.ParameterName}={g.Weight:F2}");
        return $"ToolCallMutationChromosome(Fitness={Fitness:F3}, Genes=[{string.Join(", ", geneStrings)}])";
    }
}
