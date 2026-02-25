namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Overall status of the review process.
/// </summary>
public enum ReviewStatus
{
    Draft,
    AwaitingReview,
    ChangesRequested,
    Approved,
    Merged
}