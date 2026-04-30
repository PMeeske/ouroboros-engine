// <copyright file="BeliefValueTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Agent.TheoryOfMind;

namespace Ouroboros.Tests.TheoryOfMind;

[Trait("Category", "Unit")]
public sealed class BeliefValueTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Act
        var belief = new BeliefValue("user_wants_help", 0.95, "observation");

        // Assert
        belief.Proposition.Should().Be("user_wants_help");
        belief.Probability.Should().Be(0.95);
        belief.Source.Should().Be("observation");
    }

    [Fact]
    public void FromObservation_SetsSourceToObservation()
    {
        // Act
        var belief = BeliefValue.FromObservation("user_is_confused", 0.8);

        // Assert
        belief.Proposition.Should().Be("user_is_confused");
        belief.Probability.Should().Be(0.8);
        belief.Source.Should().Be("observation");
    }

    [Fact]
    public void FromObservation_DefaultProbabilityIsOne()
    {
        // Act
        var belief = BeliefValue.FromObservation("confirmed_fact");

        // Assert
        belief.Probability.Should().Be(1.0);
    }

    [Fact]
    public void FromInference_SetsSourceToInference()
    {
        // Act
        var belief = BeliefValue.FromInference("likely_outcome", 0.6);

        // Assert
        belief.Proposition.Should().Be("likely_outcome");
        belief.Probability.Should().Be(0.6);
        belief.Source.Should().Be("inference");
    }

    [Fact]
    public void RecordEquality_WorksCorrectly()
    {
        // Arrange
        var a = new BeliefValue("prop", 0.5, "test");
        var b = new BeliefValue("prop", 0.5, "test");

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void RecordInequality_DifferentProbability()
    {
        // Arrange
        var a = new BeliefValue("prop", 0.5, "test");
        var b = new BeliefValue("prop", 0.9, "test");

        // Assert
        a.Should().NotBe(b);
    }

    [Fact]
    public void WithExpression_CreatesModifiedCopy()
    {
        // Arrange
        var original = BeliefValue.FromObservation("test", 0.5);

        // Act
        var modified = original with { Probability = 0.9 };

        // Assert
        modified.Probability.Should().Be(0.9);
        modified.Proposition.Should().Be("test");
        modified.Source.Should().Be("observation");
    }
}
