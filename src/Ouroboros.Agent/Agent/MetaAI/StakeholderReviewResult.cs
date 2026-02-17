namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Result of a stakeholder review loop execution.
/// </summary>
public sealed record StakeholderReviewResult(
    ReviewState FinalState,
    bool AllApproved,
    int TotalReviewers,
    int ApprovedCount,
    int CommentsResolved,
    int CommentsRemaining,
    TimeSpan Duration,
    string Summary);