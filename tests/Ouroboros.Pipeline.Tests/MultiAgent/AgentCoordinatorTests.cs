using FluentAssertions;
using NSubstitute;
using Ouroboros.Pipeline.MultiAgent;
using Ouroboros.Pipeline.Planning;

namespace Ouroboros.Tests.MultiAgent;

[Trait("Category", "Unit")]
public sealed class AgentCoordinatorTests : IDisposable
{
    private readonly IMessageBus _messageBus;
    private readonly ITaskExecutor _taskExecutor;
    private readonly AgentTeam _team;
    private readonly AgentIdentity _agentIdentity;
    private readonly AgentCoordinator _coordinator;

    public AgentCoordinatorTests()
    {
        _messageBus = Substitute.For<IMessageBus>();
        _messageBus.PublishAsync(Arg.Any<AgentMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _taskExecutor = Substitute.For<ITaskExecutor>();
        _taskExecutor.ExecuteAsync(Arg.Any<AgentTask>(), Arg.Any<AgentState>(), Arg.Any<CancellationToken>())
            .Returns(new AgentTaskResult(Success: true, Output: "Executed successfully"));

        _agentIdentity = AgentIdentity.Create("TestAgent", AgentRole.Coder);
        _team = AgentTeam.Empty.AddAgent(_agentIdentity);
        _coordinator = new AgentCoordinator(_team, _messageBus, _taskExecutor);
    }

    public void Dispose() => _coordinator.Dispose();

    #region Constructor

    [Fact]
    public void Constructor_WithNullTeam_ThrowsArgumentNullException()
    {
        Action act = () => new AgentCoordinator(null!, _messageBus);
        act.Should().Throw<ArgumentNullException>().WithParameterName("team");
    }

    [Fact]
    public void Constructor_WithNullMessageBus_ThrowsArgumentNullException()
    {
        Action act = () => new AgentCoordinator(_team, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("messageBus");
    }

    [Fact]
    public void Constructor_WithNullStrategy_ThrowsArgumentNullException()
    {
        Action act = () => new AgentCoordinator(_team, _messageBus, null!, _taskExecutor);
        act.Should().Throw<ArgumentNullException>().WithParameterName("strategy");
    }

    [Fact]
    public void Constructor_WithoutTaskExecutor_UsesDefault()
    {
        using var coordinator = new AgentCoordinator(_team, _messageBus);
        coordinator.Team.Count.Should().Be(1);
    }

    #endregion

    #region SetDelegationStrategy

    [Fact]
    public void SetDelegationStrategy_WithNullStrategy_ThrowsArgumentNullException()
    {
        Action act = () => _coordinator.SetDelegationStrategy(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("strategy");
    }

    [Fact]
    public void SetDelegationStrategy_WithValidStrategy_ChangesStrategy()
    {
        // Should not throw
        _coordinator.SetDelegationStrategy(DelegationStrategyFactory.BestFit());
    }

    #endregion

    #region ExecuteAsync

    [Fact]
    public async Task ExecuteAsync_WithAtomicGoal_ExecutesSingleTask()
    {
        // Arrange
        var goal = Goal.Atomic("Write code");

        // Act
        var result = await _coordinator.ExecuteAsync(goal);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var coordination = result.Value;
        coordination.IsSuccess.Should().BeTrue();
        coordination.Tasks.Should().HaveCount(1);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullGoal_ThrowsArgumentNullException()
    {
        Func<Task> act = () => _coordinator.ExecuteAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("goal");
    }

    [Fact]
    public async Task ExecuteAsync_WithNoAvailableAgents_ReturnsFailure()
    {
        // Arrange
        var emptyTeam = AgentTeam.Empty;
        using var coordinator = new AgentCoordinator(emptyTeam, _messageBus, _taskExecutor);
        var goal = Goal.Atomic("Test");

        // Act
        var result = await coordinator.ExecuteAsync(goal);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("No available agents");
    }

    [Fact]
    public async Task ExecuteAsync_WithCompositeGoal_ExecutesMultipleTasks()
    {
        // Arrange - add more agents for multiple tasks
        var agent2 = AgentIdentity.Create("Agent2", AgentRole.Analyst);
        var team = _team.AddAgent(agent2);
        using var coordinator = new AgentCoordinator(team, _messageBus, _taskExecutor);

        var subGoal1 = Goal.Atomic("Sub-goal 1");
        var subGoal2 = Goal.Atomic("Sub-goal 2");
        var compositeGoal = Goal.Atomic("Main goal").WithSubGoals(subGoal1, subGoal2);

        // Act
        var result = await coordinator.ExecuteAsync(compositeGoal);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var coordination = result.Value;
        coordination.Tasks.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTaskFails_IncludesFailedTask()
    {
        // Arrange
        _taskExecutor.ExecuteAsync(Arg.Any<AgentTask>(), Arg.Any<AgentState>(), Arg.Any<CancellationToken>())
            .Returns(new AgentTaskResult(Success: false, Output: "Execution failed"));

        var goal = Goal.Atomic("Failing task");

        // Act
        var result = await _coordinator.ExecuteAsync(goal);

        // Assert
        result.IsSuccess.Should().BeTrue(); // Coordination itself succeeds even if tasks fail
        var coordination = result.Value;
        coordination.IsSuccess.Should().BeFalse();
        coordination.Tasks.Should().ContainSingle(t =>
            t.Status == Ouroboros.Pipeline.MultiAgent.TaskStatus.Failed);
    }

    [Fact]
    public async Task ExecuteAsync_PublishesMessages()
    {
        // Arrange
        var goal = Goal.Atomic("Task with messages");

        // Act
        await _coordinator.ExecuteAsync(goal);

        // Assert
        await _messageBus.Received().PublishAsync(
            Arg.Any<AgentMessage>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var goal = Goal.Atomic("Cancelled task");

        // Act & Assert
        Func<Task> act = () => _coordinator.ExecuteAsync(goal, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        _coordinator.Dispose();
        var goal = Goal.Atomic("Test");

        // Act & Assert
        Func<Task> act = () => _coordinator.ExecuteAsync(goal);
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion

    #region ExecuteParallelAsync

    [Fact]
    public async Task ExecuteParallelAsync_WithMultipleGoals_ExecutesAll()
    {
        // Arrange - need multiple agents for parallel execution
        var agent2 = AgentIdentity.Create("Agent2", AgentRole.Analyst);
        var team = _team.AddAgent(agent2);
        using var coordinator = new AgentCoordinator(team, _messageBus, _taskExecutor);

        var goals = new List<Goal>
        {
            Goal.Atomic("Goal 1"),
            Goal.Atomic("Goal 2"),
        };

        // Act
        var result = await coordinator.ExecuteParallelAsync(goals);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteParallelAsync_WithEmptyGoals_ReturnsFailure()
    {
        var result = await _coordinator.ExecuteParallelAsync(new List<Goal>());
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("No goals provided");
    }

    [Fact]
    public async Task ExecuteParallelAsync_WithNullGoals_ThrowsArgumentNullException()
    {
        Func<Task> act = () => _coordinator.ExecuteParallelAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("goals");
    }

    [Fact]
    public async Task ExecuteParallelAsync_WhenDisposed_ThrowsObjectDisposedException()
    {
        _coordinator.Dispose();
        Func<Task> act = () => _coordinator.ExecuteParallelAsync(new List<Goal> { Goal.Atomic("Test") });
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion

    #region Team property

    [Fact]
    public void Team_ReturnsCurrentTeam()
    {
        _coordinator.Team.Count.Should().Be(1);
    }

    #endregion

    #region Dispose

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        _coordinator.Dispose();
        _coordinator.Dispose(); // should not throw
    }

    #endregion
}
