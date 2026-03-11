// <copyright file="GrammarCorrectionResultTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Pipeline.Grammar;

namespace Ouroboros.Tests.Grammar;

[Trait("Category", "Unit")]
public sealed class GrammarCorrectionResultTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var corrections = new List<string> { "fixed rule A", "fixed rule B" };
        var issues = new List<GrammarIssue>
        {
            new(GrammarIssueSeverity.Warning, "ruleX", "ambiguous", GrammarIssueKind.Ambiguity)
        };

        // Act
        var result = new GrammarCorrectionResult(true, "grammar G4;", corrections, issues);

        // Assert
        result.Success.Should().BeTrue();
        result.CorrectedGrammarG4.Should().Be("grammar G4;");
        result.CorrectionsApplied.Should().HaveCount(2);
        result.RemainingIssues.Should().HaveCount(1);
    }

    [Fact]
    public void SuccessfulCorrection_NoRemainingIssues()
    {
        // Act
        var result = new GrammarCorrectionResult(
            true, "grammar Clean;", new List<string> { "fix1" }, new List<GrammarIssue>());

        // Assert
        result.Success.Should().BeTrue();
        result.RemainingIssues.Should().BeEmpty();
    }

    [Fact]
    public void FailedCorrection_HasRemainingIssues()
    {
        // Arrange
        var issues = new List<GrammarIssue>
        {
            new(GrammarIssueSeverity.Error, "rule1", "syntax error", GrammarIssueKind.SyntaxError)
        };

        // Act
        var result = new GrammarCorrectionResult(false, "", new List<string>(), issues);

        // Assert
        result.Success.Should().BeFalse();
        result.RemainingIssues.Should().HaveCount(1);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        // Arrange
        var corrections = new List<string>();
        var issues = new List<GrammarIssue>();
        var a = new GrammarCorrectionResult(true, "g4", corrections, issues);
        var b = new GrammarCorrectionResult(true, "g4", corrections, issues);

        // Assert
        a.Should().Be(b);
    }
}
