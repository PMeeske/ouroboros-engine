using FluentAssertions;
using Ouroboros.Agent.MetaAI.Evolution;
using Xunit;

namespace Ouroboros.Tests.MetaAI.Evolution;

[Trait("Category", "Unit")]
public class PlanStrategyGeneTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var gene = new PlanStrategyGene("TestStrategy", 0.7, "A test strategy");

        gene.StrategyName.Should().Be("TestStrategy");
        gene.Weight.Should().Be(0.7);
        gene.Description.Should().Be("A test strategy");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void IsValid_WithValidWeight_ReturnsTrue(double weight)
    {
        var gene = new PlanStrategyGene("Valid", weight, "desc");

        gene.IsValid().Should().BeTrue();
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(2.0)]
    public void IsValid_WithInvalidWeight_ReturnsFalse(double weight)
    {
        var gene = new PlanStrategyGene("Valid", weight, "desc");

        gene.IsValid().Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValid_WithInvalidName_ReturnsFalse(string? name)
    {
        var gene = new PlanStrategyGene(name!, 0.5, "desc");

        gene.IsValid().Should().BeFalse();
    }

    [Fact]
    public void Mutate_ReturnsGeneWithinValidRange()
    {
        var gene = new PlanStrategyGene("Test", 0.5, "desc");
        var random = new Random(42);

        var mutated = gene.Mutate(random);

        mutated.Weight.Should().BeInRange(0.0, 1.0);
        mutated.StrategyName.Should().Be("Test");
        mutated.Description.Should().Be("desc");
    }

    [Fact]
    public void Mutate_AtLowerBound_StaysInRange()
    {
        var gene = new PlanStrategyGene("Test", 0.0, "desc");
        var random = new Random(42);

        for (int i = 0; i < 100; i++)
        {
            var mutated = gene.Mutate(random);
            mutated.Weight.Should().BeInRange(0.0, 1.0);
        }
    }

    [Fact]
    public void Mutate_AtUpperBound_StaysInRange()
    {
        var gene = new PlanStrategyGene("Test", 1.0, "desc");
        var random = new Random(42);

        for (int i = 0; i < 100; i++)
        {
            var mutated = gene.Mutate(random);
            mutated.Weight.Should().BeInRange(0.0, 1.0);
        }
    }

    [Fact]
    public void Mutate_WithCustomMutationRate_ProducesVariation()
    {
        var gene = new PlanStrategyGene("Test", 0.5, "desc");
        var random = new Random(42);

        var mutated = gene.Mutate(random, mutationRate: 0.5);

        mutated.Weight.Should().NotBe(gene.Weight);
    }

    [Fact]
    public void Strategies_PlanningDepth_CreatesCorrectGene()
    {
        var gene = PlanStrategyGene.Strategies.PlanningDepth(0.8);

        gene.StrategyName.Should().Be("PlanningDepth");
        gene.Weight.Should().Be(0.8);
        gene.Description.Should().Contain("decompose");
    }

    [Fact]
    public void Strategies_ToolVsLLMWeight_CreatesCorrectGene()
    {
        var gene = PlanStrategyGene.Strategies.ToolVsLLMWeight(0.3);

        gene.StrategyName.Should().Be("ToolVsLLMWeight");
        gene.Weight.Should().Be(0.3);
        gene.Description.Should().Contain("tool");
    }

    [Fact]
    public void Strategies_VerificationStrictness_CreatesCorrectGene()
    {
        var gene = PlanStrategyGene.Strategies.VerificationStrictness(0.9);

        gene.StrategyName.Should().Be("VerificationStrictness");
        gene.Weight.Should().Be(0.9);
        gene.Description.Should().Contain("strict");
    }

    [Fact]
    public void Strategies_DecompositionGranularity_CreatesCorrectGene()
    {
        var gene = PlanStrategyGene.Strategies.DecompositionGranularity(0.6);

        gene.StrategyName.Should().Be("DecompositionGranularity");
        gene.Weight.Should().Be(0.6);
        gene.Description.Should().Contain("step");
    }

    [Fact]
    public void RecordEquality_WithSameValues_AreEqual()
    {
        var a = new PlanStrategyGene("Test", 0.5, "desc");
        var b = new PlanStrategyGene("Test", 0.5, "desc");

        a.Should().Be(b);
    }
}
