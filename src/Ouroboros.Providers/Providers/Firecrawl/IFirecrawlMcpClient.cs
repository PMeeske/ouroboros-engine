// <copyright file="IFirecrawlMcpClient.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Providers.Firecrawl;

/// <summary>
/// Interface for Firecrawl MCP client operations.
/// Provides methods for web scraping, crawling, and content extraction.
/// </summary>
public interface IFirecrawlMcpClient
{
    /// <summary>
    /// Scrapes a single URL and returns clean markdown content.
    /// </summary>
    /// <param name="url">The URL to scrape.</param>
    /// <param name="options">Optional scrape options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing scraped content or error.</returns>
    Task<Result<FirecrawlScrapeResult, string>> ScrapeAsync(
        string url,
        FirecrawlScrapeOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// Crawls a website starting from a URL and returns content from multiple pages.
    /// </summary>
    /// <param name="url">The starting URL.</param>
    /// <param name="options">Optional crawl options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing crawl job ID or error.</returns>
    Task<Result<string, string>> CrawlAsync(
        string url,
        FirecrawlCrawlOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// Checks the status of a crawl job.
    /// </summary>
    /// <param name="jobId">The crawl job ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing crawl status or error.</returns>
    Task<Result<FirecrawlCrawlStatus, string>> GetCrawlStatusAsync(
        string jobId,
        CancellationToken ct = default);

    /// <summary>
    /// Searches the web and returns scraped results.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="maxResults">Maximum number of results (default: 5).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing search results or error.</returns>
    Task<Result<IReadOnlyList<FirecrawlSearchResult>, string>> SearchAsync(
        string query,
        int maxResults = 5,
        CancellationToken ct = default);

    /// <summary>
    /// Extracts structured data from a URL using an LLM-based extraction schema.
    /// </summary>
    /// <param name="url">The URL to extract from.</param>
    /// <param name="schema">JSON schema describing the data to extract.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing extracted JSON or error.</returns>
    Task<Result<string, string>> ExtractAsync(
        string url,
        string schema,
        CancellationToken ct = default);

    /// <summary>
    /// Maps a website to discover all accessible URLs without scraping content.
    /// </summary>
    /// <param name="url">The starting URL.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing list of discovered URLs or error.</returns>
    Task<Result<IReadOnlyList<string>, string>> MapAsync(
        string url,
        CancellationToken ct = default);
}
