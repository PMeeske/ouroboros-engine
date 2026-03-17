using FluentAssertions;
using Ouroboros.Pipeline.MultiAgent;
using Ouroboros.Pipeline.Planning;

namespace Ouroboros.Tests.MultiAgent;

[Trait("Category", "Unit")]
public sealed class AgentTaskTests
{
    private static Goal CreateTestGoal() => Goal.Atomic("Test goal");

    [Fact]
    public void Create_WithValidGoal_ReturnsTaskInPendingStatus()
    {
        // Arrange
        var goal = CreateTestGoal();

        // Act
        var task = AgentTask.Create(goal);

        // Assert
        task.Id.Should().NotBeEmpty();
        task.Goal.Should().Be(goal);
        task.AssignedAgentId.Should().BeNull();
        task.Status.Should().Be(Ouroboros.Pipeline.MultiAgent.TaskStatus.Pending);
        task.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        task.StartedAt.Should().BeNull();
        task.CompletedAt.Should().BeNull();
        task.Result.HasValue.Should().BeFalse();
        task.Error.HasValue.Should().BeFalse();
    }

    [Fact]
    public void Create_WithNullGoal_ThrowsArgumentNullException()
    {
        Action act = () => AgentTask.Create(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("goal");
    }

    [Fact]
    public void AssignTo_SetsAgentIdAndStatus()
    {
        // Arrange
        var task = AgentTask.Create(CreateTestGoal());
        var agentId = Guid.NewGuid();

        // Act
        var assigned = task.AssignTo(agentId);

        // Assert
        assigned.AssignedAgentId.Should().Be(agentId);
        assigned.Status.Should().Be(Ouroboros.Pipeline.MultiAgent.TaskStatus.Assigned);
    }

    [Fact]
    public void Start_SetsStatusAndTimestamp()
    {
        // Arrange
        var task = AgentTask.Create(CreateTestGoal()).AssignTo(Guid.NewGuid());

        // Act
        var started = task.Start();

        // Assert
        started.Status.Should().Be(Ouroboros.Pipeline.MultiAgent.TaskStatus.InProgress);
        started.StartedAt.Should().NotBeNull();
    }

    [Fact]
    public void Complete_WithValidResult_SetsStatusAndResult()
    {
        // Arrange
        var task = AgentTask.Create(CreateTestGoal()).AssignTo(Guid.NewGuid()).Start();

        // Act
        var completed = task.Complete("Success result");

        // Assert
        completed.Status.Should().Be(Ouroboros.Pipeline.MultiAgent.TaskStatus.Completed);
        completed.CompletedAt.Should().NotBeNull();
        completed.Result.HasValue.Should().BeTrue();
        completed.Result.Value.Should().Be("Success result");
    }

    [Fact]
    public void Complete_WithNullResult_ThrowsArgumentNullException()
    {
        var task = AgentTask.Create(CreateTestGoal()).Start();
        Action act = () => task.Complete(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("result");
    }

    [Fact]
    public void Fail_WithValidError_SetsStatusAndError()
    {
        // Arrange
        var task = AgentTask.Create(CreateTestGoal()).AssignTo(Guid.NewGuid()).Start();

        // Act
        var failed = task.Fail("Something went wrong");

        // Assert
        failed.Status.Should().Be(Ouroboros.Pipeline.MultiAgent.TaskStatus.Failed);
        failed.CompletedAt.Should().NotBeNull();
        failed.Error.HasValue.Should().BeTrue();
        failed.Error.Value.Should().Be("Something went wrong");
    }

    [Fact]
    public void Fail_WithNullError_ThrowsArgumentNullException()
    {
        var task = AgentTask.Create(CreateTestGoal()).Start();
        Action act = () => task.Fail(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("error");
    }

    [Fact]
    public void Duration_WhenCompletedAndStarted_ReturnsDuration()
    {
        // Arrange
        var task = AgentTask.Create(CreateTestGoal()).Start();
        var completed = task.Complete("done");

        // Assert
        completed.Duration.Should().NotBeNull();
        completed.Duration!.Value.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public void Duration_WhenNotCompleted_ReturnsNull()
    {
        // Arrange
        var task = AgentTask.Create(CreateTestGoal()).Start();

        // Assert
        task.Duration.Should().BeNull();
    }

    [Fact]
    public void Duration_WhenNotStarted_ReturnsNull()
    {
        // Arrange
        var task = AgentTask.Create(CreateTestGoal());

        // Assert
        task.Duration.Should().BeNull();
    }
}
