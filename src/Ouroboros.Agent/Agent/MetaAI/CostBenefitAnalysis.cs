namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents a cost-benefit analysis result.
/// </summary>
public sealed record CostBenefitAnalysis(
    string RecommendedRoute,
    double EstimatedCost,
    double EstimatedQuality,
    double ValueScore,
    string Rationale);