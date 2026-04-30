// <copyright file="GrammarValidationResultTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Pipeline.Grammar;

namespace Ouroboros.Tests.Grammar;

[Trait("Category", "Unit")]
public sealed class GrammarValidationResultTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var issues = new List<GrammarIssue>
        {
            new(GrammarIssueSeverity.Warning, "expr", "left recursion", GrammarIssueKind.LeftRecursion)
        };

        // Act
        var result = new GrammarValidationResult(false, issues);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Issues.Should().HaveCount(1);
    }

    [Fact]
    public void ValidResult_HasNoIssues()
    {
        // Act
        var result = new GrammarValidationResult(true, new List<GrammarIssue>());

        // Assert
        result.IsValid.Should().BeTrue();
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void InvalidResult_HasIssues()
    {
        // Arrange
        var issues = new List<GrammarIssue>
        {
            new(GrammarIssueSeverity.Error, "start", "missing rule", GrammarIssueKind.SyntaxError),
            new(GrammarIssueSeverity.Warning, "expr", "unused", GrammarIssueKind.UnreachableRule)
        };

        // Act
        var result = new GrammarValidationResult(false, issues);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Issues.Should().HaveCount(2);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        // Arrange
        var issues = new List<GrammarIssue>();
        var a = new GrammarValidationResult(true, issues);
        var b = new GrammarValidationResult(true, issues);

        // Assert
        a.Should().Be(b);
    }
}
