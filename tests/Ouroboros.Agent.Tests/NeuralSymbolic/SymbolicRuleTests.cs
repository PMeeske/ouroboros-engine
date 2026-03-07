// <copyright file="SymbolicRuleTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.NeuralSymbolic;

namespace Ouroboros.Tests.NeuralSymbolic;

[Trait("Category", "Unit")]
public class SymbolicRuleTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var rule = new SymbolicRule(
            "rule1",
            "(= (rule1 $x) True)",
            "A test rule",
            new List<string> { "pre1" },
            new List<string> { "eff1" },
            0.85,
            RuleSource.LearnedFromExperience);

        rule.Name.Should().Be("rule1");
        rule.MeTTaRepresentation.Should().Be("(= (rule1 $x) True)");
        rule.NaturalLanguageDescription.Should().Be("A test rule");
        rule.Preconditions.Should().ContainSingle("pre1");
        rule.Effects.Should().ContainSingle("eff1");
        rule.Confidence.Should().Be(0.85);
        rule.Source.Should().Be(RuleSource.LearnedFromExperience);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new SymbolicRule("r", "(r)", "desc", new List<string>(), new List<string>(), 0.5, RuleSource.UserProvided);
        var b = new SymbolicRule("r", "(r)", "desc", new List<string>(), new List<string>(), 0.5, RuleSource.UserProvided);

        a.Should().BeEquivalentTo(b);
    }
}
