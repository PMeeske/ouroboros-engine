// <copyright file="GrammarIssueTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Pipeline.Grammar;

namespace Ouroboros.Tests.Grammar;

public class GrammarIssueTests
{
    [Fact]
    public void GrammarIssue_ShouldBeCreatedWithCorrectValues()
    {
        // Arrange & Act
        var issue = new GrammarIssue(
            GrammarIssueSeverity.Error,
            "expression",
            "Direct left recursion detected",
            GrammarIssueKind.LeftRecursion);

        // Assert
        issue.Severity.Should().Be(GrammarIssueSeverity.Error);
        issue.RuleName.Should().Be("expression");
        issue.Description.Should().Contain("left recursion");
        issue.Kind.Should().Be(GrammarIssueKind.LeftRecursion);
    }

    [Fact]
    public void GrammarValidationResult_Valid_ShouldHaveNoErrors()
    {
        // Arrange & Act
        var result = new GrammarValidationResult(true, Array.Empty<GrammarIssue>());

        // Assert
        result.IsValid.Should().BeTrue();
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void GrammarValidationResult_Invalid_ShouldContainIssues()
    {
        // Arrange
        var issues = new List<GrammarIssue>
        {
            new(GrammarIssueSeverity.Error, "expr", "Left recursion", GrammarIssueKind.LeftRecursion),
            new(GrammarIssueSeverity.Warning, "unused", "Unreachable", GrammarIssueKind.UnreachableRule),
        };

        // Act
        var result = new GrammarValidationResult(false, issues);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Issues.Should().HaveCount(2);
    }

    [Fact]
    public void GrammarCorrectionResult_ShouldTrackCorrections()
    {
        // Arrange & Act
        var result = new GrammarCorrectionResult(
            true,
            "grammar Fixed; expr : term exprPrime;",
            new List<string> { "Removed left recursion from 'expr'" },
            Array.Empty<GrammarIssue>());

        // Assert
        result.Success.Should().BeTrue();
        result.CorrectedGrammarG4.Should().Contain("exprPrime");
        result.CorrectionsApplied.Should().ContainSingle();
        result.RemainingIssues.Should().BeEmpty();
    }

    [Fact]
    public void ParseFailureInfo_ShouldCaptureAllFields()
    {
        // Arrange & Act
        var failure = new ParseFailureInfo(
            OffendingToken: "ELSE",
            ExpectedTokens: "THEN, IDENTIFIER",
            Line: 5,
            Column: 12,
            InputSnippet: "IF x > 0 ELSE");

        // Assert
        failure.OffendingToken.Should().Be("ELSE");
        failure.ExpectedTokens.Should().Contain("THEN");
        failure.Line.Should().Be(5);
        failure.Column.Should().Be(12);
        failure.InputSnippet.Should().Contain("IF x > 0");
    }

    [Fact]
    public void GrammarRefinementResult_Success_ShouldContainExplanation()
    {
        // Arrange & Act
        var result = new GrammarRefinementResult(
            true,
            "grammar Refined; rule : a | b | c;",
            "Added alternative 'c' to handle missing token");

        // Assert
        result.Success.Should().BeTrue();
        result.RefinedGrammarG4.Should().Contain("| c");
        result.Explanation.Should().Contain("missing token");
    }
}
