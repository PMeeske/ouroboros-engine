using Ouroboros.Agent.MetaAI;
using Ouroboros.Core.Ethics;
using Ouroboros.Domain.Events;
using Ouroboros.Domain.Persistence;

namespace Ouroboros.Agent.Cognition.Planning;

/// <summary>
/// PEV-style self-modification governor.
///
/// Propose:  Build ModificationProposal by querying IEthicsFramework + ISafetyGuard.
/// Evaluate: Compute composite risk and determine if human approval is needed.
/// Verify:   Route through IHumanApprovalProvider when required.
///
/// All state transitions are recorded via IEventStore for audit.
/// </summary>
public sealed class SelfModificationGovernor : ISelfModificationGovernor
{
    private readonly IEthicsFramework _ethics;
    private readonly ISafetyGuard _safety;
    private readonly IHumanApprovalProvider _approval;
    private readonly IEventStore _eventStore;

    private const string StreamPrefix = "self-mod:";
    private const double AutoApprovalThreshold = 0.3;

    public SelfModificationGovernor(
        IEthicsFramework ethics,
        ISafetyGuard safety,
        IHumanApprovalProvider approval,
        IEventStore eventStore)
    {
        ArgumentNullException.ThrowIfNull(ethics);
        _ethics = ethics;
        ArgumentNullException.ThrowIfNull(safety);
        _safety = safety;
        ArgumentNullException.ThrowIfNull(approval);
        _approval = approval;
        ArgumentNullException.ThrowIfNull(eventStore);
        _eventStore = eventStore;
    }

    public async Task<Result<GovernanceDecision, string>> ProposeAsync(
        SelfModificationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var proposalId = Guid.NewGuid();
        var streamId = StreamPrefix + proposalId;

        try
        {
            // ---- Phase 1: PROPOSE ---- Run ethics + safety in parallel
            var ethicsTask = _ethics.EvaluateSelfModificationAsync(request, ct);
            var safetyTask = _safety.CheckActionSafetyAsync(
                $"self-modification:{request.Type}",
                new Dictionary<string, object>
                {
                    ["description"] = request.Description,
                    ["impactLevel"] = request.ImpactLevel,
                    ["isReversible"] = request.IsReversible,
                    ["modificationType"] = request.Type.ToString()
                },
                context: null, ct);

            await Task.WhenAll(ethicsTask, safetyTask).ConfigureAwait(false);

            var ethicsResult = await ethicsTask.ConfigureAwait(false);
            var safetyResult = await safetyTask.ConfigureAwait(false);

            if (ethicsResult.IsFailure)
                return Result<GovernanceDecision, string>.Failure(
                    $"Ethics evaluation failed: {ethicsResult.Error}");

            // ---- Phase 2: EVALUATE ---- Composite risk scoring
            var clearance = ethicsResult.Value;
            double compositeRisk = ComputeCompositeRisk(
                request.ImpactLevel, safetyResult.RiskScore, clearance);

            bool needsHumanApproval =
                clearance.Level == EthicalClearanceLevel.RequiresHumanApproval
                || !safetyResult.IsAllowed
                || compositeRisk > AutoApprovalThreshold
                || !request.IsReversible;

            var proposal = new ModificationProposal
            {
                Id = proposalId,
                Request = request,
                EthicsClearance = clearance,
                SafetyResult = safetyResult,
                CompositeRiskScore = compositeRisk,
                RequiresHumanApproval = needsHumanApproval
            };

            // Emit proposed event
            await _eventStore.AppendEventsAsync(streamId, [new ModificationProposedEvent(Guid.NewGuid(), DateTime.UtcNow, proposal)], cancellationToken: ct).ConfigureAwait(false);

            // Hard deny: ethics denied
            if (clearance.Level == EthicalClearanceLevel.Denied)
            {
                var denied = BuildDecision(proposal, GovernanceOutcome.DeniedByEthics,
                    clearance.Reasoning);
                await EmitDecidedEvent(streamId, denied).ConfigureAwait(false);
                return Result<GovernanceDecision, string>.Success(denied);
            }

            // Hard deny: safety blocked with critical violations
            if (!safetyResult.IsAllowed && safetyResult.RiskScore >= 0.9)
            {
                var denied = BuildDecision(proposal, GovernanceOutcome.DeniedBySafety,
                    safetyResult.Reason);
                await EmitDecidedEvent(streamId, denied).ConfigureAwait(false);
                return Result<GovernanceDecision, string>.Success(denied);
            }

            // ---- Phase 3: VERIFY ---- Human approval gate
            if (needsHumanApproval)
            {
                var approvalRequest = new HumanApprovalRequest
                {
                    Category = "self-modification",
                    Description = FormatApprovalDescription(request, compositeRisk),
                    Clearance = clearance,
                    Context = new Dictionary<string, object>
                    {
                        ["proposalId"] = proposalId,
                        ["modificationType"] = request.Type.ToString(),
                        ["compositeRisk"] = compositeRisk,
                        ["isReversible"] = request.IsReversible
                    }
                };

                var approvalResponse = await _approval.RequestApprovalAsync(approvalRequest, ct).ConfigureAwait(false);

                var outcome = approvalResponse.Decision switch
                {
                    HumanApprovalDecision.Approved => GovernanceOutcome.ApprovedByHuman,
                    HumanApprovalDecision.Rejected => GovernanceOutcome.DeniedByHuman,
                    HumanApprovalDecision.TimedOut => GovernanceOutcome.TimedOut,
                    _ => GovernanceOutcome.DeniedByHuman
                };

                var decision = BuildDecision(proposal, outcome,
                    approvalResponse.ReviewerComments ?? outcome.ToString(), approvalResponse);
                await EmitDecidedEvent(streamId, decision).ConfigureAwait(false);
                return Result<GovernanceDecision, string>.Success(decision);
            }

            // Auto-approved: low risk, reversible, ethics permitted
            var autoApproved = BuildDecision(proposal, GovernanceOutcome.Approved,
                "Auto-approved: low risk, reversible, ethics compliant");
            await EmitDecidedEvent(streamId, autoApproved).ConfigureAwait(false);
            return Result<GovernanceDecision, string>.Success(autoApproved);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<GovernanceDecision, string>.Failure(
                $"Governance pipeline failed: {ex.Message}");
        }
    }

