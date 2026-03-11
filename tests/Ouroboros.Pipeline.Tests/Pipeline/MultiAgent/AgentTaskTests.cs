namespace Ouroboros.Tests.Pipeline.MultiAgent;

using Ouroboros.Pipeline.MultiAgent;
using Ouroboros.Pipeline.Planning;

[Trait("Category", "Unit")]
public class AgentTaskTests
{
    private static Goal CreateGoal(string desc = "test goal") => Goal.Atomic(desc);

    [Fact]
    public void Create_ThrowsOnNullGoal()
    {
        // Act
        Action act = () => AgentTask.Create(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_SetsPendingStatus()
    {
        // Arrange
        var goal = CreateGoal();

        // Act
        var task = AgentTask.Create(goal);

        // Assert
        task.Status.Should().Be(Ouroboros.Pipeline.MultiAgent.TaskStatus.Pending);
        task.Goal.Should().Be(goal);
        task.AssignedAgentId.Should().BeNull();
    }

    [Fact]
    public void AssignTo_SetsAssignedStatusAndAgentId()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var task = AgentTask.Create(CreateGoal());

        // Act
        var assigned = task.AssignTo(agentId);

        // Assert
        assigned.Status.Should().Be(Ouroboros.Pipeline.MultiAgent.TaskStatus.Assigned);
        assigned.AssignedAgentId.Should().Be(agentId);
    }

    [Fact]
    public void Start_SetsInProgressStatus()
    {
        // Arrange
        var task = AgentTask.Create(CreateGoal())
            .AssignTo(Guid.NewGuid());

        // Act
        var started = task.Start();

        // Assert
        started.Status.Should().Be(Ouroboros.Pipeline.MultiAgent.TaskStatus.InProgress);
        started.StartedAt.Should().NotBeNull();
    }

    [Fact]
    public void Complete_SetsCompletedStatusAndResult()
    {
        // Arrange
        var task = AgentTask.Create(CreateGoal())
            .AssignTo(Guid.NewGuid())
            .Start();

        // Act
        var completed = task.Complete("analysis done");

        // Assert
        completed.Status.Should().Be(Ouroboros.Pipeline.MultiAgent.TaskStatus.Completed);
        completed.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Complete_ThrowsOnNullResult()
    {
        // Arrange
        var task = AgentTask.Create(CreateGoal())
            .AssignTo(Guid.NewGuid())
            .Start();

        // Act
        Action act = () => task.Complete(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Fail_SetsFailedStatusAndError()
    {
        // Arrange
        var task = AgentTask.Create(CreateGoal())
            .AssignTo(Guid.NewGuid())
            .Start();

        // Act
        var failed = task.Fail("timeout");

        // Assert
        failed.Status.Should().Be(Ouroboros.Pipeline.MultiAgent.TaskStatus.Failed);
        failed.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Fail_ThrowsOnNullError()
    {
        // Arrange
        var task = AgentTask.Create(CreateGoal())
            .AssignTo(Guid.NewGuid())
            .Start();

        // Act
        Action act = () => task.Fail(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Duration_ReturnsNull_WhenNotStarted()
    {
        // Arrange
        var task = AgentTask.Create(CreateGoal());

        // Act & Assert
        task.Duration.Should().BeNull();
    }

    [Fact]
    public void Duration_ReturnsTimeSpan_WhenStartedAndCompleted()
    {
        // Arrange
        var task = AgentTask.Create(CreateGoal())
            .AssignTo(Guid.NewGuid())
            .Start()
            .Complete("done");

        // Act & Assert
        task.Duration.Should().NotBeNull();
    }
}
