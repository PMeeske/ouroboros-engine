namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents the state of a PR review process.
/// </summary>
public sealed record ReviewState(
    PullRequest PR,
    List<ReviewDecision> Reviews,
    List<ReviewComment> AllComments,
    ReviewStatus Status,
    DateTime LastUpdatedAt);