namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents a PR (Pull Request) for stakeholder review.
/// </summary>
public sealed record PullRequest(
    string Id,
    string Title,
    string Description,
    string DraftSpec,
    List<string> RequiredReviewers,
    DateTime CreatedAt);