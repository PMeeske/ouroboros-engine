namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Configuration for external knowledge sources.
/// </summary>
public sealed record ExternalKnowledgeConfig(
    string ArxivBaseUrl = "http://export.arxiv.org/api/query",
    string SemanticScholarBaseUrl = "https://api.semanticscholar.org/graph/v1",
    int MaxPapersPerQuery = 10,
    int RequestTimeoutSeconds = 30,
    int RateLimitDelayMs = 500,
    bool EnableCaching = true,
    TimeSpan CacheExpiration = default);