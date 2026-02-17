namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Configuration for stakeholder review loop.
/// </summary>
public sealed record StakeholderReviewConfig(
    int MinimumRequiredApprovals = 2,
    bool RequireAllReviewersApprove = true,
    bool AutoResolveNonBlockingComments = false,
    TimeSpan ReviewTimeout = default,
    TimeSpan PollingInterval = default);