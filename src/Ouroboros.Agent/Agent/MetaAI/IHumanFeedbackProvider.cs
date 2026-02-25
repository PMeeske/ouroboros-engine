namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Interface for human feedback provider.
/// </summary>
public interface IHumanFeedbackProvider
{
    /// <summary>
    /// Requests feedback from human.
    /// </summary>
    Task<HumanFeedbackResponse> RequestFeedbackAsync(
        HumanFeedbackRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Requests approval for an action.
    /// </summary>
    Task<ApprovalResponse> RequestApprovalAsync(
        ApprovalRequest request,
        CancellationToken ct = default);
}