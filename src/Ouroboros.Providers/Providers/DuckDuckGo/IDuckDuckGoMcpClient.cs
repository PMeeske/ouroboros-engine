// <copyright file="IDuckDuckGoMcpClient.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Providers.DuckDuckGo;

/// <summary>
/// Interface for DuckDuckGo MCP client operations.
/// Provides web search, news search, and instant answers — no API key required.
/// </summary>
public interface IDuckDuckGoMcpClient
{
    /// <summary>
    /// Performs a web search using DuckDuckGo.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="maxResults">Maximum number of results (default: 10).</param>
    /// <param name="region">Region code (e.g., "us-en", "de-de"). Default: "wt-wt" (no region).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing search results or error.</returns>
    Task<Result<IReadOnlyList<DuckDuckGoSearchResult>, string>> SearchAsync(
        string query,
        int maxResults = 10,
        string region = "wt-wt",
        CancellationToken ct = default);

    /// <summary>
    /// Performs a news search using DuckDuckGo.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="maxResults">Maximum number of results (default: 10).</param>
    /// <param name="region">Region code. Default: "wt-wt".</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing news results or error.</returns>
    Task<Result<IReadOnlyList<DuckDuckGoNewsResult>, string>> SearchNewsAsync(
        string query,
        int maxResults = 10,
        string region = "wt-wt",
        CancellationToken ct = default);

    /// <summary>
    /// Gets an instant answer from DuckDuckGo (abstract / knowledge panel).
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing instant answer or error.</returns>
    Task<Result<DuckDuckGoInstantAnswer, string>> InstantAnswerAsync(
        string query,
        CancellationToken ct = default);
}
