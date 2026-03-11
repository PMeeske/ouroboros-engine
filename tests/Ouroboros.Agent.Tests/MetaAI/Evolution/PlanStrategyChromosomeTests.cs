using FluentAssertions;
using Ouroboros.Agent.MetaAI.Evolution;
using Xunit;

namespace Ouroboros.Tests.MetaAI.Evolution;

[Trait("Category", "Unit")]
public class PlanStrategyChromosomeTests
{
    [Fact]
    public void Constructor_SetsGenesAndFitness()
    {
        var genes = new List<PlanStrategyGene>
        {
            new("TestGene", 0.5, "desc")
        };

        var chromosome = new PlanStrategyChromosome(genes, 0.75);

        chromosome.Genes.Should().HaveCount(1);
        chromosome.Fitness.Should().Be(0.75);
    }

    [Fact]
    public void Constructor_WithNullGenes_ThrowsArgumentNullException()
    {
        var act = () => new PlanStrategyChromosome(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_DefaultFitness_IsZero()
    {
        var genes = new List<PlanStrategyGene> { new("g", 0.5, "d") };

        var chromosome = new PlanStrategyChromosome(genes);

        chromosome.Fitness.Should().Be(0.0);
    }

    [Fact]
    public void CreateDefault_HasFourGenes()
    {
        var chromosome = PlanStrategyChromosome.CreateDefault();

        chromosome.Genes.Should().HaveCount(4);
        chromosome.Fitness.Should().Be(0.0);
    }

    [Fact]
    public void CreateDefault_ContainsExpectedStrategies()
    {
        var chromosome = PlanStrategyChromosome.CreateDefault();
        var names = chromosome.Genes.Select(g => g.StrategyName).ToList();

        names.Should().Contain("PlanningDepth");
        names.Should().Contain("ToolVsLLMWeight");
        names.Should().Contain("VerificationStrictness");
        names.Should().Contain("DecompositionGranularity");
    }

    [Fact]
    public void CreateRandom_HasFourGenes()
    {
        var chromosome = PlanStrategyChromosome.CreateRandom(new Random(42));

        chromosome.Genes.Should().HaveCount(4);
        chromosome.Genes.Should().OnlyContain(g => g.Weight >= 0.0 && g.Weight <= 1.0);
    }

    [Fact]
    public void WithGenes_CreatesNewChromosomeWithSameFitness()
    {
        var original = new PlanStrategyChromosome(
            new List<PlanStrategyGene> { new("A", 0.5, "d") }, 0.8);
        var newGenes = new List<PlanStrategyGene> { new("B", 0.3, "d") };

        var result = original.WithGenes(newGenes);

        result.Genes.Should().HaveCount(1);
        result.Genes[0].StrategyName.Should().Be("B");
        result.Fitness.Should().Be(0.8);
    }

    [Fact]
    public void WithFitness_CreatesNewChromosomeWithSameGenes()
    {
        var original = new PlanStrategyChromosome(
            new List<PlanStrategyGene> { new("A", 0.5, "d") }, 0.3);

        var result = original.WithFitness(0.95);

        result.Genes.Should().HaveCount(1);
        result.Genes[0].StrategyName.Should().Be("A");
        result.Fitness.Should().Be(0.95);
    }

    [Fact]
    public void GetGene_WithExistingName_ReturnsGene()
    {
        var chromosome = PlanStrategyChromosome.CreateDefault();

        var gene = chromosome.GetGene("PlanningDepth");

        gene.Should().NotBeNull();
        gene!.StrategyName.Should().Be("PlanningDepth");
    }

    [Fact]
    public void GetGene_WithNonExistentName_ReturnsNull()
    {
        var chromosome = PlanStrategyChromosome.CreateDefault();

        var gene = chromosome.GetGene("NonExistent");

        gene.Should().BeNull();
    }

    [Fact]
    public void ToString_ContainsFitnessAndGenes()
    {
        var chromosome = PlanStrategyChromosome.CreateDefault();

        var str = chromosome.ToString();

        str.Should().Contain("PlanStrategyChromosome");
        str.Should().Contain("Fitness=");
        str.Should().Contain("PlanningDepth=");
    }
}
