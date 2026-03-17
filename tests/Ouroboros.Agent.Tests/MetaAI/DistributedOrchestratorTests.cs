// <copyright file="DistributedOrchestratorTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;

namespace Ouroboros.Agent.Tests.MetaAI;

/// <summary>
/// Unit tests for the DistributedOrchestrator class.
/// </summary>
[Trait("Category", "Unit")]
public class DistributedOrchestratorTests
{
    private readonly Mock<ISafetyGuard> _mockSafety;
    private readonly DistributedOrchestrator _sut;

    public DistributedOrchestratorTests()
    {
        _mockSafety = new Mock<ISafetyGuard>();
        _mockSafety.Setup(s => s.SandboxStep(It.IsAny<PlanStep>()))
            .Returns((PlanStep step) => step);

        _sut = new DistributedOrchestrator(_mockSafety.Object);
    }

    [Fact]
    public void Constructor_NullSafety_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new DistributedOrchestrator(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithValidSafety_DoesNotThrow()
    {
        // Act
        var act = () => new DistributedOrchestrator(_mockSafety.Object);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void RegisterAgent_WithValidAgent_Succeeds()
    {
        // Arrange
        var agent = CreateTestAgent("agent-1");

        // Act
        _sut.RegisterAgent(agent);

        // Assert
        _sut.GetAgentStatus().Should().ContainSingle(a => a.AgentId == "agent-1");
    }

    [Fact]
    public void RegisterAgent_NullAgent_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _sut.RegisterAgent(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RegisterAgent_ExceedsMaxAgents_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = new DistributedOrchestrationConfig(
            MaxAgents: 2,
            HeartbeatTimeout: TimeSpan.FromMinutes(5));
        var sut = new DistributedOrchestrator(_mockSafety.Object, config);

        sut.RegisterAgent(CreateTestAgent("agent-1"));
        sut.RegisterAgent(CreateTestAgent("agent-2"));

        // Act
        var act = () => sut.RegisterAgent(CreateTestAgent("agent-3"));

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Maximum number of agents*");
    }

    [Fact]
    public void UnregisterAgent_ExistingAgent_RemovesAgent()
    {
        // Arrange
        _sut.RegisterAgent(CreateTestAgent("agent-1"));

        // Act
        _sut.UnregisterAgent("agent-1");

        // Assert
        _sut.GetAgentStatus().Should().BeEmpty();
    }

    [Fact]
    public void UnregisterAgent_NonexistentAgent_DoesNotThrow()
    {
        // Act
        var act = () => _sut.UnregisterAgent("nonexistent");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void GetAgentStatus_NoAgents_ReturnsEmptyList()
    {
        // Act
        var result = _sut.GetAgentStatus();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetAgentStatus_WithRegisteredAgents_ReturnsAll()
    {
        // Arrange
        _sut.RegisterAgent(CreateTestAgent("agent-1"));
        _sut.RegisterAgent(CreateTestAgent("agent-2"));

        // Act
        var result = _sut.GetAgentStatus();

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public void UpdateHeartbeat_ExistingAgent_UpdatesTimestamp()
    {
        // Arrange
        var agent = CreateTestAgent("agent-1", lastHeartbeat: DateTime.UtcNow.AddMinutes(-10));
        _sut.RegisterAgent(agent);

        // Act
        _sut.UpdateHeartbeat("agent-1");

        // Assert
        var updated = _sut.GetAgentStatus().First(a => a.AgentId == "agent-1");
        updated.LastHeartbeat.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void UpdateHeartbeat_NonexistentAgent_DoesNotThrow()
    {
        // Act
        var act = () => _sut.UpdateHeartbeat("nonexistent");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task ExecuteDistributedAsync_NullPlan_ReturnsFailure()
    {
        // Act
        var result = await _sut.ExecuteDistributedAsync(null!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("null");
    }

    [Fact]
    public async Task ExecuteDistributedAsync_NoAvailableAgents_ReturnsFailure()
    {
        // Arrange
        var plan = new Plan(
            "test goal",
            new List<PlanStep> { CreateTestStep("step1") },
            new Dictionary<string, double>(),
            DateTime.UtcNow);

        // Act
        var result = await _sut.ExecuteDistributedAsync(plan);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("No agents available");
    }

    [Fact]
    public async Task ExecuteDistributedAsync_WithAvailableAgents_ReturnsSuccess()
    {
        // Arrange
        _sut.RegisterAgent(CreateTestAgent("agent-1"));
        var plan = new Plan(
            "test goal",
            new List<PlanStep> { CreateTestStep("analyze") },
            new Dictionary<string, double>(),
            DateTime.UtcNow);

        // Act
        var result = await _sut.ExecuteDistributedAsync(plan);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.OverallSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteDistributedAsync_CallsSandboxStep()
    {
        // Arrange
        _sut.RegisterAgent(CreateTestAgent("agent-1"));
        var step = CreateTestStep("action1");
        var plan = new Plan(
            "test goal",
            new List<PlanStep> { step },
            new Dictionary<string, double>(),
            DateTime.UtcNow);

        // Act
        await _sut.ExecuteDistributedAsync(plan);

        // Assert
        _mockSafety.Verify(s => s.SandboxStep(It.IsAny<PlanStep>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteDistributedAsync_WithCancellationToken_RespectsToken()
    {
        // Arrange
        _sut.RegisterAgent(CreateTestAgent("agent-1"));
        var plan = new Plan(
            "test goal",
            new List<PlanStep> { CreateTestStep("step1") },
            new Dictionary<string, double>(),
            DateTime.UtcNow);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _sut.ExecuteDistributedAsync(plan, cts.Token));
    }

    private static AgentInfo CreateTestAgent(
        string agentId,
        AgentStatus status = AgentStatus.Available,
        DateTime? lastHeartbeat = null)
    {
        return new AgentInfo(
            agentId,
            $"Test Agent {agentId}",
            new HashSet<string> { "analyze", "generate" },
            status,
            lastHeartbeat ?? DateTime.UtcNow);
    }

    private static PlanStep CreateTestStep(string action)
    {
        return new PlanStep(
            action,
            new Dictionary<string, object> { ["input"] = "test" },
            "Expected outcome",
            0.9);
    }
}
