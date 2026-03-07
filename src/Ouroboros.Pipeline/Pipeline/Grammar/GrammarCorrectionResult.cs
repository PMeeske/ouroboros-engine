// <copyright file="GrammarCorrectionResult.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Grammar;

/// <summary>
/// Result of correcting an ANTLR4 grammar through MeTTa rewriting rules.
/// </summary>
public sealed record GrammarCorrectionResult(
    bool Success,
    string CorrectedGrammarG4,
    IReadOnlyList<string> CorrectionsApplied,
    IReadOnlyList<GrammarIssue> RemainingIssues);
