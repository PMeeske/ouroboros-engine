namespace Ouroboros.Agent.MetaAI.Affect;

/// <summary>
/// Result of threat/opportunity appraisal.
/// </summary>
public sealed record TaskAppraisal(
    double ThreatLevel,
    double OpportunityScore,
    double UrgencyFactor,
    double RelevanceScore,
    string Rationale);