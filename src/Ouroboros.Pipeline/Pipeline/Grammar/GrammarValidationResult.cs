// <copyright file="GrammarValidationResult.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Grammar;

/// <summary>
/// Result of validating an ANTLR4 grammar through the Hyperon sidecar.
/// </summary>
public sealed record GrammarValidationResult(
    bool IsValid,
    IReadOnlyList<GrammarIssue> Issues);
