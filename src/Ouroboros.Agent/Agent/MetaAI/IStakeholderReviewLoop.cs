namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Interface for stakeholder review loop orchestration.
/// </summary>
public interface IStakeholderReviewLoop
{
    /// <summary>
    /// Executes the complete stakeholder review workflow.
    /// Opens PR, requests reviews, resolves comments, and merges when approved.
    /// </summary>
    Task<Result<StakeholderReviewResult, string>> ExecuteReviewLoopAsync(
        string title,
        string description,
        string draftSpec,
        List<string> requiredReviewers,
        StakeholderReviewConfig? config = null,
        CancellationToken ct = default);

    /// <summary>
    /// Monitors an existing PR until all approvals are collected.
    /// </summary>
    Task<Result<ReviewState, string>> MonitorReviewProgressAsync(
        string prId,
        StakeholderReviewConfig? config = null,
        CancellationToken ct = default);

    /// <summary>
    /// Attempts to automatically resolve comments based on feedback.
    /// </summary>
    Task<Result<int, string>> ResolveCommentsAsync(
        string prId,
        List<ReviewComment> comments,
        CancellationToken ct = default);
}