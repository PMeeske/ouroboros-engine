// <copyright file="FirecrawlMcpClientOptions.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Providers.Firecrawl;

/// <summary>
/// Configuration options for the Firecrawl MCP client.
/// </summary>
public sealed record FirecrawlMcpClientOptions
{
    /// <summary>
    /// Gets the Firecrawl API key.
    /// Can also be set via FIRECRAWL_API_KEY environment variable.
    /// </summary>
    public string? ApiKey { get; init; }

    /// <summary>
    /// Gets the Firecrawl API base URL (default: "https://api.firecrawl.dev").
    /// Set to a self-hosted instance URL if applicable.
    /// </summary>
    public string BaseUrl { get; init; } = "https://api.firecrawl.dev";

    /// <summary>
    /// Gets the API version (default: "v1").
    /// </summary>
    public string ApiVersion { get; init; } = "v1";

    /// <summary>
    /// Gets the timeout for HTTP requests (default: 60 seconds, scraping can be slow).
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Gets the maximum retry attempts (default: 3).
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// Resolves the API key from options or environment variable.
    /// </summary>
    /// <returns>The API key, or null if not configured.</returns>
    public string? ResolveApiKey() =>
        !string.IsNullOrWhiteSpace(ApiKey) ? ApiKey : Environment.GetEnvironmentVariable("FIRECRAWL_API_KEY");

    /// <summary>
    /// Validates the options.
    /// </summary>
    /// <returns>True if valid.</returns>
    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(ResolveApiKey())
            && !string.IsNullOrWhiteSpace(BaseUrl)
            && Timeout > TimeSpan.Zero;
    }
}
