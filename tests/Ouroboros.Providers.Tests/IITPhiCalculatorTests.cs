using Polly;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class IITPhiCalculatorTests
{
    private static NeuralPathway CreatePathway(
        string name,
        PathwayTier tier = PathwayTier.CloudLight,
        int synapses = 100,
        int activations = 90,
        double weight = 1.0,
        HashSet<SubGoalType>? specializations = null)
    {
        var breaker = Policy
            .Handle<Exception>()
            .CircuitBreakerAsync(5, TimeSpan.FromMinutes(1));

        return new NeuralPathway
        {
            Name = name,
            Tier = tier,
            Synapses = synapses,
            Activations = activations,
            Weight = weight,
            Specializations = specializations ?? new HashSet<SubGoalType>(),
            CircuitBreaker = breaker,
        };
    }

    [Fact]
    public void Compute_EmptyPathways_ReturnsEmpty()
    {
        var calc = new IITPhiCalculator();

        var result = calc.Compute(Array.Empty<NeuralPathway>());

        result.Should().Be(PhiResult.Empty);
    }

    [Fact]
    public void Compute_SinglePathway_ReturnsZeroPhi()
    {
        var calc = new IITPhiCalculator();
        var pathways = new[] { CreatePathway("p1") };

        var result = calc.Compute(pathways);

        result.Phi.Should().Be(0.0);
        result.PartitionA.Should().HaveCount(1);
        result.PartitionB.Should().BeEmpty();
    }

    [Fact]
    public void Compute_TwoPathways_ReturnsSomePhiValue()
    {
        var calc = new IITPhiCalculator();
        var pathways = new[]
        {
            CreatePathway("gpt-4", PathwayTier.CloudPremium, 100, 90, 1.0,
                new HashSet<SubGoalType> { SubGoalType.Reasoning }),
            CreatePathway("claude", PathwayTier.CloudPremium, 100, 85, 1.0,
                new HashSet<SubGoalType> { SubGoalType.Reasoning }),
        };

        var result = calc.Compute(pathways);

        result.Phi.Should().BeGreaterThanOrEqualTo(0.0);
        result.PartitionA.Should().NotBeEmpty();
        result.PartitionB.Should().NotBeEmpty();
        result.MinimumInformationPartition.Should().NotBeNullOrWhiteSpace();
        result.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Compute_DiversePathways_ReturnsResult()
    {
        var calc = new IITPhiCalculator();
        var pathways = new[]
        {
            CreatePathway("local", PathwayTier.Local, 50, 45, 0.8,
                new HashSet<SubGoalType> { SubGoalType.Retrieval }),
            CreatePathway("cloud-light", PathwayTier.CloudLight, 80, 70, 1.0,
                new HashSet<SubGoalType> { SubGoalType.Transform }),
            CreatePathway("premium", PathwayTier.CloudPremium, 100, 95, 1.5,
                new HashSet<SubGoalType> { SubGoalType.Reasoning, SubGoalType.Creative }),
        };

        var result = calc.Compute(pathways);

        result.Phi.Should().BeGreaterThanOrEqualTo(0.0);
        result.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Compute_PathwaysWithZeroSynapses_DoesNotThrow()
    {
        var calc = new IITPhiCalculator();
        var pathways = new[]
        {
            CreatePathway("idle1", synapses: 0, activations: 0),
            CreatePathway("idle2", synapses: 0, activations: 0),
        };

        var result = calc.Compute(pathways);

        result.Phi.Should().BeGreaterThanOrEqualTo(0.0);
    }

    [Fact]
    public void Compute_HighlyIntegrated_ReturnsHigherPhi()
    {
        var calc = new IITPhiCalculator();
        // Same tier, same specializations, same weight => high coupling
        var specs = new HashSet<SubGoalType> { SubGoalType.Reasoning, SubGoalType.Coding };
        var pathways = new[]
        {
            CreatePathway("a", PathwayTier.Specialized, 100, 90, 1.0, new HashSet<SubGoalType>(specs)),
            CreatePathway("b", PathwayTier.Specialized, 100, 85, 1.0, new HashSet<SubGoalType>(specs)),
        };

        var integrated = calc.Compute(pathways);

        // Dissimilar pathways => low coupling
        var pathways2 = new[]
        {
            CreatePathway("x", PathwayTier.Local, 100, 50, 0.3, new HashSet<SubGoalType> { SubGoalType.Retrieval }),
            CreatePathway("y", PathwayTier.CloudPremium, 100, 95, 2.0, new HashSet<SubGoalType> { SubGoalType.Creative }),
        };

        var dissimilar = calc.Compute(pathways2);

        // Integrated system should have higher Phi than dissimilar
        integrated.Phi.Should().BeGreaterThanOrEqualTo(dissimilar.Phi);
    }

    [Fact]
    public void PhiResult_Empty_HasExpectedValues()
    {
        PhiResult.Empty.Phi.Should().Be(0.0);
        PhiResult.Empty.PartitionA.Should().BeEmpty();
        PhiResult.Empty.PartitionB.Should().BeEmpty();
    }
}
