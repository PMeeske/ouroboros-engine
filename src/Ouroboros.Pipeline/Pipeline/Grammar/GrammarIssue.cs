// <copyright file="GrammarIssue.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Grammar;

/// <summary>
/// Represents a structural issue found during grammar validation.
/// </summary>
public sealed record GrammarIssue(
    GrammarIssueSeverity Severity,
    string RuleName,
    string Description,
    GrammarIssueKind Kind);

/// <summary>
/// Severity levels for grammar issues.
/// </summary>
public enum GrammarIssueSeverity
{
    Warning,
    Error,
}

/// <summary>
/// Categories of grammar structural issues.
/// </summary>
public enum GrammarIssueKind
{
    Unspecified,
    LeftRecursion,
    UnreachableRule,
    FirstSetConflict,
    MissingRule,
    SyntaxError,
    Ambiguity,
}
