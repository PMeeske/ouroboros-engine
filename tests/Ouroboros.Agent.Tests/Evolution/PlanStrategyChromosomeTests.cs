// <copyright file="PlanStrategyChromosomeTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Ouroboros.Agent.MetaAI.Evolution;
using Ouroboros.Genetic.Abstractions;

namespace Ouroboros.Tests.Evolution;

/// <summary>
/// Unit tests for PlanStrategyChromosome IChromosome implementation.
/// </summary>
[Trait("Category", "Unit")]
public class PlanStrategyChromosomeTests
{
    [Fact]
    public void Constructor_WithGenes_CreatesChromosome()
    {
        // Arrange
        var genes = new List<PlanStrategyGene>
        {
            PlanStrategyGene.Strategies.PlanningDepth(0.5),
            PlanStrategyGene.Strategies.ToolVsLLMWeight(0.7),
        };

        // Act
        var chromosome = new PlanStrategyChromosome(genes);

        // Assert
        chromosome.Genes.Should().HaveCount(2);
        chromosome.Genes.Should().BeEquivalentTo(genes);
        chromosome.Fitness.Should().Be(0.0); // Default fitness
    }

    [Fact]
    public void Constructor_WithGenesAndFitness_CreatesChromosome()
    {
        // Arrange
        var genes = new List<PlanStrategyGene>
        {
            PlanStrategyGene.Strategies.PlanningDepth(0.5),
        };

        // Act
        var chromosome = new PlanStrategyChromosome(genes, fitness: 0.75);

        // Assert
        chromosome.Genes.Should().HaveCount(1);
        chromosome.Fitness.Should().Be(0.75);
    }

    [Fact]
    public void Constructor_WithNullGenes_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new PlanStrategyChromosome(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("genes");
    }

    [Fact]
    public void WithGenes_ReturnsNewInstanceWithCorrectGenes()
    {
        // Arrange
        var originalGenes = new List<PlanStrategyGene>
        {
            PlanStrategyGene.Strategies.PlanningDepth(0.5),
        };
        var chromosome = new PlanStrategyChromosome(originalGenes, fitness: 0.8);

        var newGenes = new List<PlanStrategyGene>
        {
            PlanStrategyGene.Strategies.ToolVsLLMWeight(0.7),
            PlanStrategyGene.Strategies.VerificationStrictness(0.6),
        };

        // Act
        IChromosome<PlanStrategyGene> newChromosome = chromosome.WithGenes(newGenes);

        // Assert
        newChromosome.Genes.Should().HaveCount(2);
        newChromosome.Genes.Should().BeEquivalentTo(newGenes);
        newChromosome.Fitness.Should().Be(0.8); // Fitness preserved
        chromosome.Genes.Should().HaveCount(1); // Original unchanged
    }

    [Fact]
    public void WithFitness_ReturnsNewInstanceWithUpdatedFitness()
    {
        // Arrange
        var genes = new List<PlanStrategyGene>
        {
            PlanStrategyGene.Strategies.PlanningDepth(0.5),
        };
        var chromosome = new PlanStrategyChromosome(genes, fitness: 0.5);

        // Act
        IChromosome<PlanStrategyGene> newChromosome = chromosome.WithFitness(0.9);

        // Assert
        newChromosome.Fitness.Should().Be(0.9);
        newChromosome.Genes.Should().BeEquivalentTo(genes); // Genes preserved
        chromosome.Fitness.Should().Be(0.5); // Original unchanged
    }

    [Fact]
    public void Genes_ReturnsExpectedGeneList()
    {
        // Arrange
        var genes = new List<PlanStrategyGene>
        {
            PlanStrategyGene.Strategies.PlanningDepth(0.5),
            PlanStrategyGene.Strategies.ToolVsLLMWeight(0.7),
            PlanStrategyGene.Strategies.VerificationStrictness(0.6),
        };
        var chromosome = new PlanStrategyChromosome(genes);

        // Act
        IReadOnlyList<PlanStrategyGene> result = chromosome.Genes;

        // Assert
        result.Should().HaveCount(3);
        result[0].StrategyName.Should().Be("PlanningDepth");
        result[1].StrategyName.Should().Be("ToolVsLLMWeight");
        result[2].StrategyName.Should().Be("VerificationStrictness");
    }

    [Fact]
    public void Fitness_DefaultsToZero()
    {
        // Arrange
        var genes = new List<PlanStrategyGene>
        {
            PlanStrategyGene.Strategies.PlanningDepth(0.5),
        };

        // Act
        var chromosome = new PlanStrategyChromosome(genes);

        // Assert
        chromosome.Fitness.Should().Be(0.0);
    }

    [Fact]
    public void CreateDefault_CreatesChromosomeWithFourGenes()
    {
        // Act
        var chromosome = PlanStrategyChromosome.CreateDefault();

        // Assert
        chromosome.Genes.Should().HaveCount(4);
        chromosome.GetGene("PlanningDepth").Should().NotBeNull();
        chromosome.GetGene("ToolVsLLMWeight").Should().NotBeNull();
        chromosome.GetGene("VerificationStrictness").Should().NotBeNull();
        chromosome.GetGene("DecompositionGranularity").Should().NotBeNull();
    }

    [Fact]
    public void CreateRandom_CreatesChromosomeWithRandomWeights()
    {
        // Arrange
        var random = new Random(42);

        // Act
        var chromosome = PlanStrategyChromosome.CreateRandom(random);

        // Assert
        chromosome.Genes.Should().HaveCount(4);
        foreach (var gene in chromosome.Genes)
        {
            gene.Weight.Should().BeInRange(0.0, 1.0);
        }
    }

    [Fact]
    public void CreateRandom_WithDifferentSeeds_CreatesDifferentChromosomes()
    {
        // Arrange
        var random1 = new Random(42);
        var random2 = new Random(99);

        // Act
        var chromosome1 = PlanStrategyChromosome.CreateRandom(random1);
        var chromosome2 = PlanStrategyChromosome.CreateRandom(random2);

        // Assert
        chromosome1.Genes.Should().NotBeEquivalentTo(chromosome2.Genes);
    }

    [Fact]
    public void GetGene_WithExistingStrategy_ReturnsGene()
    {
        // Arrange
        var chromosome = PlanStrategyChromosome.CreateDefault();

        // Act
        var gene = chromosome.GetGene("PlanningDepth");

        // Assert
        gene.Should().NotBeNull();
        gene!.StrategyName.Should().Be("PlanningDepth");
    }

    [Fact]
    public void GetGene_WithNonExistingStrategy_ReturnsNull()
    {
        // Arrange
        var chromosome = PlanStrategyChromosome.CreateDefault();

        // Act
        var gene = chromosome.GetGene("NonExistent");

        // Assert
        gene.Should().BeNull();
    }

    [Fact]
    public void ToString_IncludesFitnessAndGenes()
    {
        // Arrange
        var genes = new List<PlanStrategyGene>
        {
            PlanStrategyGene.Strategies.PlanningDepth(0.5),
        };
        var chromosome = new PlanStrategyChromosome(genes, fitness: 0.75);

        // Act
        string result = chromosome.ToString();

        // Assert
        result.Should().Contain("PlanStrategyChromosome");
        result.Should().Contain("Fitness=0.750");
        result.Should().Contain("PlanningDepth");
    }

    [Fact]
    public void ImplementsIChromosome_Interface()
    {
        // Arrange
        var chromosome = PlanStrategyChromosome.CreateDefault();

        // Act & Assert
        chromosome.Should().BeAssignableTo<IChromosome<PlanStrategyGene>>();
    }
}
