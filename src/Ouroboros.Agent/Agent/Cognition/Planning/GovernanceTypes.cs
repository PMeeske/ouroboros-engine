using Ouroboros.Core.Ethics;

namespace Ouroboros.Agent.Cognition.Planning;

/// <summary>
/// Outcome of the self-modification governance pipeline.
/// </summary>
public enum GovernanceOutcome
{
    /// <summary>Modification approved automatically (low risk, ethics permitted).</summary>
    Approved,

    /// <summary>Modification approved after human review.</summary>
    ApprovedByHuman,

    /// <summary>Modification denied by ethics framework.</summary>
    DeniedByEthics,

    /// <summary>Modification denied by safety guard.</summary>
    DeniedBySafety,

    /// <summary>Modification denied by human reviewer.</summary>
    DeniedByHuman,

    /// <summary>Human approval timed out (treated as denied).</summary>
    TimedOut
}

/// <summary>
/// Enriched proposal: the original request annotated with ethics and safety analysis.
/// Produced during the Evaluate phase of the PEV pipeline.
/// </summary>
public sealed record ModificationProposal
{
    public required Guid Id { get; init; }
    public required SelfModificationRequest Request { get; init; }
    public required EthicalClearance EthicsClearance { get; init; }
    public required SafetyCheckResult SafetyResult { get; init; }
    public required double CompositeRiskScore { get; init; }
    public required bool RequiresHumanApproval { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Final governance decision: the composite ruling from all evaluation sources.
/// Output of ISelfModificationGovernor.ProposeAsync.
/// </summary>
public sealed record GovernanceDecision
{
    public required Guid ProposalId { get; init; }
    public required ModificationProposal Proposal { get; init; }
    public required GovernanceOutcome Outcome { get; init; }
    public required string Reasoning { get; init; }
    public HumanApprovalResponse? ApprovalResponse { get; init; }
    public bool IsApproved => Outcome is GovernanceOutcome.Approved or GovernanceOutcome.ApprovedByHuman;
    public DateTimeOffset DecidedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Snapshot of pre-modification state for rollback capability.
/// </summary>
public sealed record ModificationSnapshot
{
    public required Guid ProposalId { get; init; }
    public required Guid SnapshotId { get; init; }
    public required string StreamId { get; init; }
    public required long EventStoreVersion { get; init; }
    public required IReadOnlyDictionary<string, object> PreModificationState { get; init; }
    public required SelfModificationRequest OriginalRequest { get; init; }
    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;
}
