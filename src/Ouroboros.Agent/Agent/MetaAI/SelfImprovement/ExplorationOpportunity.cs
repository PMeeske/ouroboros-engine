namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents a novel exploration opportunity.
/// </summary>
public sealed record ExplorationOpportunity(
    string Description,
    double NoveltyScore,
    double InformationGainEstimate,
    List<string> Prerequisites,
    DateTime IdentifiedAt);