using FluentAssertions;
using Ouroboros.Pipeline.MultiAgent;
using Ouroboros.Pipeline.Planning;

namespace Ouroboros.Tests.MultiAgent;

[Trait("Category", "Unit")]
public sealed class CoordinationResultTests
{
    private static Goal CreateTestGoal() => Goal.Atomic("Test goal");

    [Fact]
    public void Success_ReturnsSuccessResult()
    {
        // Arrange
        var goal = CreateTestGoal();
        var task = AgentTask.Create(goal).AssignTo(Guid.NewGuid()).Start().Complete("done");
        var tasks = new List<AgentTask> { task };
        var agentId = Guid.NewGuid();
        var agents = new Dictionary<Guid, AgentIdentity>
        {
            { agentId, AgentIdentity.Create("Agent", AgentRole.Coder) }
        };
        var duration = TimeSpan.FromSeconds(5);

        // Act
        var result = CoordinationResult.Success(goal, tasks, agents, duration);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.OriginalGoal.Should().Be(goal);
        result.Tasks.Should().HaveCount(1);
        result.ParticipatingAgents.Should().HaveCount(1);
        result.TotalDuration.Should().Be(duration);
        result.Summary.Should().Contain("successfully");
    }

    [Fact]
    public void Failure_ReturnsFailureResult()
    {
        // Arrange
        var goal = CreateTestGoal();
        var tasks = new List<AgentTask>();
        var duration = TimeSpan.FromSeconds(2);

        // Act
        var result = CoordinationResult.Failure(goal, "No agents available", tasks, duration);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Summary.Should().Contain("No agents available");
        result.ParticipatingAgents.Should().BeEmpty();
    }

    [Fact]
    public void Success_WithNullGoal_ThrowsArgumentNullException()
    {
        Action act = () => CoordinationResult.Success(null!, new List<AgentTask>(),
            new Dictionary<Guid, AgentIdentity>(), TimeSpan.Zero);
        act.Should().Throw<ArgumentNullException>().WithParameterName("goal");
    }

    [Fact]
    public void Failure_WithNullGoal_ThrowsArgumentNullException()
    {
        Action act = () => CoordinationResult.Failure(null!, "reason", new List<AgentTask>(), TimeSpan.Zero);
        act.Should().Throw<ArgumentNullException>().WithParameterName("goal");
    }

    [Fact]
    public void Failure_WithNullReason_ThrowsArgumentNullException()
    {
        Action act = () => CoordinationResult.Failure(CreateTestGoal(), null!, new List<AgentTask>(), TimeSpan.Zero);
        act.Should().Throw<ArgumentNullException>().WithParameterName("reason");
    }

    [Fact]
    public void CompletedTaskCount_ReturnsCorrectCount()
    {
        // Arrange
        var goal = CreateTestGoal();
        var completed = AgentTask.Create(goal).Start().Complete("done");
        var failed = AgentTask.Create(goal).Start().Fail("error");
        var tasks = new List<AgentTask> { completed, failed };
        var result = CoordinationResult.Success(goal, tasks,
            new Dictionary<Guid, AgentIdentity>(), TimeSpan.Zero);

        // Assert
        result.CompletedTaskCount.Should().Be(1);
    }

    [Fact]
    public void FailedTaskCount_ReturnsCorrectCount()
    {
        // Arrange
        var goal = CreateTestGoal();
        var completed = AgentTask.Create(goal).Start().Complete("done");
        var failed = AgentTask.Create(goal).Start().Fail("error");
        var tasks = new List<AgentTask> { completed, failed };
        var result = CoordinationResult.Success(goal, tasks,
            new Dictionary<Guid, AgentIdentity>(), TimeSpan.Zero);

        // Assert
        result.FailedTaskCount.Should().Be(1);
    }

    [Fact]
    public void SuccessRate_WithMixedTasks_ReturnsCorrectRate()
    {
        // Arrange
        var goal = CreateTestGoal();
        var completed1 = AgentTask.Create(goal).Start().Complete("done1");
        var completed2 = AgentTask.Create(goal).Start().Complete("done2");
        var failed = AgentTask.Create(goal).Start().Fail("error");
        var tasks = new List<AgentTask> { completed1, completed2, failed };
        var result = CoordinationResult.Success(goal, tasks,
            new Dictionary<Guid, AgentIdentity>(), TimeSpan.Zero);

        // Assert - 2 completed, 1 failed -> 2/3
        result.SuccessRate.Should().BeApproximately(2.0 / 3.0, 0.001);
    }

    [Fact]
    public void SuccessRate_WithNoTasks_ReturnsOne()
    {
        // Arrange
        var result = CoordinationResult.Success(CreateTestGoal(), new List<AgentTask>(),
            new Dictionary<Guid, AgentIdentity>(), TimeSpan.Zero);

        // Assert
        result.SuccessRate.Should().Be(1.0);
    }
}
