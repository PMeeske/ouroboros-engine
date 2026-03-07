namespace Ouroboros.Tests.Pipeline.MultiAgent;

using Ouroboros.Pipeline.MultiAgent;
using Ouroboros.Pipeline.Planning;

[Trait("Category", "Unit")]
public class AgentTaskMultiAgentTests
{
    private static Goal CreateGoal(string desc = "test goal") => Goal.Atomic(desc);

    [Fact]
    public void Create_SetsGoalAndPendingStatus()
    {
        var goal = CreateGoal("Complete analysis");
        var task = AgentTask.Create(goal);

        task.Goal.Should().Be(goal);
        task.Status.Should().Be(Ouroboros.Pipeline.MultiAgent.TaskStatus.Pending);
        task.AssignedAgentId.Should().BeNull();
    }

    [Fact]
    public void AssignTo_SetsAgent()
    {
        var agentId = Guid.NewGuid();
        var task = AgentTask.Create(CreateGoal()).AssignTo(agentId);

        task.AssignedAgentId.Should().Be(agentId);
        task.Status.Should().Be(Ouroboros.Pipeline.MultiAgent.TaskStatus.Assigned);
    }

    [Fact]
    public void Start_SetsInProgressStatus()
    {
        var task = AgentTask.Create(CreateGoal())
            .AssignTo(Guid.NewGuid())
            .Start();

        task.Status.Should().Be(Ouroboros.Pipeline.MultiAgent.TaskStatus.InProgress);
        task.StartedAt.Should().NotBeNull();
    }

    [Fact]
    public void Complete_SetsCompletedStatus()
    {
        var task = AgentTask.Create(CreateGoal())
            .AssignTo(Guid.NewGuid())
            .Start()
            .Complete("done");

        task.Status.Should().Be(Ouroboros.Pipeline.MultiAgent.TaskStatus.Completed);
        task.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Fail_SetsFailedStatus()
    {
        var task = AgentTask.Create(CreateGoal())
            .AssignTo(Guid.NewGuid())
            .Start()
            .Fail("error occurred");

        task.Status.Should().Be(Ouroboros.Pipeline.MultiAgent.TaskStatus.Failed);
    }

    [Fact]
    public void Duration_IsComputedFromStartAndComplete()
    {
        var task = AgentTask.Create(CreateGoal())
            .AssignTo(Guid.NewGuid())
            .Start()
            .Complete("done");

        task.Duration.Should().NotBeNull();
    }
}
