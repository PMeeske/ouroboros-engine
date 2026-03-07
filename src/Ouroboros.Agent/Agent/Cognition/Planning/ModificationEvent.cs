using Ouroboros.Domain.Events;

namespace Ouroboros.Agent.Cognition.Planning;

/// <summary>
/// Event: a self-modification was proposed and evaluated.
/// </summary>
public sealed record ModificationProposedEvent(
    Guid Id, DateTime Timestamp, ModificationProposal Proposal)
    : PipelineEvent(Id, "ModificationProposed", Timestamp);

/// <summary>
/// Event: a governance decision was reached.
/// </summary>
public sealed record ModificationDecidedEvent(
    Guid Id, DateTime Timestamp, GovernanceDecision Decision)
    : PipelineEvent(Id, "ModificationDecided", Timestamp);

/// <summary>
/// Event: an approved modification was executed successfully.
/// </summary>
public sealed record ModificationExecutedEvent(
    Guid Id, DateTime Timestamp, Guid ProposalId, ModificationSnapshot Snapshot)
    : PipelineEvent(Id, "ModificationExecuted", Timestamp);

/// <summary>
/// Event: a modification execution failed.
/// </summary>
public sealed record ModificationFailedEvent(
    Guid Id, DateTime Timestamp, Guid ProposalId, string Error)
    : PipelineEvent(Id, "ModificationFailed", Timestamp);

/// <summary>
/// Event: a previously executed modification was rolled back.
/// </summary>
public sealed record ModificationRolledBackEvent(
    Guid Id, DateTime Timestamp, Guid ProposalId, Guid SnapshotId, string Reason)
    : PipelineEvent(Id, "ModificationRolledBack", Timestamp);
