namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents an insight gained from self-reflection.
/// </summary>
public sealed record Insight(
    string Category,
    string Description,
    double Confidence,
    List<string> SupportingEvidence,
    DateTime DiscoveredAt);