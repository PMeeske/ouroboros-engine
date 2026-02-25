namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Interface for external knowledge sources that feed the emergence pipeline.
/// </summary>
public interface IExternalKnowledgeSource
{
    /// <summary>
    /// Searches for research papers relevant to a topic.
    /// </summary>
    Task<Result<List<ResearchPaper>, string>> SearchPapersAsync(
        string query,
        int maxResults = 5,
        CancellationToken ct = default);

    /// <summary>
    /// Gets citation data for a paper.
    /// </summary>
    Task<Result<CitationMetadata, string>> GetCitationsAsync(
        string paperId,
        CancellationToken ct = default);

    /// <summary>
    /// Generates observations from research papers for hypothesis generation.
    /// </summary>
    Task<List<string>> ExtractObservationsAsync(
        List<ResearchPaper> papers,
        CancellationToken ct = default);

    /// <summary>
    /// Identifies exploration opportunities from trending research.
    /// </summary>
    Task<List<ExplorationOpportunity>> IdentifyResearchOpportunitiesAsync(
        string domain,
        int maxOpportunities = 5,
        CancellationToken ct = default);

    /// <summary>
    /// Builds knowledge graph facts from citation networks.
    /// </summary>
    Task<List<string>> BuildKnowledgeGraphFactsAsync(
        List<ResearchPaper> papers,
        List<CitationMetadata> citations,
        CancellationToken ct = default);
}