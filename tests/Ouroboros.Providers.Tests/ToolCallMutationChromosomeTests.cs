using Ouroboros.Providers.Resilience;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class ToolCallMutationChromosomeTests
{
    [Fact]
    public void CreateDefault_ReturnsChromosomeWithAllGenes()
    {
        var chromosome = ToolCallMutationChromosome.CreateDefault();

        chromosome.Genes.Should().HaveCount(5);
        chromosome.Fitness.Should().Be(0.0);
        chromosome.GetGene("FormatHintAggression").Should().NotBeNull();
        chromosome.GetGene("TemperatureAmplitude").Should().NotBeNull();
        chromosome.GetGene("SimplificationRate").Should().NotBeNull();
        chromosome.GetGene("FormatSwitchPreference").Should().NotBeNull();
        chromosome.GetGene("LlmVariatorWeight").Should().NotBeNull();
    }

    [Fact]
    public void CreateRandom_ReturnsChromosomeWithValidWeights()
    {
        var random = new Random(42);
        var chromosome = ToolCallMutationChromosome.CreateRandom(random);

        chromosome.Genes.Should().HaveCount(5);
        chromosome.Genes.Should().AllSatisfy(g =>
        {
            g.Weight.Should().BeGreaterThanOrEqualTo(0.0);
            g.Weight.Should().BeLessThanOrEqualTo(1.0);
        });
    }

    [Fact]
    public void WithFitness_ReturnsNewChromosomeWithUpdatedFitness()
    {
        var original = ToolCallMutationChromosome.CreateDefault();

        var updated = original.WithFitness(0.85);

        updated.Fitness.Should().Be(0.85);
        updated.Genes.Should().BeEquivalentTo(original.Genes);
        updated.Should().NotBeSameAs(original);
    }

    [Fact]
    public void WithGenes_ReturnsNewChromosomeWithUpdatedGenes()
    {
        var original = ToolCallMutationChromosome.CreateDefault();
        var newGenes = new List<ToolCallMutationGene>
        {
            ToolCallMutationGene.Parameters.FormatHintAggression(0.9),
        };

        var updated = original.WithGenes(newGenes);

        updated.Genes.Should().HaveCount(1);
        updated.Fitness.Should().Be(original.Fitness);
    }

    [Fact]
    public void MutateAll_ProducesDifferentChromosome()
    {
        var original = ToolCallMutationChromosome.CreateDefault();
        var random = new Random(42);

        var mutated = original.MutateAll(random, mutationRate: 0.3);

        mutated.Should().NotBeSameAs(original);
        mutated.Genes.Should().HaveCount(original.Genes.Count);
        // At least some genes should have changed
        bool anyDifferent = original.Genes
            .Zip(mutated.Genes, (a, b) => Math.Abs(a.Weight - b.Weight) > 0.001)
            .Any(d => d);
        anyDifferent.Should().BeTrue("mutation with rate 0.3 should change at least one gene");
    }

    [Fact]
    public void Crossover_ProducesTwoChildren()
    {
        var parent1 = ToolCallMutationChromosome.CreateDefault();
        var parent2 = ToolCallMutationChromosome.CreateRandom(new Random(42));
        var random = new Random(123);

        var (child1, child2) = parent1.Crossover(parent2, random);

        child1.Genes.Should().HaveCount(5);
        child2.Genes.Should().HaveCount(5);
        child1.Should().NotBeSameAs(parent1);
        child2.Should().NotBeSameAs(parent2);
    }

    [Fact]
    public void GetGene_NonExistent_ReturnsNull()
    {
        var chromosome = ToolCallMutationChromosome.CreateDefault();

        chromosome.GetGene("NonExistentGene").Should().BeNull();
    }

    [Fact]
    public void ToString_ContainsFitnessAndGeneInfo()
    {
        var chromosome = ToolCallMutationChromosome.CreateDefault().WithFitness(0.75);

        var str = chromosome.ToString();

        str.Should().Contain("Fitness=0.750");
        str.Should().Contain("FormatHintAggression");
    }
}
