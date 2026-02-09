// <copyright file="DuckDuckGoMcpClient.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using Ouroboros.Core.Monads;

namespace Ouroboros.Providers.DuckDuckGo;

/// <summary>
/// DuckDuckGo MCP client. No API key required.
/// Uses the DuckDuckGo HTML search endpoint and the Instant Answer API.
/// </summary>
public sealed partial class DuckDuckGoMcpClient : IDuckDuckGoMcpClient, IDisposable
{
    private readonly DuckDuckGoMcpClientOptions _options;
    private readonly HttpClient _httpClient;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DuckDuckGoMcpClient"/> class.
    /// </summary>
    /// <param name="options">Configuration options.</param>
    /// <param name="httpClient">Optional HTTP client.</param>
    public DuckDuckGoMcpClient(DuckDuckGoMcpClientOptions? options = null, HttpClient? httpClient = null)
    {
        _options = options ?? new DuckDuckGoMcpClientOptions();
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_options.UserAgent);
        _httpClient.Timeout = _options.Timeout;
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<DuckDuckGoSearchResult>, string>> SearchAsync(
        string query,
        int maxResults = 10,
        string region = "wt-wt",
        CancellationToken ct = default)
    {
        try
        {
            var safeSearch = _options.SafeSearch switch
            {
                "strict" => "1",
                "off" => "-1",
                _ => "-2" // moderate (default)
            };

            var url = $"{_options.SearchBaseUrl}/html/?q={Uri.EscapeDataString(query)}" +
                      $"&kl={region}&p={safeSearch}";

            var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return Result<IReadOnlyList<DuckDuckGoSearchResult>, string>.Failure(
                    $"Search failed: {response.StatusCode}");

            var html = await response.Content.ReadAsStringAsync(ct);
            var results = ParseHtmlSearchResults(html, maxResults);
            return Result<IReadOnlyList<DuckDuckGoSearchResult>, string>.Success(results);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<DuckDuckGoSearchResult>, string>.Failure($"Search failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<DuckDuckGoNewsResult>, string>> SearchNewsAsync(
        string query,
        int maxResults = 10,
        string region = "wt-wt",
        CancellationToken ct = default)
    {
        try
        {
            // DuckDuckGo news uses the same HTML endpoint with iar=news parameter
            var url = $"{_options.SearchBaseUrl}/html/?q={Uri.EscapeDataString(query)}" +
                      $"&kl={region}&iar=news";

            var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return Result<IReadOnlyList<DuckDuckGoNewsResult>, string>.Failure(
                    $"News search failed: {response.StatusCode}");

            var html = await response.Content.ReadAsStringAsync(ct);
            var results = ParseHtmlNewsResults(html, maxResults);
            return Result<IReadOnlyList<DuckDuckGoNewsResult>, string>.Success(results);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<DuckDuckGoNewsResult>, string>.Failure($"News search failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<DuckDuckGoInstantAnswer, string>> InstantAnswerAsync(
        string query,
        CancellationToken ct = default)
    {
        try
        {
            var url = $"{_options.InstantAnswerBaseUrl}/?q={Uri.EscapeDataString(query)}" +
                      "&format=json&no_redirect=1&no_html=1&skip_disambig=1";

            var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return Result<DuckDuckGoInstantAnswer, string>.Failure(
                    $"Instant answer failed: {response.StatusCode}");

            var json = await response.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(json).RootElement;

            var topics = new List<DuckDuckGoRelatedTopic>();
            if (doc.TryGetProperty("RelatedTopics", out var rt) && rt.ValueKind == JsonValueKind.Array)
            {
                foreach (var t in rt.EnumerateArray())
                {
                    if (t.TryGetProperty("Text", out var text))
                    {
                        topics.Add(new DuckDuckGoRelatedTopic
                        {
                            Text = text.GetString(),
                            Url = t.TryGetProperty("FirstURL", out var firstUrl) ? firstUrl.GetString() : null
                        });
                    }
                }
            }

            var answer = new DuckDuckGoInstantAnswer
            {
                Heading = doc.TryGetProperty("Heading", out var h) ? h.GetString() : null,
                AbstractText = doc.TryGetProperty("AbstractText", out var at) ? at.GetString() : null,
                AbstractSource = doc.TryGetProperty("AbstractSource", out var asrc) ? asrc.GetString() : null,
                AbstractUrl = doc.TryGetProperty("AbstractURL", out var aurl) ? aurl.GetString() : null,
                ImageUrl = doc.TryGetProperty("Image", out var img) && img.GetString() is { Length: > 0 } imgStr
                    ? imgStr : null,
                Answer = doc.TryGetProperty("Answer", out var ans) ? ans.GetString() : null,
                AnswerType = doc.TryGetProperty("AnswerType", out var atype) ? atype.GetString() : null,
                Definition = doc.TryGetProperty("Definition", out var def) ? def.GetString() : null,
                RelatedTopics = topics
            };

            return Result<DuckDuckGoInstantAnswer, string>.Success(answer);
        }
        catch (Exception ex)
        {
            return Result<DuckDuckGoInstantAnswer, string>.Failure($"Instant answer failed: {ex.Message}");
        }
    }

    /// <summary>Disposes managed resources.</summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _httpClient.Dispose();
            _disposed = true;
        }
    }

    // ── HTML Parsing Helpers ────────────────────────────────────────────

    private static IReadOnlyList<DuckDuckGoSearchResult> ParseHtmlSearchResults(string html, int maxResults)
    {
        var results = new List<DuckDuckGoSearchResult>();

        // DuckDuckGo HTML results are in <div class="result"> blocks
        var resultMatches = ResultBlockRegex().Matches(html);
        foreach (Match block in resultMatches)
        {
            if (results.Count >= maxResults) break;

            var blockHtml = block.Groups[1].Value;

            // Extract URL from <a class="result__a" href="...">
            var urlMatch = ResultUrlRegex().Match(blockHtml);
            if (!urlMatch.Success) continue;

            var rawUrl = HttpUtility.HtmlDecode(urlMatch.Groups[1].Value);
            // DuckDuckGo wraps URLs in a redirect; extract the actual URL
            var actualUrl = ExtractActualUrl(rawUrl);

            // Extract title
            var titleMatch = ResultTitleRegex().Match(blockHtml);
            var title = titleMatch.Success
                ? StripHtmlTags(HttpUtility.HtmlDecode(titleMatch.Groups[1].Value)).Trim()
                : actualUrl;

            // Extract snippet from <a class="result__snippet">
            var snippetMatch = ResultSnippetRegex().Match(blockHtml);
            var snippet = snippetMatch.Success
                ? StripHtmlTags(HttpUtility.HtmlDecode(snippetMatch.Groups[1].Value)).Trim()
                : null;

            if (!string.IsNullOrWhiteSpace(actualUrl))
            {
                results.Add(new DuckDuckGoSearchResult
                {
                    Title = title,
                    Url = actualUrl,
                    Snippet = snippet
                });
            }
        }

        return results;
    }

    private static IReadOnlyList<DuckDuckGoNewsResult> ParseHtmlNewsResults(string html, int maxResults)
    {
        var results = new List<DuckDuckGoNewsResult>();
        var resultMatches = ResultBlockRegex().Matches(html);

        foreach (Match block in resultMatches)
        {
            if (results.Count >= maxResults) break;

            var blockHtml = block.Groups[1].Value;

            var urlMatch = ResultUrlRegex().Match(blockHtml);
            if (!urlMatch.Success) continue;

            var rawUrl = HttpUtility.HtmlDecode(urlMatch.Groups[1].Value);
            var actualUrl = ExtractActualUrl(rawUrl);

            var titleMatch = ResultTitleRegex().Match(blockHtml);
            var title = titleMatch.Success
                ? StripHtmlTags(HttpUtility.HtmlDecode(titleMatch.Groups[1].Value)).Trim()
                : actualUrl;

            var snippetMatch = ResultSnippetRegex().Match(blockHtml);
            var snippet = snippetMatch.Success
                ? StripHtmlTags(HttpUtility.HtmlDecode(snippetMatch.Groups[1].Value)).Trim()
                : null;

            if (!string.IsNullOrWhiteSpace(actualUrl))
            {
                results.Add(new DuckDuckGoNewsResult
                {
                    Title = title,
                    Url = actualUrl,
                    Snippet = snippet
                });
            }
        }

        return results;
    }

    private static string ExtractActualUrl(string ddgUrl)
    {
        // DuckDuckGo wraps URLs: //duckduckgo.com/l/?uddg=<encoded>&rut=...
        if (ddgUrl.Contains("uddg="))
        {
            var parsed = HttpUtility.ParseQueryString(new Uri("https:" + ddgUrl).Query);
            var uddg = parsed["uddg"];
            if (!string.IsNullOrWhiteSpace(uddg))
                return uddg;
        }

        // Direct URL
        if (ddgUrl.StartsWith("//"))
            return "https:" + ddgUrl;

        return ddgUrl;
    }

    private static string StripHtmlTags(string input)
    {
        return HtmlTagRegex().Replace(input, "");
    }

    [GeneratedRegex(@"<div\s+class=""result[^""]*""[^>]*>(.*?)</div>", RegexOptions.Singleline)]
    private static partial Regex ResultBlockRegex();

    [GeneratedRegex(@"<a\s+class=""result__a""\s+href=""([^""]+)""", RegexOptions.Singleline)]
    private static partial Regex ResultUrlRegex();

    [GeneratedRegex(@"<a\s+class=""result__a""[^>]*>(.*?)</a>", RegexOptions.Singleline)]
    private static partial Regex ResultTitleRegex();

    [GeneratedRegex(@"<a\s+class=""result__snippet""[^>]*>(.*?)</a>", RegexOptions.Singleline)]
    private static partial Regex ResultSnippetRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTagRegex();
}
