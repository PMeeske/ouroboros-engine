using FluentAssertions;
using Ouroboros.Pipeline.MultiAgent;

namespace Ouroboros.Tests.MultiAgent;

[Trait("Category", "Unit")]
public sealed class AgentStateTests
{
    private static AgentIdentity CreateTestIdentity() =>
        AgentIdentity.Create("TestAgent", AgentRole.Coder);

    [Fact]
    public void ForAgent_WithValidIdentity_ReturnsIdleState()
    {
        // Arrange
        var identity = CreateTestIdentity();

        // Act
        var state = AgentState.ForAgent(identity);

        // Assert
        state.Identity.Should().Be(identity);
        state.Status.Should().Be(AgentStatus.Idle);
        state.CurrentTaskId.HasValue.Should().BeFalse();
        state.CompletedTasks.Should().Be(0);
        state.FailedTasks.Should().Be(0);
        state.LastActivityAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void ForAgent_WithNullIdentity_ThrowsArgumentNullException()
    {
        Action act = () => AgentState.ForAgent(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("identity");
    }

    [Fact]
    public void IsAvailable_WhenIdle_ReturnsTrue()
    {
        var state = AgentState.ForAgent(CreateTestIdentity());
        state.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public void IsAvailable_WhenBusy_ReturnsFalse()
    {
        var state = AgentState.ForAgent(CreateTestIdentity()).StartTask(Guid.NewGuid());
        state.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public void SuccessRate_WithNoTasks_ReturnsOne()
    {
        var state = AgentState.ForAgent(CreateTestIdentity());
        state.SuccessRate.Should().Be(1.0);
    }

    [Fact]
    public void SuccessRate_WithCompletedAndFailedTasks_ReturnsCorrectRate()
    {
        // Arrange
        var state = AgentState.ForAgent(CreateTestIdentity());
        state = state.StartTask(Guid.NewGuid()).CompleteTask(); // 1 completed
        state = state.StartTask(Guid.NewGuid()).CompleteTask(); // 2 completed
        state = state.StartTask(Guid.NewGuid()).FailTask();     // 1 failed

        // Assert - 2 completed, 1 failed -> 2/3
        state.SuccessRate.Should().BeApproximately(2.0 / 3.0, 0.001);
    }

    [Fact]
    public void WithStatus_UpdatesStatusAndTimestamp()
    {
        // Arrange
        var state = AgentState.ForAgent(CreateTestIdentity());

        // Act
        var updated = state.WithStatus(AgentStatus.Waiting);

        // Assert
        updated.Status.Should().Be(AgentStatus.Waiting);
        updated.LastActivityAt.Should().BeOnOrAfter(state.LastActivityAt);
    }

    [Fact]
    public void StartTask_SetsStatusToBusyAndRecordsTaskId()
    {
        // Arrange
        var state = AgentState.ForAgent(CreateTestIdentity());
        var taskId = Guid.NewGuid();

        // Act
        var updated = state.StartTask(taskId);

        // Assert
        updated.Status.Should().Be(AgentStatus.Busy);
        updated.CurrentTaskId.HasValue.Should().BeTrue();
        updated.CurrentTaskId.Value.Should().Be(taskId);
    }

    [Fact]
    public void CompleteTask_SetsStatusToIdleAndIncrementsCompleted()
    {
        // Arrange
        var state = AgentState.ForAgent(CreateTestIdentity()).StartTask(Guid.NewGuid());

        // Act
        var updated = state.CompleteTask();

        // Assert
        updated.Status.Should().Be(AgentStatus.Idle);
        updated.CurrentTaskId.HasValue.Should().BeFalse();
        updated.CompletedTasks.Should().Be(1);
    }

    [Fact]
    public void FailTask_SetsStatusToErrorAndIncrementsFailed()
    {
        // Arrange
        var state = AgentState.ForAgent(CreateTestIdentity()).StartTask(Guid.NewGuid());

        // Act
        var updated = state.FailTask();

        // Assert
        updated.Status.Should().Be(AgentStatus.Error);
        updated.CurrentTaskId.HasValue.Should().BeFalse();
        updated.FailedTasks.Should().Be(1);
    }

    [Fact]
    public void ImmutabilityPreserved_OriginalStateUnchanged()
    {
        // Arrange
        var original = AgentState.ForAgent(CreateTestIdentity());

        // Act
        var modified = original.StartTask(Guid.NewGuid());

        // Assert
        original.Status.Should().Be(AgentStatus.Idle);
        original.CurrentTaskId.HasValue.Should().BeFalse();
        modified.Status.Should().Be(AgentStatus.Busy);
    }
}
