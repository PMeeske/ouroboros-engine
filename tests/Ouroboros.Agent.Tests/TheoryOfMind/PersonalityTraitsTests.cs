// <copyright file="PersonalityTraitsTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Agent.TheoryOfMind;

namespace Ouroboros.Tests.TheoryOfMind;

[Trait("Category", "Unit")]
public sealed class PersonalityTraitsTests
{
    [Fact]
    public void Default_ReturnsNeutralValues()
    {
        // Act
        var traits = PersonalityTraits.Default();

        // Assert
        traits.Cooperativeness.Should().Be(0.5);
        traits.Predictability.Should().Be(0.5);
        traits.Competence.Should().Be(0.5);
        traits.CustomTraits.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var custom = new Dictionary<string, double> { ["empathy"] = 0.8 };

        // Act
        var traits = new PersonalityTraits(0.9, 0.7, 0.6, custom);

        // Assert
        traits.Cooperativeness.Should().Be(0.9);
        traits.Predictability.Should().Be(0.7);
        traits.Competence.Should().Be(0.6);
        traits.CustomTraits.Should().ContainKey("empathy");
    }

    [Fact]
    public void WithTrait_AddsCustomTrait()
    {
        // Arrange
        var traits = PersonalityTraits.Default();

        // Act
        var updated = traits.WithTrait("curiosity", 0.8);

        // Assert
        updated.CustomTraits.Should().ContainKey("curiosity");
        updated.CustomTraits["curiosity"].Should().Be(0.8);
    }

    [Fact]
    public void WithTrait_ClampsValue()
    {
        // Arrange
        var traits = PersonalityTraits.Default();

        // Act & Assert
        traits.WithTrait("over", 1.5).CustomTraits["over"].Should().Be(1.0);
        traits.WithTrait("under", -0.5).CustomTraits["under"].Should().Be(0.0);
    }

    [Fact]
    public void WithTrait_DoesNotMutateOriginal()
    {
        // Arrange
        var traits = PersonalityTraits.Default();

        // Act
        _ = traits.WithTrait("new_trait", 0.7);

        // Assert
        traits.CustomTraits.Should().BeEmpty();
    }

    [Fact]
    public void WithTrait_UpdatesExistingTrait()
    {
        // Arrange
        var traits = PersonalityTraits.Default().WithTrait("curiosity", 0.5);

        // Act
        var updated = traits.WithTrait("curiosity", 0.9);

        // Assert
        updated.CustomTraits["curiosity"].Should().Be(0.9);
    }

    [Fact]
    public void RecordEquality_WorksCorrectly()
    {
        // Arrange
        var custom = new Dictionary<string, double>();
        var a = new PersonalityTraits(0.5, 0.5, 0.5, custom);
        var b = new PersonalityTraits(0.5, 0.5, 0.5, custom);

        // Assert
        a.Should().Be(b);
    }
}
