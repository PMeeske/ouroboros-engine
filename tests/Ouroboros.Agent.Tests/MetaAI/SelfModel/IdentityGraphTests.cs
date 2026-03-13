// <copyright file="IdentityGraphTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Agent.MetaAI.SelfModel;
using AgentCapability = Ouroboros.Agent.MetaAI.AgentCapability;
using Plan = Ouroboros.Agent.MetaAI.Plan;
using PlanStep = Ouroboros.Agent.PlanStep;

namespace Ouroboros.Agent.Tests.MetaAI.SelfModel;

/// <summary>
/// Unit tests for the IdentityGraph agent self-model component.
/// </summary>
[Trait("Category", "Unit")]
public class IdentityGraphTests
{
    private readonly Mock<ICapabilityRegistry> _mockCapabilityRegistry;
    private readonly IdentityGraph _sut;
    private readonly Guid _agentId;

    public IdentityGraphTests()
    {
        _agentId = Guid.NewGuid();
        _mockCapabilityRegistry = new Mock<ICapabilityRegistry>();
        _mockCapabilityRegistry
            .Setup(r => r.GetCapabilitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentCapability>());

        _sut = new IdentityGraph(_agentId, "TestAgent", _mockCapabilityRegistry.Object);
    }

    [Fact]
    public void Constructor_InitializesDefaultResources()
    {
        // Assert — CPU, Memory, and Attention resources should exist by default
        AgentResource? cpu = _sut.GetResource("CPU");
        AgentResource? memory = _sut.GetResource("Memory");
        AgentResource? attention = _sut.GetResource("Attention");

        cpu.Should().NotBeNull();
        cpu!.Name.Should().Be("CPU");
        cpu.Unit.Should().Be("cores");

        memory.Should().NotBeNull();
        memory!.Name.Should().Be("Memory");
        memory.Unit.Should().Be("MB");

        attention.Should().NotBeNull();
        attention!.Name.Should().Be("Attention");
        attention.Unit.Should().Be("units");
    }

