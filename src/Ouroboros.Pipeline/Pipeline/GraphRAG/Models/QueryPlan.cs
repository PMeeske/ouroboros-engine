// <copyright file="QueryPlan.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.GraphRAG.Models;

/// <summary>
/// Represents a decomposed query plan for hybrid retrieval.
/// </summary>
/// <param name="OriginalQuery">The original natural language query.</param>
/// <param name="QueryType">The type of query (SingleHop, MultiHop, Aggregation, Comparison).</param>
/// <param name="Steps">The sequence of steps to execute.</param>
public sealed record QueryPlan(
    string OriginalQuery,
    QueryType QueryType,
    IReadOnlyList<QueryStep> Steps)
{
    /// <summary>
    /// Creates a simple single-hop query plan.
    /// </summary>
    /// <param name="query">The query string.</param>
    /// <returns>A single-hop query plan.</returns>
    public static QueryPlan SingleHop(string query) =>
        new(query, QueryType.SingleHop, [new QueryStep(1, QueryStepType.VectorSearch, query, [])]);

    /// <summary>
    /// Gets the estimated complexity of executing this plan.
    /// </summary>
    public int EstimatedComplexity => Steps.Count + (QueryType == QueryType.MultiHop ? 2 : 0);
}