namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents citation metadata from Semantic Scholar.
/// </summary>
public sealed record CitationMetadata(
    string PaperId,
    string Title,
    int CitationCount,
    int InfluentialCitationCount,
    List<string> References,
    List<string> CitedBy);