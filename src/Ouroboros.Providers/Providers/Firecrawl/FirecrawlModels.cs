// <copyright file="FirecrawlModels.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Providers.Firecrawl;

/// <summary>
/// Result of scraping a single URL.
/// </summary>
public sealed record FirecrawlScrapeResult
{
    /// <summary>Gets the scraped URL.</summary>
    public required string Url { get; init; }

    /// <summary>Gets the page title.</summary>
    public string? Title { get; init; }

    /// <summary>Gets the markdown content.</summary>
    public string? Markdown { get; init; }

    /// <summary>Gets the raw HTML content (if requested).</summary>
    public string? Html { get; init; }

    /// <summary>Gets the extracted metadata.</summary>
    public FirecrawlMetadata? Metadata { get; init; }
}

/// <summary>
/// Metadata extracted from a scraped page.
/// </summary>
public sealed record FirecrawlMetadata
{
    /// <summary>Gets the page title.</summary>
    public string? Title { get; init; }

    /// <summary>Gets the meta description.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the language.</summary>
    public string? Language { get; init; }

    /// <summary>Gets the source URL.</summary>
    public string? SourceUrl { get; init; }

    /// <summary>Gets the status code.</summary>
    public int? StatusCode { get; init; }
}

/// <summary>
/// Options for scraping a URL.
/// </summary>
public sealed record FirecrawlScrapeOptions
{
    /// <summary>Gets the output formats requested (markdown, html, rawHtml, links, screenshot).</summary>
    public IReadOnlyList<string> Formats { get; init; } = ["markdown"];

    /// <summary>Gets CSS selectors to include (only content matching these selectors).</summary>
    public IReadOnlyList<string>? IncludeTags { get; init; }

    /// <summary>Gets CSS selectors to exclude.</summary>
    public IReadOnlyList<string>? ExcludeTags { get; init; }

    /// <summary>Gets whether to wait for dynamic content to load.</summary>
    public bool WaitForDynamic { get; init; }

    /// <summary>Gets the timeout in milliseconds for page load.</summary>
    public int? TimeoutMs { get; init; }
}

/// <summary>
/// Options for crawling a website.
/// </summary>
public sealed record FirecrawlCrawlOptions
{
    /// <summary>Gets the maximum number of pages to crawl (default: 50).</summary>
    public int MaxPages { get; init; } = 50;

    /// <summary>Gets URL patterns to include (regex).</summary>
    public IReadOnlyList<string>? IncludePatterns { get; init; }

    /// <summary>Gets URL patterns to exclude (regex).</summary>
    public IReadOnlyList<string>? ExcludePatterns { get; init; }

    /// <summary>Gets the maximum crawl depth (default: 3).</summary>
    public int MaxDepth { get; init; } = 3;
}

/// <summary>
/// Status of a crawl job.
/// </summary>
public sealed record FirecrawlCrawlStatus
{
    /// <summary>Gets the job ID.</summary>
    public required string JobId { get; init; }

    /// <summary>Gets the status (scraping, completed, failed).</summary>
    public required string Status { get; init; }

    /// <summary>Gets the number of pages scraped so far.</summary>
    public int PagesScraped { get; init; }

    /// <summary>Gets the total pages discovered.</summary>
    public int TotalPages { get; init; }

    /// <summary>Gets the scraped results (available when completed).</summary>
    public IReadOnlyList<FirecrawlScrapeResult> Results { get; init; } = [];
}

/// <summary>
/// A search result with scraped content.
/// </summary>
public sealed record FirecrawlSearchResult
{
    /// <summary>Gets the result URL.</summary>
    public required string Url { get; init; }

    /// <summary>Gets the page title.</summary>
    public string? Title { get; init; }

    /// <summary>Gets a snippet or description.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the markdown content of the result page.</summary>
    public string? Markdown { get; init; }
}
