// <copyright file="UrgeSystemTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Agent.MetaAI.Affect;

namespace Ouroboros.Tests.MetaAI;

/// <summary>
/// Unit tests for the Psi-theory UrgeSystem.
/// </summary>
[Trait("Category", "Unit")]
public class UrgeSystemTests
{
    [Fact]
    public void Constructor_CreatesDefaultUrges()
    {
        // Arrange & Act
        var system = new UrgeSystem();

        // Assert
        system.Urges.Should().HaveCount(5);
        system.Urges.Select(u => u.Name).Should().BeEquivalentTo(
            new[] { "competence", "certainty", "affiliation", "curiosity", "integrity" });
    }

    [Fact]
    public void Tick_AccumulatesAllUrges()
    {
        // Arrange
        var system = new UrgeSystem();
        var initialIntensities = system.Urges.Select(u => u.Intensity).ToList();

        // Act — tick 5 times
        for (int i = 0; i < 5; i++)
        {
            system.Tick();
        }

        // Assert — each urge intensity should increase by 5 × accumulationRate
        for (int i = 0; i < system.Urges.Count; i++)
        {
            double expected = Math.Min(1.0, initialIntensities[i] + (5 * system.Urges[i].AccumulationRate));
            system.Urges[i].Intensity.Should().BeApproximately(expected, 0.001);
        }
    }

    [Fact]
    public void Satisfy_ReducesIntensity()
    {
        // Arrange
        var system = new UrgeSystem();

        // Tick several times to build up curiosity
        for (int i = 0; i < 10; i++)
        {
            system.Tick();
        }

        double beforeSatisfy = system.Urges.First(u => u.Name == "curiosity").Intensity;

        // Act
        system.Satisfy("curiosity");

        // Assert — intensity reduced by satisfactionRate × amount
        double curiosity = system.Urges.First(u => u.Name == "curiosity").Intensity;
        double expectedReduction = system.Urges.First(u => u.Name == "curiosity").SatisfactionRate;
        curiosity.Should().BeApproximately(beforeSatisfy - expectedReduction, 0.001);
    }

    [Fact]
    public void Satisfy_ClampsToZero()
    {
        // Arrange
        var system = new UrgeSystem();

        // Act — satisfy multiple times to try to go below 0
        for (int i = 0; i < 20; i++)
        {
            system.Satisfy("affiliation");
        }

        // Assert
        system.Urges.First(u => u.Name == "affiliation").Intensity.Should().BeGreaterThanOrEqualTo(0.0);
    }

    [Fact]
    public void Satisfy_UnknownUrge_DoesNothing()
    {
        // Arrange
        var system = new UrgeSystem();
        var before = system.Urges.Select(u => u.Intensity).ToList();

        // Act
        system.Satisfy("nonexistent");

        // Assert — intensities unchanged
        for (int i = 0; i < system.Urges.Count; i++)
        {
            system.Urges[i].Intensity.Should().Be(before[i]);
        }
    }

    [Fact]
    public void GetDominantUrge_ReturnsHighestUrgency()
    {
        // Arrange — curiosity starts highest (0.4 intensity × 0.8 priority)
        var system = new UrgeSystem();

        // Act
        var dominant = system.GetDominantUrge();

        // Assert — should be curiosity (highest intensity × priority product)
        dominant.Should().NotBeNull();
        // The urgency formula: intensity × priority × (1 + stress × 0.3)
        // With stress=0: competence=0.3*1.0=0.30, certainty=0.3*0.9=0.27,
        // affiliation=0.2*0.7=0.14, curiosity=0.4*0.8=0.32, integrity=0.2*0.95=0.19
        dominant.Name.Should().Be("curiosity");
    }

    [Fact]
    public void GetDominantUrge_WithStress_AmplifiesUrgency()
    {
        // Arrange — use high stress
        var system = new UrgeSystem(stress: 0.9);

        // Act
        var dominant = system.GetDominantUrge();

        // Assert — stress amplifies all urgencies, but curiosity should still dominate
        dominant.Should().NotBeNull();
    }

    [Fact]
    public void Tick_ClampsIntensityToMax()
    {
        // Arrange
        var system = new UrgeSystem();

        // Act — tick many times
        for (int i = 0; i < 100; i++)
        {
            system.Tick();
        }

        // Assert — all intensities should be clamped to 1.0
        foreach (var urge in system.Urges)
        {
            urge.Intensity.Should().BeLessThanOrEqualTo(1.0);
        }
    }

    [Fact]
    public void ToMeTTa_ProjectsAllUrges()
    {
        // Arrange
        var system = new UrgeSystem();
        string instanceId = "test-instance-123";

        // Act
        string metta = system.ToMeTTa(instanceId);

        // Assert
        metta.Should().Contain("(HasUrge (OuroborosInstance \"test-instance-123\") (Urge \"competence\"");
        metta.Should().Contain("(HasUrge (OuroborosInstance \"test-instance-123\") (Urge \"certainty\"");
        metta.Should().Contain("(HasUrge (OuroborosInstance \"test-instance-123\") (Urge \"affiliation\"");
        metta.Should().Contain("(HasUrge (OuroborosInstance \"test-instance-123\") (Urge \"curiosity\"");
        metta.Should().Contain("(HasUrge (OuroborosInstance \"test-instance-123\") (Urge \"integrity\"");
        metta.Should().Contain("(DominantUrge (OuroborosInstance \"test-instance-123\")");
    }

    [Fact]
    public void ToMeTTa_ThrowsOnNullInstanceId()
    {
        // Arrange
        var system = new UrgeSystem();

        // Act & Assert
        FluentActions.Invoking(() => system.ToMeTTa(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Satisfy_ThrowsOnNullUrgeName()
    {
        // Arrange
        var system = new UrgeSystem();

        // Act & Assert
        FluentActions.Invoking(() => system.Satisfy(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Satisfy_IsCaseInsensitive()
    {
        // Arrange
        var system = new UrgeSystem();
        double before = system.Urges.First(u => u.Name == "curiosity").Intensity;

        // Act
        system.Satisfy("CURIOSITY");

        // Assert
        double after = system.Urges.First(u => u.Name == "curiosity").Intensity;
        after.Should().BeLessThan(before);
    }
}
