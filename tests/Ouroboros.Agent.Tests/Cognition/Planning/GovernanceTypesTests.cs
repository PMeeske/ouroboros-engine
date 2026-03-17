// <copyright file="GovernanceTypesTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.Cognition.Planning;
using Ouroboros.Core.Ethics;

namespace Ouroboros.Agent.Tests.Cognition.Planning;

/// <summary>
/// Unit tests for governance types: <see cref="GovernanceOutcome"/>,
/// <see cref="ModificationProposal"/>, <see cref="GovernanceDecision"/>,
/// and <see cref="ModificationSnapshot"/>.
/// </summary>
[Trait("Category", "Unit")]
public class GovernanceTypesTests
{
    // --- GovernanceOutcome ---

    [Fact]
    public void GovernanceOutcome_HasExpectedValues()
    {
        Enum.GetValues<GovernanceOutcome>().Should().HaveCount(6);
        Enum.IsDefined(GovernanceOutcome.Approved).Should().BeTrue();
        Enum.IsDefined(GovernanceOutcome.ApprovedByHuman).Should().BeTrue();
        Enum.IsDefined(GovernanceOutcome.DeniedByEthics).Should().BeTrue();
        Enum.IsDefined(GovernanceOutcome.DeniedBySafety).Should().BeTrue();
        Enum.IsDefined(GovernanceOutcome.DeniedByHuman).Should().BeTrue();
        Enum.IsDefined(GovernanceOutcome.TimedOut).Should().BeTrue();
    }

    // --- ModificationProposal ---

    [Fact]
    public void ModificationProposal_SetsRequiredProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var request = CreateRequest();
        var clearance = new EthicalClearance
        {
            Level = EthicalClearanceLevel.Permitted,
            Reasoning = "All clear"
        };
        var safety = new SafetyCheckResult { IsAllowed = true, RiskScore = 0.1 };

        // Act
        var proposal = new ModificationProposal
        {
            Id = id,
            Request = request,
            EthicsClearance = clearance,
            SafetyResult = safety,
            CompositeRiskScore = 0.15,
            RequiresHumanApproval = false
        };

        // Assert
        proposal.Id.Should().Be(id);
        proposal.Request.Should().Be(request);
        proposal.EthicsClearance.Should().Be(clearance);
        proposal.SafetyResult.Should().Be(safety);
        proposal.CompositeRiskScore.Should().Be(0.15);
        proposal.RequiresHumanApproval.Should().BeFalse();
    }

    [Fact]
    public void ModificationProposal_CreatedAt_DefaultsToNow()
    {
        // Arrange & Act
        var before = DateTimeOffset.UtcNow;
        var proposal = new ModificationProposal
        {
            Id = Guid.NewGuid(),
            Request = CreateRequest(),
            EthicsClearance = new EthicalClearance
            {
                Level = EthicalClearanceLevel.Permitted,
                Reasoning = "ok"
            },
            SafetyResult = new SafetyCheckResult { IsAllowed = true, RiskScore = 0 },
            CompositeRiskScore = 0,
            RequiresHumanApproval = false
        };
        var after = DateTimeOffset.UtcNow;

        // Assert
        proposal.CreatedAt.Should().BeOnOrAfter(before);
        proposal.CreatedAt.Should().BeOnOrBefore(after);
    }

    // --- GovernanceDecision ---

    [Fact]
    public void GovernanceDecision_IsApproved_WhenOutcomeIsApproved()
    {
        var decision = CreateDecision(GovernanceOutcome.Approved);

        decision.IsApproved.Should().BeTrue();
    }

    [Fact]
    public void GovernanceDecision_IsApproved_WhenOutcomeIsApprovedByHuman()
    {
        var decision = CreateDecision(GovernanceOutcome.ApprovedByHuman);

        decision.IsApproved.Should().BeTrue();
    }

    [Theory]
    [InlineData(GovernanceOutcome.DeniedByEthics)]
    [InlineData(GovernanceOutcome.DeniedBySafety)]
    [InlineData(GovernanceOutcome.DeniedByHuman)]
    [InlineData(GovernanceOutcome.TimedOut)]
    public void GovernanceDecision_IsNotApproved_WhenDeniedOrTimedOut(GovernanceOutcome outcome)
    {
        var decision = CreateDecision(outcome);

        decision.IsApproved.Should().BeFalse();
    }

    [Fact]
    public void GovernanceDecision_DecidedAt_DefaultsToNow()
    {
        var before = DateTimeOffset.UtcNow;
        var decision = CreateDecision(GovernanceOutcome.Approved);
        var after = DateTimeOffset.UtcNow;

        decision.DecidedAt.Should().BeOnOrAfter(before);
        decision.DecidedAt.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void GovernanceDecision_ApprovalResponse_DefaultsToNull()
    {
        var decision = CreateDecision(GovernanceOutcome.Approved);

        decision.ApprovalResponse.Should().BeNull();
    }

    // --- ModificationSnapshot ---

    [Fact]
    public void ModificationSnapshot_SetsRequiredProperties()
    {
        // Arrange
        var proposalId = Guid.NewGuid();
        var snapshotId = Guid.NewGuid();
        var state = new Dictionary<string, object> { ["version"] = 1 };

        // Act
        var snapshot = new ModificationSnapshot
        {
            ProposalId = proposalId,
            SnapshotId = snapshotId,
            StreamId = "stream-1",
            EventStoreVersion = 42,
            PreModificationState = state,
            OriginalRequest = CreateRequest()
        };

        // Assert
        snapshot.ProposalId.Should().Be(proposalId);
        snapshot.SnapshotId.Should().Be(snapshotId);
        snapshot.StreamId.Should().Be("stream-1");
        snapshot.EventStoreVersion.Should().Be(42);
        snapshot.PreModificationState.Should().ContainKey("version");
    }

    [Fact]
    public void ModificationSnapshot_CapturedAt_DefaultsToNow()
    {
        var before = DateTimeOffset.UtcNow;
        var snapshot = new ModificationSnapshot
        {
            ProposalId = Guid.NewGuid(),
            SnapshotId = Guid.NewGuid(),
            StreamId = "s",
            EventStoreVersion = 0,
            PreModificationState = new Dictionary<string, object>(),
            OriginalRequest = CreateRequest()
        };
        var after = DateTimeOffset.UtcNow;

        snapshot.CapturedAt.Should().BeOnOrAfter(before);
        snapshot.CapturedAt.Should().BeOnOrBefore(after);
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

    private static GovernanceDecision CreateDecision(GovernanceOutcome outcome) => new()
    {
        ProposalId = Guid.NewGuid(),
        Proposal = new ModificationProposal
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
        },
        Outcome = outcome,
        Reasoning = "Test reasoning"
    };
}
