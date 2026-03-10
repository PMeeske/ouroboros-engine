// <copyright file="LogicalConflictTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.NeuralSymbolic;

namespace Ouroboros.Tests.NeuralSymbolic;

[Trait("Category", "Unit")]
public sealed class LogicalConflictTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var rule1 = new SymbolicRule("r1", "(= x 1)", "x is 1", new(), new(), 0.9, RuleSource.UserProvided);
        var rule2 = new SymbolicRule("r2", "(= x 2)", "x is 2", new(), new(), 0.8, RuleSource.LearnedFromExperience);

        // Act
        var conflict = new LogicalConflict("x cannot be both 1 and 2", rule1, rule2, "pick one value");

        // Assert
        conflict.Description.Should().Be("x cannot be both 1 and 2");
        conflict.Rule1.Should().Be(rule1);
        conflict.Rule2.Should().Be(rule2);
        conflict.Resolution.Should().Be("pick one value");
    }

    [Fact]
    public void Constructor_AllowsNullRules()
    {
        // Act
        var conflict = new LogicalConflict("detected via LLM", null, null, "review manually");

        // Assert
        conflict.Rule1.Should().BeNull();
        conflict.Rule2.Should().BeNull();
        conflict.Description.Should().Be("detected via LLM");
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        // Arrange
        var a = new LogicalConflict("desc", null, null, "fix");
        var b = new LogicalConflict("desc", null, null, "fix");

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void WithExpression_CreatesModifiedCopy()
    {
        // Arrange
        var conflict = new LogicalConflict("original", null, null, "resolve");

        // Act
        var modified = conflict with { Description = "updated" };

        // Assert
        modified.Description.Should().Be("updated");
        modified.Resolution.Should().Be("resolve");
    }
}
