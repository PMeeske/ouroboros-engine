using Ouroboros.Core.Ethics;
using Ouroboros.Domain.Events;

namespace Ouroboros.Agent.Cognition.Planning;

/// <summary>
/// Kleisli arrow: SelfModificationRequest → Result&lt;GovernanceDecision&gt;.
/// Orchestrates the full self-modification governance pipeline:
///   Propose → Evaluate → Approve → Execute → Audit.
/// Composes IEthicsFramework, ISafetyGuard, IHumanApprovalProvider, and IEventStore.
/// </summary>
public interface ISelfModificationGovernor
{
    /// <summary>
    /// Evaluates a self-modification proposal through the full governance pipeline.
    /// PEV: Propose (build enriched proposal) → Evaluate (ethics+safety) → Verify (approval).
    /// </summary>
    Task<Result<GovernanceDecision, string>> ProposeAsync(
        SelfModificationRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Executes an approved modification within an audited context.
    /// Returns a snapshot for rollback.
    /// </summary>
    Task<Result<ModificationSnapshot, string>> ExecuteAsync(
        GovernanceDecision decision,
        Func<CancellationToken, Task<Result<object, string>>> modificationAction,
        CancellationToken ct = default);

    /// <summary>
    /// Rolls back a previously executed modification using its snapshot.
    /// </summary>
    Task<Result<bool, string>> RollbackAsync(
        ModificationSnapshot snapshot,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves the full audit trail for a modification proposal.
    /// </summary>
    Task<IReadOnlyList<PipelineEvent>> GetAuditTrailAsync(
        Guid proposalId,
        CancellationToken ct = default);
}