    [Fact]
    public void Constructor_NullAgentName_ThrowsArgumentNullException()
    {
        // Act & Assert
        FluentActions.Invoking(() => new IdentityGraph(Guid.NewGuid(), null!, _mockCapabilityRegistry.Object))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullCapabilityRegistry_ThrowsArgumentNullException()
    {
        // Act & Assert
        FluentActions.Invoking(() => new IdentityGraph(Guid.NewGuid(), "agent", null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task GetStateAsync_ReturnsCompleteIdentityState()
    {
        // Arrange — set up capabilities mock with a real capability
        var capabilities = new List<AgentCapability>
        {
            new AgentCapability(
                "Reasoning", "Can reason about problems",
                new List<string> { "llm" }, 0.95, 500.0,
                new List<string>(), 10, DateTime.UtcNow, DateTime.UtcNow,
                new Dictionary<string, object>())
        };
        _mockCapabilityRegistry
            .Setup(r => r.GetCapabilitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(capabilities);

        // Act
        AgentIdentityState state = await _sut.GetStateAsync();

        // Assert
        state.AgentId.Should().Be(_agentId);
        state.Name.Should().Be("TestAgent");
        state.Capabilities.Should().HaveCount(1);
        state.Resources.Should().HaveCountGreaterThanOrEqualTo(3);
        state.Performance.Should().NotBeNull();
        state.StateTimestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void RegisterResource_AddsOrUpdatesResource()
    {
        // Arrange
        var customResource = new AgentResource(
            "GPU", "Computation", 4.0, 4.0, "units", DateTime.UtcNow, new Dictionary<string, object>());

        // Act
        _sut.RegisterResource(customResource);

        // Assert
        AgentResource? retrieved = _sut.GetResource("GPU");
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("GPU");
        retrieved.Available.Should().Be(4.0);
        retrieved.Total.Should().Be(4.0);
    }

    [Fact]
    public void GetResource_NonExistent_ReturnsNull()
    {
        // Act
        AgentResource? result = _sut.GetResource("NonExistent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void CreateCommitment_ReturnsPlannedCommitmentWithClampedPriority()
    {
        // Arrange
        DateTime deadline = DateTime.UtcNow.AddDays(7);

        // Act
        AgentCommitment commitment = _sut.CreateCommitment("Deliver report", deadline, 1.5);

        // Assert
        commitment.Should().NotBeNull();
        commitment.Description.Should().Be("Deliver report");
        commitment.Deadline.Should().Be(deadline);
        commitment.Priority.Should().Be(1.0); // Clamped from 1.5
        commitment.Status.Should().Be(CommitmentStatus.Planned);
        commitment.ProgressPercent.Should().Be(0.0);
        commitment.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void UpdateCommitment_ChangesStatusAndProgress()
    {
        // Arrange
        AgentCommitment commitment = _sut.CreateCommitment("Task A", DateTime.UtcNow.AddDays(1), 0.8);

        // Act
        _sut.UpdateCommitment(commitment.Id, CommitmentStatus.InProgress, 50.0);

        // Assert
        List<AgentCommitment> active = _sut.GetActiveCommitments();
        AgentCommitment updated = active.First(c => c.Id == commitment.Id);
        updated.Status.Should().Be(CommitmentStatus.InProgress);
        updated.ProgressPercent.Should().Be(50.0);
    }

    [Fact]
    public void UpdateCommitment_ToCompleted_SetsCompletedAt()
    {
        // Arrange
        AgentCommitment commitment = _sut.CreateCommitment("Finish task", DateTime.UtcNow.AddDays(1), 0.9);

        // Act
        _sut.UpdateCommitment(commitment.Id, CommitmentStatus.Completed, 100.0);

        // Assert — completed commitment should no longer appear in active list
        List<AgentCommitment> active = _sut.GetActiveCommitments();
        active.Should().NotContain(c => c.Id == commitment.Id);
    }

    [Fact]
    public void GetActiveCommitments_ReturnsOnlyPlannedInProgressAndAtRisk()
    {
        // Arrange
        AgentCommitment planned = _sut.CreateCommitment("Planned", DateTime.UtcNow.AddDays(7), 0.5);
        AgentCommitment inProgress = _sut.CreateCommitment("InProgress", DateTime.UtcNow.AddDays(3), 0.8);
        AgentCommitment completed = _sut.CreateCommitment("Completed", DateTime.UtcNow.AddDays(1), 0.9);

        _sut.UpdateCommitment(inProgress.Id, CommitmentStatus.InProgress, 30.0);
        _sut.UpdateCommitment(completed.Id, CommitmentStatus.Completed, 100.0);

        // Act
        List<AgentCommitment> active = _sut.GetActiveCommitments();

        // Assert
        active.Should().HaveCount(2);
        active.Should().Contain(c => c.Id == planned.Id);
        active.Should().Contain(c => c.Id == inProgress.Id);
        active.Should().NotContain(c => c.Id == completed.Id);
    }

    [Fact]
    public void RecordTaskResult_AffectsPerformanceSummary()
    {
        // Arrange
        var plan = new Plan("Test goal", new List<PlanStep>(), new Dictionary<string, double>(), DateTime.UtcNow);

        var successResult = new PlanExecutionResult(
            plan, Array.Empty<StepResult>(), true, "ok",
            new Dictionary<string, object>(), TimeSpan.FromMilliseconds(200));

        var failResult = new PlanExecutionResult(
            plan, Array.Empty<StepResult>(), false, "error",
            new Dictionary<string, object>(), TimeSpan.FromMilliseconds(500));

        // Act
        _sut.RecordTaskResult(successResult);
        _sut.RecordTaskResult(failResult);

        // Assert
        AgentPerformance perf = _sut.GetPerformanceSummary(TimeSpan.FromDays(1));
        perf.TotalTasks.Should().Be(2);
        perf.SuccessfulTasks.Should().Be(1);
        perf.FailedTasks.Should().Be(1);
        perf.OverallSuccessRate.Should().BeApproximately(0.5, 0.01);
        perf.AverageResponseTime.Should().BeApproximately(350.0, 1.0);
    }

    [Fact]
    public void GetPerformanceSummary_NoTasks_ReturnsZeroMetrics()
    {
        // Act
        AgentPerformance perf = _sut.GetPerformanceSummary(TimeSpan.FromDays(30));

        // Assert
        perf.TotalTasks.Should().Be(0);
        perf.SuccessfulTasks.Should().Be(0);
        perf.FailedTasks.Should().Be(0);
        perf.OverallSuccessRate.Should().Be(0.0);
        perf.ResourceUtilization.Should().NotBeNull();
    }
}
