using Polly;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class NeuralPathwayTests
{
    private static NeuralPathway CreatePathway(
        string name = "test",
        int synapses = 0,
        int activations = 0,
        int inhibitions = 0,
        double weight = 1.0)
    {
        var breaker = Policy
            .Handle<Exception>()
            .CircuitBreakerAsync(5, TimeSpan.FromMinutes(1));

        return new NeuralPathway
        {
            Name = name,
            CircuitBreaker = breaker,
            Synapses = synapses,
            Activations = activations,
            Inhibitions = inhibitions,
            Weight = weight,
        };
    }

    [Fact]
    public void Defaults_AreCorrect()
    {
        var pathway = CreatePathway();

        pathway.Name.Should().Be("test");
        pathway.Tier.Should().Be(PathwayTier.CloudLight);
        pathway.Specializations.Should().BeEmpty();
        pathway.Synapses.Should().Be(0);
        pathway.Activations.Should().Be(0);
        pathway.Inhibitions.Should().Be(0);
        pathway.LastActivation.Should().BeNull();
        pathway.AverageLatency.Should().Be(TimeSpan.Zero);
        pathway.Weight.Should().Be(1.0);
    }

    [Fact]
    public void IsHealthy_ClosedCircuit_ReturnsTrue()
    {
        var pathway = CreatePathway();

        pathway.IsHealthy.Should().BeTrue();
    }

    [Fact]
    public void ActivationRate_NoRequests_ReturnsOne()
    {
        var pathway = CreatePathway();

        pathway.ActivationRate.Should().Be(1.0);
    }

    [Fact]
    public void ActivationRate_WithRequests_CalculatesCorrectly()
    {
        var pathway = CreatePathway(synapses: 10, activations: 8);

        pathway.ActivationRate.Should().Be(0.8);
    }

    [Fact]
    public void RecordActivation_IncrementsSynapses()
    {
        var pathway = CreatePathway();

        pathway.RecordActivation(TimeSpan.FromMilliseconds(100));

        pathway.Synapses.Should().Be(1);
        pathway.Activations.Should().Be(1);
        pathway.LastActivation.Should().NotBeNull();
    }

    [Fact]
    public void RecordActivation_SetsAverageLatency()
    {
        var pathway = CreatePathway();

        pathway.RecordActivation(TimeSpan.FromMilliseconds(100));

        pathway.AverageLatency.Should().Be(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void RecordActivation_UpdatesMovingAverage()
    {
        var pathway = CreatePathway();

        pathway.RecordActivation(TimeSpan.FromMilliseconds(100));
        pathway.RecordActivation(TimeSpan.FromMilliseconds(200));

        // EMA: 100 * 0.8 + 200 * 0.2 = 120ms
        pathway.AverageLatency.TotalMilliseconds.Should().BeApproximately(120, 1);
    }

    [Fact]
    public void RecordActivation_IncreasesWeight()
    {
        var pathway = CreatePathway(weight: 1.0);

        pathway.RecordActivation(TimeSpan.FromMilliseconds(100));

        pathway.Weight.Should().BeGreaterThan(1.0);
        pathway.Weight.Should().BeApproximately(1.05, 0.001);
    }

    [Fact]
    public void RecordActivation_WeightCappedAtTwo()
    {
        var pathway = CreatePathway(weight: 1.99);

        pathway.RecordActivation(TimeSpan.FromMilliseconds(100));

        pathway.Weight.Should().BeLessThanOrEqualTo(2.0);
    }

    [Fact]
    public void RecordInhibition_IncrementsSynapsesAndInhibitions()
    {
        var pathway = CreatePathway();

        pathway.RecordInhibition();

        pathway.Synapses.Should().Be(1);
        pathway.Inhibitions.Should().Be(1);
    }

    [Fact]
    public void RecordInhibition_DecreasesWeight()
    {
        var pathway = CreatePathway(weight: 1.0);

        pathway.RecordInhibition();

        pathway.Weight.Should().BeLessThan(1.0);
        pathway.Weight.Should().BeApproximately(0.7, 0.001);
    }

    [Fact]
    public void RecordInhibition_WeightFlooredAtPointOne()
    {
        var pathway = CreatePathway(weight: 0.11);

        pathway.RecordInhibition();

        pathway.Weight.Should().BeGreaterThanOrEqualTo(0.1);
    }

    [Fact]
    public void Specializations_CanBeConfigured()
    {
        var pathway = CreatePathway();
        pathway.Specializations.Add(SubGoalType.Coding);
        pathway.Specializations.Add(SubGoalType.Math);

        pathway.Specializations.Should().HaveCount(2);
        pathway.Specializations.Should().Contain(SubGoalType.Coding);
    }
}
