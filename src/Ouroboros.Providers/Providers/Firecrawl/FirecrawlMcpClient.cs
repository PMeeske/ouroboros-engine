// <copyright file="FirecrawlMcpClient.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Ouroboros.Core.Monads;

namespace Ouroboros.Providers.Firecrawl;

/// <summary>
/// Firecrawl MCP client using the Firecrawl REST API.
/// Provides web scraping, crawling, search, extraction, and site mapping.
/// </summary>
public sealed class FirecrawlMcpClient : IFirecrawlMcpClient, IDisposable
{
    private readonly FirecrawlMcpClientOptions _options;
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="FirecrawlMcpClient"/> class.
    /// </summary>
    /// <param name="options">Configuration options.</param>
    /// <param name="httpClient">Optional HTTP client.</param>
    public FirecrawlMcpClient(FirecrawlMcpClientOptions options, HttpClient? httpClient = null)
    {
        if (!options.IsValid())
        {
            throw new ArgumentException("Invalid FirecrawlMcpClientOptions. Provide ApiKey or set FIRECRAWL_API_KEY.", nameof(options));
        }

        _options = options;
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/'));
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", options.ResolveApiKey());
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.Timeout = options.Timeout;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };
    }

    /// <inheritdoc/>
    public async Task<Result<FirecrawlScrapeResult, string>> ScrapeAsync(
        string url,
        FirecrawlScrapeOptions? options = null,
        CancellationToken ct = default)
    {
        try
        {
            var body = new Dictionary<string, object> { ["url"] = url };

            if (options != null)
            {
                body["formats"] = options.Formats;
                if (options.IncludeTags != null) body["includeTags"] = options.IncludeTags;
                if (options.ExcludeTags != null) body["excludeTags"] = options.ExcludeTags;
                if (options.WaitForDynamic) body["waitFor"] = 3000;
                if (options.TimeoutMs.HasValue) body["timeout"] = options.TimeoutMs.Value;
            }

            var content = new StringContent(
                JsonSerializer.Serialize(body, _jsonOptions), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"/{_options.ApiVersion}/scrape", content, ct);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(ct);
                return Result<FirecrawlScrapeResult, string>.Failure(
                    $"Scrape failed: {response.StatusCode} — {err}");
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(json).RootElement;
            var data = doc.TryGetProperty("data", out var d) ? d : doc;

            return Result<FirecrawlScrapeResult, string>.Success(ParseScrapeResult(data, url));
        }
        catch (Exception ex)
        {
            return Result<FirecrawlScrapeResult, string>.Failure($"Scrape failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<string, string>> CrawlAsync(
        string url,
        FirecrawlCrawlOptions? options = null,
        CancellationToken ct = default)
    {
        try
        {
            var body = new Dictionary<string, object> { ["url"] = url };

            if (options != null)
            {
                body["limit"] = options.MaxPages;
                body["maxDepth"] = options.MaxDepth;
                if (options.IncludePatterns != null) body["includePaths"] = options.IncludePatterns;
                if (options.ExcludePatterns != null) body["excludePaths"] = options.ExcludePatterns;
            }

            var content = new StringContent(
                JsonSerializer.Serialize(body, _jsonOptions), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"/{_options.ApiVersion}/crawl", content, ct);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(ct);
                return Result<string, string>.Failure($"Crawl failed: {response.StatusCode} — {err}");
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(json).RootElement;
            var jobId = doc.TryGetProperty("id", out var id) ? id.GetString()!
                : doc.TryGetProperty("jobId", out var jid) ? jid.GetString()!
                : throw new InvalidOperationException("No job ID in crawl response");

            return Result<string, string>.Success(jobId);
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Crawl failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<FirecrawlCrawlStatus, string>> GetCrawlStatusAsync(
        string jobId,
        CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/{_options.ApiVersion}/crawl/{jobId}", ct);
            if (!response.IsSuccessStatusCode)
                return Result<FirecrawlCrawlStatus, string>.Failure(
                    $"GetCrawlStatus failed: {response.StatusCode}");

            var json = await response.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(json).RootElement;

            var results = new List<FirecrawlScrapeResult>();
            if (doc.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in data.EnumerateArray())
                    results.Add(ParseScrapeResult(item, ""));
            }

            return Result<FirecrawlCrawlStatus, string>.Success(new FirecrawlCrawlStatus
            {
                JobId = jobId,
                Status = doc.TryGetProperty("status", out var st) ? st.GetString()! : "unknown",
                PagesScraped = doc.TryGetProperty("completed", out var comp) ? comp.GetInt32() : results.Count,
                TotalPages = doc.TryGetProperty("total", out var tot) ? tot.GetInt32() : results.Count,
                Results = results
            });
        }
        catch (Exception ex)
        {
            return Result<FirecrawlCrawlStatus, string>.Failure($"GetCrawlStatus failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<FirecrawlSearchResult>, string>> SearchAsync(
        string query,
        int maxResults = 5,
        CancellationToken ct = default)
    {
        try
        {
            var body = new Dictionary<string, object>
            {
                ["query"] = query,
                ["limit"] = maxResults
            };

            var content = new StringContent(
                JsonSerializer.Serialize(body, _jsonOptions), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"/{_options.ApiVersion}/search", content, ct);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(ct);
                return Result<IReadOnlyList<FirecrawlSearchResult>, string>.Failure(
                    $"Search failed: {response.StatusCode} — {err}");
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(json).RootElement;
            var dataArray = doc.TryGetProperty("data", out var d) ? d : doc;

            var results = new List<FirecrawlSearchResult>();
            if (dataArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in dataArray.EnumerateArray())
                {
                    results.Add(new FirecrawlSearchResult
                    {
                        Url = item.TryGetProperty("url", out var u) ? u.GetString()! : "",
                        Title = item.TryGetProperty("title", out var t) ? t.GetString() : null,
                        Description = item.TryGetProperty("description", out var desc) ? desc.GetString() : null,
                        Markdown = item.TryGetProperty("markdown", out var md) ? md.GetString() : null
                    });
                }
            }

            return Result<IReadOnlyList<FirecrawlSearchResult>, string>.Success(results);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<FirecrawlSearchResult>, string>.Failure($"Search failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<string, string>> ExtractAsync(
        string url,
        string schema,
        CancellationToken ct = default)
    {
        try
        {
            var schemaObj = JsonSerializer.Deserialize<JsonElement>(schema);
            var body = new Dictionary<string, object>
            {
                ["urls"] = new[] { url },
                ["schema"] = schemaObj
            };

            var content = new StringContent(
                JsonSerializer.Serialize(body, _jsonOptions), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"/{_options.ApiVersion}/extract", content, ct);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(ct);
                return Result<string, string>.Failure($"Extract failed: {response.StatusCode} — {err}");
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            return Result<string, string>.Success(json);
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Extract failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<string>, string>> MapAsync(
        string url,
        CancellationToken ct = default)
    {
        try
        {
            var body = new Dictionary<string, object> { ["url"] = url };
            var content = new StringContent(
                JsonSerializer.Serialize(body, _jsonOptions), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"/{_options.ApiVersion}/map", content, ct);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(ct);
                return Result<IReadOnlyList<string>, string>.Failure($"Map failed: {response.StatusCode} — {err}");
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(json).RootElement;
            var urls = new List<string>();

            var linksArray = doc.TryGetProperty("links", out var links) ? links : doc;
            if (linksArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in linksArray.EnumerateArray())
                    if (item.GetString() is { } s)
                        urls.Add(s);
            }

            return Result<IReadOnlyList<string>, string>.Success(urls);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<string>, string>.Failure($"Map failed: {ex.Message}");
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

    // ── Helpers ──────────────────────────────────────────────────────────

    private static FirecrawlScrapeResult ParseScrapeResult(JsonElement el, string fallbackUrl)
    {
        var metadata = el.TryGetProperty("metadata", out var md) ? md : default;

        return new FirecrawlScrapeResult
        {
            Url = el.TryGetProperty("url", out var u) ? u.GetString()!
                : metadata.ValueKind == JsonValueKind.Object && metadata.TryGetProperty("sourceURL", out var su)
                    ? su.GetString()! : fallbackUrl,
            Title = metadata.ValueKind == JsonValueKind.Object && metadata.TryGetProperty("title", out var t)
                ? t.GetString() : null,
            Markdown = el.TryGetProperty("markdown", out var mkd) ? mkd.GetString() : null,
            Html = el.TryGetProperty("html", out var html) ? html.GetString() : null,
            Metadata = metadata.ValueKind == JsonValueKind.Object
                ? new FirecrawlMetadata
                {
                    Title = metadata.TryGetProperty("title", out var mt) ? mt.GetString() : null,
                    Description = metadata.TryGetProperty("description", out var desc) ? desc.GetString() : null,
                    Language = metadata.TryGetProperty("language", out var lang) ? lang.GetString() : null,
                    SourceUrl = metadata.TryGetProperty("sourceURL", out var src) ? src.GetString() : null,
                    StatusCode = metadata.TryGetProperty("statusCode", out var sc) ? sc.GetInt32() : null
                }
                : null
        };
    }
}
