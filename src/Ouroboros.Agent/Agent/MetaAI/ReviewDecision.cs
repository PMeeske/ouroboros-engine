namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents a stakeholder's review decision.
/// </summary>
public sealed record ReviewDecision(
    string ReviewerId,
    bool Approved,
    string? Feedback,
    List<ReviewComment>? Comments,
    DateTime ReviewedAt);