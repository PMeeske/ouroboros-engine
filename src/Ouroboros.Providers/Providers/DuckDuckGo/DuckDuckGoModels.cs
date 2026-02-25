// <copyright file="DuckDuckGoModels.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Providers.DuckDuckGo;

/// <summary>
/// A DuckDuckGo web search result.
/// </summary>
public sealed record DuckDuckGoSearchResult
{
    /// <summary>Gets the result title.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the result URL.</summary>
    public required string Url { get; init; }

    /// <summary>Gets the text snippet (body/description).</summary>
    public string? Snippet { get; init; }
}

/// <summary>
/// A DuckDuckGo news search result.
/// </summary>
public sealed record DuckDuckGoNewsResult
{
    /// <summary>Gets the news title.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the article URL.</summary>
    public required string Url { get; init; }

    /// <summary>Gets the news snippet.</summary>
    public string? Snippet { get; init; }

    /// <summary>Gets the source/publisher name.</summary>
    public string? Source { get; init; }

    /// <summary>Gets the publication date.</summary>
    public DateTimeOffset? PublishedAt { get; init; }
}

/// <summary>
/// A DuckDuckGo instant answer (from the Instant Answer API).
/// </summary>
public sealed record DuckDuckGoInstantAnswer
{
    /// <summary>Gets the heading/topic.</summary>
    public string? Heading { get; init; }

    /// <summary>Gets the abstract text.</summary>
    public string? AbstractText { get; init; }

    /// <summary>Gets the abstract source.</summary>
    public string? AbstractSource { get; init; }

    /// <summary>Gets the abstract URL.</summary>
    public string? AbstractUrl { get; init; }

    /// <summary>Gets the image URL.</summary>
    public string? ImageUrl { get; init; }

    /// <summary>Gets the answer text (direct answer).</summary>
    public string? Answer { get; init; }

    /// <summary>Gets the answer type (e.g., "calc", "ip", etc.).</summary>
    public string? AnswerType { get; init; }

    /// <summary>Gets the definition text.</summary>
    public string? Definition { get; init; }

    /// <summary>Gets related topics.</summary>
    public IReadOnlyList<DuckDuckGoRelatedTopic> RelatedTopics { get; init; } = [];
}

/// <summary>
/// A related topic from DuckDuckGo instant answers.
/// </summary>
public sealed record DuckDuckGoRelatedTopic
{
    /// <summary>Gets the topic text.</summary>
    public string? Text { get; init; }

    /// <summary>Gets the topic URL.</summary>
    public string? Url { get; init; }
}
