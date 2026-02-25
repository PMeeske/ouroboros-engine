namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Interface for interacting with the PR/review system (GitHub, etc.).
/// </summary>
public interface IReviewSystemProvider
{
    /// <summary>
    /// Opens a new PR with the given spec.
    /// </summary>
    Task<Result<PullRequest, string>> OpenPullRequestAsync(
        string title,
        string description,
        string draftSpec,
        List<string> requiredReviewers,
        CancellationToken ct = default);

    /// <summary>
    /// Requests reviews from specified reviewers.
    /// </summary>
    Task<Result<bool, string>> RequestReviewersAsync(
        string prId,
        List<string> reviewers,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves review decisions for a PR.
    /// </summary>
    Task<Result<List<ReviewDecision>, string>> GetReviewDecisionsAsync(
        string prId,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves all comments on a PR.
    /// </summary>
    Task<Result<List<ReviewComment>, string>> GetCommentsAsync(
        string prId,
        CancellationToken ct = default);

    /// <summary>
    /// Resolves a specific comment.
    /// </summary>
    Task<Result<bool, string>> ResolveCommentAsync(
        string prId,
        string commentId,
        string resolution,
        CancellationToken ct = default);

    /// <summary>
    /// Merges the PR after all approvals are collected.
    /// </summary>
    Task<Result<bool, string>> MergePullRequestAsync(
        string prId,
        string mergeCommitMessage,
        CancellationToken ct = default);
}