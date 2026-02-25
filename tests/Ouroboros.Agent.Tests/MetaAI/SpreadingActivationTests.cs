// <copyright file="SpreadingActivationTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using Ouroboros.Agent.MetaAI.Affect;
using Ouroboros.Core.Hyperon;

namespace Ouroboros.Tests.MetaAI;

/// <summary>
/// Unit tests for SpreadingActivation over the AtomSpace.
/// </summary>
[Trait("Category", "Unit")]
public class SpreadingActivationTests
{
    private static AtomSpace CreateTestSpace()
    {
        var space = new AtomSpace();

        // Create: (related "database" "SQL")
        space.Add(new Expression(ImmutableList.Create<Atom>(
            new Symbol("related"),
            new Symbol("database"),
            new Symbol("SQL"))));

        // Create: (related "SQL" "query")
        space.Add(new Expression(ImmutableList.Create<Atom>(
            new Symbol("related"),
            new Symbol("SQL"),
            new Symbol("query"))));

        // Create: (related "query" "optimization")
        space.Add(new Expression(ImmutableList.Create<Atom>(
            new Symbol("related"),
            new Symbol("query"),
            new Symbol("optimization"))));

        return space;
    }

    [Fact]
    public void Activate_SpreadsToConnectedAtoms()
    {
        // Arrange
        var space = CreateTestSpace();
        var spreading = new SpreadingActivation(space, decayRate: 0.15, spreadFactor: 0.5, activationThreshold: 0.1);

        // Act
        spreading.Activate("database", 1.0);

        // Assert — "database" should be activated; "SQL" should receive spread activation
        var activated = spreading.GetActivatedAtoms();
        activated.Should().Contain(a => a.AtomKey == "database");
        activated.Should().Contain(a => a.AtomKey == "SQL");
    }

    [Fact]
    public void Activate_DecaysWithDistance()
    {
        // Arrange
        var space = CreateTestSpace();
        var spreading = new SpreadingActivation(space, decayRate: 0.15, spreadFactor: 0.5, activationThreshold: 0.01);

        // Act
        spreading.Activate("database", 1.0);

        // Assert — further neighbors should have lower activation
        var activated = spreading.GetActivatedAtoms();
        double dbActivation = activated.First(a => a.AtomKey == "database").Activation;
        double sqlActivation = activated.First(a => a.AtomKey == "SQL").Activation;

        dbActivation.Should().BeGreaterThan(sqlActivation);
    }

    [Fact]
    public void Decay_ReducesAllActivations()
    {
        // Arrange
        var space = CreateTestSpace();
        var spreading = new SpreadingActivation(space, decayRate: 0.15, spreadFactor: 0.5, activationThreshold: 0.01);
        spreading.Activate("database", 1.0);
        var beforeDecay = spreading.GetActivatedAtoms().ToDictionary(a => a.AtomKey, a => a.Activation);

        // Act
        spreading.Decay();

        // Assert — all activations should be lower
        var afterDecay = spreading.GetActivatedAtoms();
        foreach (var (key, activation) in afterDecay)
        {
            if (beforeDecay.TryGetValue(key, out double before))
            {
                activation.Should().BeLessThan(before);
            }
        }
    }

    [Fact]
    public void BelowThreshold_RemovedFromActive()
    {
        // Arrange
        var space = CreateTestSpace();
        var spreading = new SpreadingActivation(space, decayRate: 0.5, spreadFactor: 0.5, activationThreshold: 0.1);
        spreading.Activate("database", 0.2);

        // Act — decay many times to push activation below threshold
        for (int i = 0; i < 20; i++)
        {
            spreading.Decay();
        }

        // Assert — should have no activated atoms
        spreading.GetActivatedAtoms().Should().BeEmpty();
    }

    [Fact]
    public void ModulateByArousal_NarrowsSpread()
    {
        // Arrange
        var space = CreateTestSpace();
        var lowArousal = new SpreadingActivation(space, decayRate: 0.15, spreadFactor: 0.5, activationThreshold: 0.01);
        var highArousal = new SpreadingActivation(space, decayRate: 0.15, spreadFactor: 0.5, activationThreshold: 0.01);

        // Modulate arousal
        lowArousal.ModulateByArousal(0.1); // Low arousal → wide spread
        highArousal.ModulateByArousal(0.9); // High arousal → narrow spread

        // Act
        lowArousal.Activate("database", 1.0);
        highArousal.Activate("database", 1.0);

        // Assert — high arousal should have fewer or weaker activated atoms
        var lowActivated = lowArousal.GetActivatedAtoms();
        var highActivated = highArousal.GetActivatedAtoms();

        // High arousal narrows spread factor and depth, so deeper neighbors should have less activation
        double lowTotalActivation = lowActivated.Sum(a => a.Activation);
        double highTotalActivation = highActivated.Sum(a => a.Activation);
        highTotalActivation.Should().BeLessThanOrEqualTo(lowTotalActivation);
    }

    [Fact]
    public void GetActivatedAtoms_OrderedByActivationDescending()
    {
        // Arrange
        var space = CreateTestSpace();
        var spreading = new SpreadingActivation(space, decayRate: 0.15, spreadFactor: 0.5, activationThreshold: 0.01);

        // Act
        spreading.Activate("database", 1.0);

        // Assert
        var activated = spreading.GetActivatedAtoms();
        activated.Should().BeInDescendingOrder(a => a.Activation);
    }

    [Fact]
    public void Constructor_ThrowsOnNullAtomSpace()
    {
        // Act & Assert
        FluentActions.Invoking(() => new SpreadingActivation(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Activate_ThrowsOnNullAtomKey()
    {
        // Arrange
        var space = new AtomSpace();
        var spreading = new SpreadingActivation(space);

        // Act & Assert
        FluentActions.Invoking(() => spreading.Activate(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Activate_EmptySpace_OnlyActivatesSelf()
    {
        // Arrange
        var space = new AtomSpace();
        var spreading = new SpreadingActivation(space);

        // Act
        spreading.Activate("isolated", 1.0);

        // Assert
        var activated = spreading.GetActivatedAtoms();
        activated.Should().ContainSingle(a => a.AtomKey == "isolated");
    }
}
