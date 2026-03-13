// <copyright file="GrammarRefinementResult.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Grammar;

/// <summary>
/// Result of refining a grammar based on parse failure feedback.
/// </summary>
public sealed record GrammarRefinementResult(
    bool Success,
    string RefinedGrammarG4,
    string Explanation);
