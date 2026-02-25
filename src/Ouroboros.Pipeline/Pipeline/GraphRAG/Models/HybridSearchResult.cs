// <copyright file="HybridSearchResult.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.GraphRAG.Models;

/// <summary>
/// Represents the result of a hybrid vector-symbolic search.
/// </summary>
/// <param name="Matches">The matching entities/documents from the search.</param>
/// <param name="Inferences">Logical inferences made during symbolic reasoning.</param>
/// <param name="ReasoningChain">The chain of reasoning steps that led to the results.</param>
public sealed record HybridSearchResult(
    IReadOnlyList<SearchMatch> Matches,
    IReadOnlyList<Inference> Inferences,
    IReadOnlyList<ReasoningChainStep> ReasoningChain)
{
    /// <summary>
    /// Gets the total relevance score for the result set.
    /// </summary>
    public double TotalRelevance => Matches.Sum(m => m.Relevance);

    /// <summary>
    /// Creates an empty result.
    /// </summary>
    public static HybridSearchResult Empty => new([], [], []);

    /// <summary>
    /// Gets the top N matches by relevance.
    /// </summary>
    /// <param name="n">Number of matches to return.</param>
    /// <returns>Top N matches sorted by relevance.</returns>
    public IEnumerable<SearchMatch> TopMatches(int n) =>
        Matches.OrderByDescending(m => m.Relevance).Take(n);
}