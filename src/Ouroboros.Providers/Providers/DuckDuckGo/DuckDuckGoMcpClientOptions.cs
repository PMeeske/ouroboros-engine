// <copyright file="DuckDuckGoMcpClientOptions.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Providers.DuckDuckGo;

/// <summary>
/// Configuration options for the DuckDuckGo MCP client.
/// DuckDuckGo does not require API keys for its public endpoints.
/// </summary>
public sealed record DuckDuckGoMcpClientOptions
{
    /// <summary>
    /// Gets the base URL for the DuckDuckGo HTML search endpoint.
    /// Default: "https://html.duckduckgo.com".
    /// </summary>
    public string SearchBaseUrl { get; init; } = "https://html.duckduckgo.com";

    /// <summary>
    /// Gets the base URL for the DuckDuckGo Instant Answer API.
    /// Default: "https://api.duckduckgo.com".
    /// </summary>
    public string InstantAnswerBaseUrl { get; init; } = "https://api.duckduckgo.com";

    /// <summary>
    /// Gets the User-Agent header to use for requests.
    /// </summary>
    public string UserAgent { get; init; } = "Ouroboros/1.0 (https://github.com/PMeeske/ouroboros-engine)";

    /// <summary>
    /// Gets the timeout for HTTP requests (default: 15 seconds).
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Gets the maximum retry attempts (default: 2).
    /// </summary>
    public int MaxRetries { get; init; } = 2;

    /// <summary>
    /// Gets the safe search level: "strict", "moderate", or "off".
    /// </summary>
    public string SafeSearch { get; init; } = "moderate";

    /// <summary>
    /// Validates the options. Always valid since no API key is needed.
    /// </summary>
    /// <returns>True.</returns>
    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(SearchBaseUrl)
            && !string.IsNullOrWhiteSpace(InstantAnswerBaseUrl)
            && Timeout > TimeSpan.Zero;
    }
}
