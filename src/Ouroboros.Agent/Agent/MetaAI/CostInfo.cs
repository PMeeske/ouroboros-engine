namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents cost information for a model or operation.
/// </summary>
public sealed record CostInfo(
    string ResourceId,
    double CostPerToken,
    double CostPerRequest,
    double EstimatedQuality);