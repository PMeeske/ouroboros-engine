// <copyright file="GrammarRefinementResult.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Grammar;

/// <summary>
/// Result of refining a grammar based on parse failure feedback.
/// </summary>
public sealed record GrammarRefinementResult(
    bool Success,
    string RefinedGrammarG4,
    string Explanation);
