// <copyright file="SelfModificationGovernorTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.Cognition.Planning;
using Ouroboros.Core.Ethics;
using Ouroboros.Domain.Events;
using Ouroboros.Domain.Persistence;

namespace Ouroboros.Agent.Tests.Cognition.Planning;

/// <summary>
/// Unit tests for <see cref="SelfModificationGovernor"/>.
/// </summary>
[Trait("Category", "Unit")]
public class SelfModificationGovernorTests
{
    private readonly Mock<IEthicsFramework> _ethicsMock = new();
    private readonly Mock<ISafetyGuard> _safetyMock = new();
    private readonly Mock<IHumanApprovalProvider> _approvalMock = new();
    private readonly Mock<IEventStore> _eventStoreMock = new();

    private SelfModificationGovernor CreateSut() =>
        new(_ethicsMock.Object, _safetyMock.Object, _approvalMock.Object, _eventStoreMock.Object);

    // --- Constructor ---

    [Fact]
    public void Constructor_NullEthics_ThrowsArgumentNullException()
    {
        var act = () => new SelfModificationGovernor(
            null!, _safetyMock.Object, _approvalMock.Object, _eventStoreMock.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullSafety_ThrowsArgumentNullException()
    {
        var act = () => new SelfModificationGovernor(
            _ethicsMock.Object, null!, _approvalMock.Object, _eventStoreMock.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullApproval_ThrowsArgumentNullException()
    {
        var act = () => new SelfModificationGovernor(
            _ethicsMock.Object, _safetyMock.Object, null!, _eventStoreMock.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullEventStore_ThrowsArgumentNullException()
    {
        var act = () => new SelfModificationGovernor(
            _ethicsMock.Object, _safetyMock.Object, _approvalMock.Object, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ValidParams_DoesNotThrow()
    {
        var act = () => CreateSut();
        act.Should().NotThrow();
    }

    // --- ProposeAsync ---

    [Fact]
    public async Task ProposeAsync_NullRequest_ThrowsArgumentNullException()
    {
        var sut = CreateSut();
        var act = () => sut.ProposeAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ProposeAsync_EthicsDenied_ReturnsDeniedByEthics()
    {
        // Arrange
        var request = CreateRequest(impactLevel: 0.5, isReversible: true);
        SetupEthics(EthicalClearanceLevel.Denied, "Not allowed");
        SetupSafety(allowed: true, riskScore: 0.1);
        SetupEventStore();

        var sut = CreateSut();

        // Act
        var result = await sut.ProposeAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Outcome.Should().Be(GovernanceOutcome.DeniedByEthics);
        result.Value.IsApproved.Should().BeFalse();
    }

    [Fact]
    public async Task ProposeAsync_SafetyBlockedHighRisk_ReturnsDeniedBySafety()
    {
        // Arrange
        var request = CreateRequest(impactLevel: 0.9, isReversible: true);
        SetupEthics(EthicalClearanceLevel.Permitted, "OK");
        SetupSafety(allowed: false, riskScore: 0.95);
        SetupEventStore();

        var sut = CreateSut();

        // Act
        var result = await sut.ProposeAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Outcome.Should().Be(GovernanceOutcome.DeniedBySafety);
        result.Value.IsApproved.Should().BeFalse();
    }

    [Fact]
    public async Task ProposeAsync_LowRiskReversiblePermitted_AutoApproves()
    {
        // Arrange — low risk, reversible, ethics permitted
        var request = CreateRequest(impactLevel: 0.1, isReversible: true);
        SetupEthics(EthicalClearanceLevel.Permitted, "OK");
        SetupSafety(allowed: true, riskScore: 0.05);
        SetupEventStore();

        var sut = CreateSut();

        // Act
        var result = await sut.ProposeAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Outcome.Should().Be(GovernanceOutcome.Approved);
        result.Value.IsApproved.Should().BeTrue();
        result.Value.Reasoning.Should().Contain("Auto-approved");
    }

    [Fact]
    public async Task ProposeAsync_RequiresHumanApproval_RoutesToApprover()
    {
        // Arrange
        var request = CreateRequest(impactLevel: 0.5, isReversible: false);
        SetupEthics(EthicalClearanceLevel.RequiresHumanApproval, "Needs review");
        SetupSafety(allowed: true, riskScore: 0.3);
        SetupEventStore();
        _approvalMock
            .Setup(a => a.RequestApprovalAsync(It.IsAny<HumanApprovalRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HumanApprovalResponse
            {
                Decision = HumanApprovalDecision.Approved,
                ReviewerComments = "Looks good"
            });

        var sut = CreateSut();

        // Act
        var result = await sut.ProposeAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Outcome.Should().Be(GovernanceOutcome.ApprovedByHuman);
        result.Value.IsApproved.Should().BeTrue();
    }

    [Fact]
    public async Task ProposeAsync_HumanRejects_ReturnsDeniedByHuman()
    {
        // Arrange
        var request = CreateRequest(impactLevel: 0.5, isReversible: false);
        SetupEthics(EthicalClearanceLevel.RequiresHumanApproval, "Needs review");
        SetupSafety(allowed: true, riskScore: 0.3);
        SetupEventStore();
        _approvalMock
            .Setup(a => a.RequestApprovalAsync(It.IsAny<HumanApprovalRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HumanApprovalResponse
            {
                Decision = HumanApprovalDecision.Rejected,
                ReviewerComments = "Too risky"
            });

        var sut = CreateSut();

        // Act
        var result = await sut.ProposeAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Outcome.Should().Be(GovernanceOutcome.DeniedByHuman);
    }

    [Fact]
    public async Task ProposeAsync_HumanTimesOut_ReturnsTimedOut()
    {
        // Arrange
        var request = CreateRequest(impactLevel: 0.5, isReversible: false);
        SetupEthics(EthicalClearanceLevel.RequiresHumanApproval, "Needs review");
        SetupSafety(allowed: true, riskScore: 0.3);
        SetupEventStore();
        _approvalMock
            .Setup(a => a.RequestApprovalAsync(It.IsAny<HumanApprovalRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HumanApprovalResponse
            {
                Decision = HumanApprovalDecision.TimedOut
            });

        var sut = CreateSut();

        // Act
        var result = await sut.ProposeAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Outcome.Should().Be(GovernanceOutcome.TimedOut);
    }

    [Fact]
    public async Task ProposeAsync_EthicsEvaluationFails_ReturnsFailure()
    {
        // Arrange
        var request = CreateRequest(impactLevel: 0.5, isReversible: true);
        _ethicsMock
            .Setup(e => e.EvaluateSelfModificationAsync(It.IsAny<SelfModificationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<EthicalClearance, string>.Failure("Ethics service unavailable"));
        SetupSafety(allowed: true, riskScore: 0.1);
        SetupEventStore();

        var sut = CreateSut();

        // Act
        var result = await sut.ProposeAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Ethics evaluation failed");
    }

    [Fact]
    public async Task ProposeAsync_EmitsProposedEvent()
    {
        // Arrange
        var request = CreateRequest(impactLevel: 0.1, isReversible: true);
        SetupEthics(EthicalClearanceLevel.Permitted, "OK");
        SetupSafety(allowed: true, riskScore: 0.05);
        SetupEventStore();

        var sut = CreateSut();

        // Act
        await sut.ProposeAsync(request);

        // Assert — at least 2 events: proposed + decided
        _eventStoreMock.Verify(
            e => e.AppendEventsAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<PipelineEvent>>()),
            Times.AtLeast(2));
    }

    // --- ExecuteAsync ---

    [Fact]
    public async Task ExecuteAsync_NullDecision_ThrowsArgumentNullException()
    {
        var sut = CreateSut();
        var act = () => sut.ExecuteAsync(null!, _ => Task.FromResult(Result<object, string>.Success(new object())));
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteAsync_NullAction_ThrowsArgumentNullException()
    {
        var sut = CreateSut();
        var decision = CreateApprovedDecision();
        var act = () => sut.ExecuteAsync(decision, null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteAsync_UnapprovedDecision_ReturnsFailure()
    {
        // Arrange
        var decision = CreateDeniedDecision();
        var sut = CreateSut();

        // Act
        var result = await sut.ExecuteAsync(decision, _ => Task.FromResult(Result<object, string>.Success(new object())));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Cannot execute unapproved");
    }

    [Fact]
    public async Task ExecuteAsync_ApprovedDecision_SuccessfulAction_ReturnsSnapshot()
    {
        // Arrange
        var decision = CreateApprovedDecision();
        SetupEventStore();
        _eventStoreMock
            .Setup(e => e.GetVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1L);

        var sut = CreateSut();

        // Act
        var result = await sut.ExecuteAsync(
            decision,
            _ => Task.FromResult(Result<object, string>.Success(new object())));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ProposalId.Should().Be(decision.ProposalId);
    }

    [Fact]
    public async Task ExecuteAsync_ActionFails_ReturnsFailure()
    {
        // Arrange
        var decision = CreateApprovedDecision();
        SetupEventStore();
        _eventStoreMock
            .Setup(e => e.GetVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1L);

        var sut = CreateSut();

        // Act
        var result = await sut.ExecuteAsync(
            decision,
            _ => Task.FromResult(Result<object, string>.Failure("Action failed")));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Action failed");
    }

    // --- RollbackAsync ---

    [Fact]
    public async Task RollbackAsync_NullSnapshot_ThrowsArgumentNullException()
    {
        var sut = CreateSut();
        var act = () => sut.RollbackAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task RollbackAsync_ValidSnapshot_ReturnsSuccess()
    {
        // Arrange
        var snapshot = CreateSnapshot();
        SetupEventStore();
        var sut = CreateSut();

        // Act
        var result = await sut.RollbackAsync(snapshot);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task RollbackAsync_EmitsRollbackEvent()
    {
        // Arrange
        var snapshot = CreateSnapshot();
        SetupEventStore();
        var sut = CreateSut();

        // Act
        await sut.RollbackAsync(snapshot);

        // Assert
        _eventStoreMock.Verify(
            e => e.AppendEventsAsync(snapshot.StreamId, It.IsAny<IReadOnlyList<PipelineEvent>>()),
            Times.Once);
    }

    // --- GetAuditTrailAsync ---

    [Fact]
    public async Task GetAuditTrailAsync_ReturnsEventsFromStore()
    {
        // Arrange
        var proposalId = Guid.NewGuid();
        var events = new List<PipelineEvent>();
        _eventStoreMock
            .Setup(e => e.GetEventsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(events);

        var sut = CreateSut();

        // Act
        var result = await sut.GetAuditTrailAsync(proposalId);

        // Assert
        result.Should().BeSameAs(events);
    }

    // --- GovernanceDecision.IsApproved ---

    [Fact]
    public void GovernanceDecision_Approved_IsApprovedTrue()
    {
        var decision = CreateDecisionWithOutcome(GovernanceOutcome.Approved);
        decision.IsApproved.Should().BeTrue();
    }

    [Fact]
    public void GovernanceDecision_ApprovedByHuman_IsApprovedTrue()
    {
        var decision = CreateDecisionWithOutcome(GovernanceOutcome.ApprovedByHuman);
        decision.IsApproved.Should().BeTrue();
    }

    [Theory]
    [InlineData(GovernanceOutcome.DeniedByEthics)]
    [InlineData(GovernanceOutcome.DeniedBySafety)]
    [InlineData(GovernanceOutcome.DeniedByHuman)]
    [InlineData(GovernanceOutcome.TimedOut)]
    public void GovernanceDecision_DeniedOutcomes_IsApprovedFalse(GovernanceOutcome outcome)
    {
        var decision = CreateDecisionWithOutcome(outcome);
        decision.IsApproved.Should().BeFalse();
    }

    // --- Helpers ---

    private static SelfModificationRequest CreateRequest(double impactLevel, bool isReversible) =>
        new()
        {
            Description = "Test modification",
            Type = SelfModificationType.ParameterTuning,
            ImpactLevel = impactLevel,
            IsReversible = isReversible,
            Justification = "Testing"
        };

    private void SetupEthics(EthicalClearanceLevel level, string reasoning)
    {
        var clearance = new EthicalClearance
        {
            Level = level,
            Reasoning = reasoning
        };
        _ethicsMock
            .Setup(e => e.EvaluateSelfModificationAsync(It.IsAny<SelfModificationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<EthicalClearance, string>.Success(clearance));
    }

    private void SetupSafety(bool allowed, double riskScore)
    {
        _safetyMock
            .Setup(s => s.CheckActionSafetyAsync(
                It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SafetyCheckResult
            {
                IsAllowed = allowed,
                RiskScore = riskScore,
                Reason = allowed ? "Safe" : "Blocked"
            });
    }

    private void SetupEventStore()
    {
        _eventStoreMock
            .Setup(e => e.AppendEventsAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<PipelineEvent>>()))
            .Returns(Task.CompletedTask);
    }

    private static GovernanceDecision CreateApprovedDecision() =>
        CreateDecisionWithOutcome(GovernanceOutcome.Approved);

    private static GovernanceDecision CreateDeniedDecision() =>
        CreateDecisionWithOutcome(GovernanceOutcome.DeniedByEthics);

    private static GovernanceDecision CreateDecisionWithOutcome(GovernanceOutcome outcome)
    {
        var request = CreateRequest(0.5, true);
        var proposal = new ModificationProposal
        {
            Id = Guid.NewGuid(),
            Request = request,
            EthicsClearance = new EthicalClearance { Level = EthicalClearanceLevel.Permitted, Reasoning = "OK" },
            SafetyResult = new SafetyCheckResult { IsAllowed = true, RiskScore = 0.1, Reason = "Safe" },
            CompositeRiskScore = 0.2,
            RequiresHumanApproval = false
        };
        return new GovernanceDecision
        {
            ProposalId = proposal.Id,
            Proposal = proposal,
            Outcome = outcome,
            Reasoning = "Test reasoning"
        };
    }

    private static ModificationSnapshot CreateSnapshot() => new()
    {
        ProposalId = Guid.NewGuid(),
        SnapshotId = Guid.NewGuid(),
        StreamId = "self-mod:" + Guid.NewGuid(),
        EventStoreVersion = 1,
        PreModificationState = new Dictionary<string, object>(),
        OriginalRequest = CreateRequest(0.5, true)
    };
}