    public async Task<Result<ModificationSnapshot, string>> ExecuteAsync(
        GovernanceDecision decision,
        Func<CancellationToken, Task<Result<object, string>>> modificationAction,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(modificationAction);

        if (!decision.IsApproved)
            return Result<ModificationSnapshot, string>.Failure(
                $"Cannot execute unapproved modification: {decision.Outcome}");

        var streamId = StreamPrefix + decision.ProposalId;

        try
        {
            // Capture pre-modification snapshot
            var version = await _eventStore.GetVersionAsync(streamId, ct).ConfigureAwait(false);
            var snapshot = new ModificationSnapshot
            {
                ProposalId = decision.ProposalId,
                SnapshotId = Guid.NewGuid(),
                StreamId = streamId,
                EventStoreVersion = version,
                PreModificationState = new Dictionary<string, object>
                {
                    ["capturedAt"] = DateTimeOffset.UtcNow,
                    ["modificationType"] = decision.Proposal.Request.Type.ToString(),
                    ["compositeRisk"] = decision.Proposal.CompositeRiskScore
                },
                OriginalRequest = decision.Proposal.Request
            };

            // Execute the modification
            var result = await modificationAction(ct).ConfigureAwait(false);

            if (result.IsFailure)
            {
                await _eventStore.AppendEventsAsync(streamId, [new ModificationFailedEvent(
                        Guid.NewGuid(), DateTime.UtcNow, decision.ProposalId, result.Error)], cancellationToken: ct).ConfigureAwait(false);
                return Result<ModificationSnapshot, string>.Failure(
                    $"Modification execution failed: {result.Error}");
            }

            // Record successful execution
            await _eventStore.AppendEventsAsync(streamId, [new ModificationExecutedEvent(
                    Guid.NewGuid(), DateTime.UtcNow, decision.ProposalId, snapshot)], cancellationToken: ct).ConfigureAwait(false);

            return Result<ModificationSnapshot, string>.Success(snapshot);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await _eventStore.AppendEventsAsync(streamId, [new ModificationFailedEvent(
                    Guid.NewGuid(), DateTime.UtcNow, decision.ProposalId, ex.Message)], cancellationToken: ct).ConfigureAwait(false);
            return Result<ModificationSnapshot, string>.Failure(
                $"Modification execution failed: {ex.Message}");
        }
    }

    public async Task<Result<bool, string>> RollbackAsync(
        ModificationSnapshot snapshot, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        try
        {
            await _eventStore.AppendEventsAsync(snapshot.StreamId, [new ModificationRolledBackEvent(
                    Guid.NewGuid(), DateTime.UtcNow,
                    snapshot.ProposalId, snapshot.SnapshotId,
                    $"Rollback to version {snapshot.EventStoreVersion}")], cancellationToken: ct).ConfigureAwait(false);

            return Result<bool, string>.Success(true);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<bool, string>.Failure($"Rollback failed: {ex.Message}");
        }
    }

    public async Task<IReadOnlyList<PipelineEvent>> GetAuditTrailAsync(
        Guid proposalId, CancellationToken ct = default)
    {
        var streamId = StreamPrefix + proposalId;
        return await _eventStore.GetEventsAsync(streamId, cancellationToken: ct).ConfigureAwait(false);
    }

    private static double ComputeCompositeRisk(
        double impactLevel, double safetyRisk, EthicalClearance clearance)
    {
        double ethicsRisk = clearance.Level switch
        {
            EthicalClearanceLevel.Permitted => 0.0,
            EthicalClearanceLevel.PermittedWithConcerns => 0.3,
            EthicalClearanceLevel.RequiresHumanApproval => 0.7,
            EthicalClearanceLevel.Denied => 1.0,
            _ => 0.5
        };
        return 0.4 * ethicsRisk + 0.3 * safetyRisk + 0.3 * impactLevel;
    }

    private static GovernanceDecision BuildDecision(
        ModificationProposal proposal,
        GovernanceOutcome outcome,
        string reasoning,
        HumanApprovalResponse? approvalResponse = null) => new()
    {
        ProposalId = proposal.Id,
        Proposal = proposal,
        Outcome = outcome,
        Reasoning = reasoning,
        ApprovalResponse = approvalResponse
    };

    private async Task EmitDecidedEvent(string streamId, GovernanceDecision decision)
    {
        await _eventStore.AppendEventsAsync(streamId,
            [new ModificationDecidedEvent(Guid.NewGuid(), DateTime.UtcNow, decision)]).ConfigureAwait(false);
    }

    private static string FormatApprovalDescription(
        SelfModificationRequest request, double compositeRisk) =>
        $"[{request.Type}] {request.Description} " +
        $"(impact: {request.ImpactLevel:F2}, risk: {compositeRisk:F2}, " +
        $"reversible: {request.IsReversible})\n" +
        $"Justification: {request.Justification}";
}
