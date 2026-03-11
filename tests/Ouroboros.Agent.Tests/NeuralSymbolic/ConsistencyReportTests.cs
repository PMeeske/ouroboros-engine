// <copyright file="ConsistencyReportTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.NeuralSymbolic;

namespace Ouroboros.Tests.NeuralSymbolic;

[Trait("Category", "Unit")]
public sealed class ConsistencyReportTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var conflicts = new List<LogicalConflict>
        {
            new("conflict desc", null, null, "resolve manually")
        };
        var missing = new List<string> { "prerequisite1" };
        var suggestions = new List<string> { "add more rules" };

        // Act
        var report = new ConsistencyReport(true, conflicts, missing, suggestions, 0.85);

        // Assert
        report.IsConsistent.Should().BeTrue();
        report.Conflicts.Should().HaveCount(1);
        report.MissingPrerequisites.Should().Contain("prerequisite1");
        report.Suggestions.Should().Contain("add more rules");
        report.ConsistencyScore.Should().Be(0.85);
    }

    [Fact]
    public void ConsistentReport_WithNoConflicts()
    {
        // Act
        var report = new ConsistencyReport(
            true,
            new List<LogicalConflict>(),
            new List<string>(),
            new List<string>(),
            1.0);

        // Assert
        report.IsConsistent.Should().BeTrue();
        report.Conflicts.Should().BeEmpty();
        report.ConsistencyScore.Should().Be(1.0);
    }

    [Fact]
    public void InconsistentReport_WithConflicts()
    {
        // Arrange
        var rule1 = new SymbolicRule("r1", "(= a b)", "a equals b", new(), new(), 0.9, RuleSource.UserProvided);
        var rule2 = new SymbolicRule("r2", "(!= a b)", "a not equals b", new(), new(), 0.8, RuleSource.LearnedFromExperience);
        var conflict = new LogicalConflict("contradiction", rule1, rule2, "remove one");

        // Act
        var report = new ConsistencyReport(
            false,
            new List<LogicalConflict> { conflict },
            new List<string>(),
            new List<string> { "resolve contradiction" },
            0.3);

        // Assert
        report.IsConsistent.Should().BeFalse();
        report.Conflicts.Should().HaveCount(1);
        report.ConsistencyScore.Should().Be(0.3);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        // Arrange
        var conflicts = new List<LogicalConflict>();
        var missing = new List<string>();
        var suggestions = new List<string>();

        var a = new ConsistencyReport(true, conflicts, missing, suggestions, 1.0);
        var b = new ConsistencyReport(true, conflicts, missing, suggestions, 1.0);

        // Assert
        a.Should().Be(b);
    }
}
