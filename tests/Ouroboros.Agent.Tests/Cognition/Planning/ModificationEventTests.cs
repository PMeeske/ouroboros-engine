// <copyright file="ModificationEventTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.Cognition.Planning;
using Ouroboros.Core.Ethics;

namespace Ouroboros.Agent.Tests.Cognition.Planning;

/// <summary>
/// Unit tests for modification event records:
/// <see cref="ModificationProposedEvent"/>, <see cref="ModificationDecidedEvent"/>,
/// <see cref="ModificationExecutedEvent"/>, <see cref="ModificationFailedEvent"/>,
/// and <see cref="ModificationRolledBackEvent"/>.
/// </summary>
[Trait("Category", "Unit")]
public class ModificationEventTests
{
    [Fact]
    public void ModificationProposedEvent_SetsProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;
        var proposal = CreateProposal();

        // Act
        var evt = new ModificationProposedEvent(id, timestamp, proposal);

        // Assert
        evt.Id.Should().Be(id);
        evt.Timestamp.Should().Be(timestamp);
        evt.Proposal.Should().Be(proposal);
        evt.EventType.Should().Be("ModificationProposed");
    }

    [Fact]
    public void ModificationDecidedEvent_SetsProperties()
    {
        var id = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;
        var decision = CreateDecision();

        var evt = new ModificationDecidedEvent(id, timestamp, decision);

        evt.Id.Should().Be(id);
        evt.EventType.Should().Be("ModificationDecided");
        evt.Decision.Should().Be(decision);
    }

    [Fact]
    public void ModificationExecutedEvent_SetsProperties()
    {
        var id = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;
        var snapshot = CreateSnapshot(proposalId);

        var evt = new ModificationExecutedEvent(id, timestamp, proposalId, snapshot);

        evt.Id.Should().Be(id);
        evt.EventType.Should().Be("ModificationExecuted");
        evt.ProposalId.Should().Be(proposalId);
        evt.Snapshot.Should().Be(snapshot);
    }

    [Fact]
    public void ModificationFailedEvent_SetsProperties()
    {
        var id = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;

        var evt = new ModificationFailedEvent(id, timestamp, proposalId, "Execution timed out");

        evt.Id.Should().Be(id);
        evt.EventType.Should().Be("ModificationFailed");
        evt.ProposalId.Should().Be(proposalId);
        evt.Error.Should().Be("Execution timed out");
    }

    [Fact]
    public void ModificationRolledBackEvent_SetsProperties()
    {
        var id = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var snapshotId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;

        var evt = new ModificationRolledBackEvent(id, timestamp, proposalId, snapshotId, "Safety violation detected");

        evt.Id.Should().Be(id);
        evt.EventType.Should().Be("ModificationRolledBack");
        evt.ProposalId.Should().Be(proposalId);
        evt.SnapshotId.Should().Be(snapshotId);
        evt.Reason.Should().Be("Safety violation detected");
    }

    [Fact]
    public void AllEvents_AreRecords_SupportEquality()
    {
        var id = Guid.NewGuid();
        var ts = DateTime.UtcNow;
        var proposal = CreateProposal();

        var evt1 = new ModificationProposedEvent(id, ts, proposal);
        var evt2 = new ModificationProposedEvent(id, ts, proposal);

        evt1.Should().Be(evt2);
    }

    // --- Helpers ---

    private static SelfModificationRequest CreateRequest() => new()
    {
        Type = SelfModificationType.ParameterTuning,
        Description = "Test modification",
        Justification = "Testing",
        ImpactLevel = 0.5,
        IsReversible = true
    };

    private static ModificationProposal CreateProposal() => new()
    {
        Id = Guid.NewGuid(),
        Request = CreateRequest(),
        EthicsClearance = new EthicalClearance
        {
            Level = EthicalClearanceLevel.Permitted,
            Reasoning = "ok"
        },
        SafetyResult = new SafetyCheckResult { IsAllowed = true, RiskScore = 0 },
        CompositeRiskScore = 0.1,
        RequiresHumanApproval = false
    };

    private static GovernanceDecision CreateDecision() => new()
    {
        ProposalId = Guid.NewGuid(),
        Proposal = CreateProposal(),
        Outcome = GovernanceOutcome.Approved,
        Reasoning = "Approved by system"
    };

    private static ModificationSnapshot CreateSnapshot(Guid proposalId) => new()
    {
        ProposalId = proposalId,
        SnapshotId = Guid.NewGuid(),
        StreamId = "test-stream",
        EventStoreVersion = 1,
        PreModificationState = new Dictionary<string, object>(),
        OriginalRequest = CreateRequest()
    };
}
