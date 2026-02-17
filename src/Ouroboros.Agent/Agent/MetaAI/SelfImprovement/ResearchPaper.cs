namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents a research paper from external sources.
/// </summary>
public sealed record ResearchPaper(
    string Id,
    string Title,
    string Authors,
    string Abstract,
    string Category,
    string Url,
    DateTime? PublishedDate = null);