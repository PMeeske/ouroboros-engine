using Ouroboros.Providers.Resilience;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class ToolCallMutationGeneTests
{
    [Fact]
    public void Gene_ValidWeight_IsValid()
    {
        var gene = ToolCallMutationGene.Parameters.FormatHintAggression(0.5);

        gene.IsValid().Should().BeTrue();
        gene.ParameterName.Should().Be("FormatHintAggression");
        gene.Weight.Should().Be(0.5);
    }

    [Fact]
    public void Gene_InvalidWeight_IsNotValid()
    {
        var gene = new ToolCallMutationGene("test", 1.5, "too high");

        gene.IsValid().Should().BeFalse();
    }

    [Fact]
    public void Gene_Mutate_StaysInBounds()
    {
        var gene = ToolCallMutationGene.Parameters.TemperatureAmplitude(0.5);
        var random = new Random(42);

        var mutated = gene.Mutate(random);

        mutated.Weight.Should().BeGreaterThanOrEqualTo(0.0);
        mutated.Weight.Should().BeLessThanOrEqualTo(1.0);
        mutated.ParameterName.Should().Be(gene.ParameterName);
    }

    [Fact]
    public void Gene_Mutate_AtUpperBound_StaysClamped()
    {
        var gene = new ToolCallMutationGene("test", 1.0, "at max");
        var random = new Random(42);

        var mutated = gene.Mutate(random, mutationRate: 0.5);

        mutated.Weight.Should().BeLessThanOrEqualTo(1.0);
        mutated.Weight.Should().BeGreaterThanOrEqualTo(0.0);
    }

    [Fact]
    public void Gene_Mutate_AtLowerBound_StaysClamped()
    {
        var gene = new ToolCallMutationGene("test", 0.0, "at min");
        var random = new Random(42);

        var mutated = gene.Mutate(random, mutationRate: 0.5);

        mutated.Weight.Should().BeGreaterThanOrEqualTo(0.0);
        mutated.Weight.Should().BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public void Gene_AllFactoryMethods_ProduceValidGenes()
    {
        var genes = new[]
        {
            ToolCallMutationGene.Parameters.FormatHintAggression(0.5),
            ToolCallMutationGene.Parameters.TemperatureAmplitude(0.3),
            ToolCallMutationGene.Parameters.SimplificationRate(0.4),
            ToolCallMutationGene.Parameters.FormatSwitchPreference(0.6),
            ToolCallMutationGene.Parameters.LlmVariatorWeight(0.2),
        };

        genes.Should().AllSatisfy(g => g.IsValid().Should().BeTrue());
    }
}
