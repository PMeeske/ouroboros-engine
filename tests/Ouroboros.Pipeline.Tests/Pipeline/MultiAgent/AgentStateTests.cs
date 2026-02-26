namespace Ouroboros.Tests.Pipeline.MultiAgent;

using Ouroboros.Pipeline.MultiAgent;

[Trait("Category", "Unit")]
public class AgentStateTests
{
    private AgentIdentity CreateIdentity() =>
        AgentIdentity.Create("TestAgent", AgentRole.Coder);

    [Fact]
    public void ForAgent_CreatesIdleState()
    {
        var state = AgentState.ForAgent(CreateIdentity());

        state.Status.Should().Be(AgentStatus.Idle);
        state.CompletedTasks.Should().Be(0);
        state.FailedTasks.Should().Be(0);
        state.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public void StartTask_SetsBusyStatus()
    {
        var state = AgentState.ForAgent(CreateIdentity())
            .StartTask(Guid.NewGuid());

        state.Status.Should().Be(AgentStatus.Busy);
        state.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public void CompleteTask_IncrementsCompletedAndReturnsToIdle()
    {
        var state = AgentState.ForAgent(CreateIdentity())
            .StartTask(Guid.NewGuid())
            .CompleteTask();

        state.Status.Should().Be(AgentStatus.Idle);
        state.CompletedTasks.Should().Be(1);
    }

    [Fact]
    public void FailTask_IncrementsFailedAndSetsError()
    {
        var state = AgentState.ForAgent(CreateIdentity())
            .StartTask(Guid.NewGuid())
            .FailTask();

        state.Status.Should().Be(AgentStatus.Error);
        state.FailedTasks.Should().Be(1);
    }

    [Fact]
    public void SuccessRate_ComputesCorrectly()
    {
        var state = AgentState.ForAgent(CreateIdentity())
            .StartTask(Guid.NewGuid()).CompleteTask()
            .StartTask(Guid.NewGuid()).CompleteTask()
            .StartTask(Guid.NewGuid()).FailTask();

        state.SuccessRate.Should().BeApproximately(2.0 / 3.0, 0.001);
    }

    [Fact]
    public void SuccessRate_ReturnsOneWhenNoTasks()
    {
        var state = AgentState.ForAgent(CreateIdentity());
        state.SuccessRate.Should().Be(1.0);
    }
}
