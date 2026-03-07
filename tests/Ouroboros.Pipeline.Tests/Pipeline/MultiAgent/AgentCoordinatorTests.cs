using System.Collections.Immutable;
using FluentAssertions;
using NSubstitute;
using Ouroboros.Pipeline.MultiAgent;
using Ouroboros.Pipeline.Planning;
using Xunit;

namespace Ouroboros.Tests.Pipeline.MultiAgent;

[Trait("Category", "Unit")]
public sealed class AgentCoordinatorTests : IDisposable
{
    private readonly IMessageBus _messageBus = Substitute.For<IMessageBus>();
    private readonly ITaskExecutor _taskExecutor = Substitute.For<ITaskExecutor>();

    private static AgentIdentity CreateAgent(string name, AgentRole role = AgentRole.Executor)
        => AgentIdentity.Create(name, role);

    private static AgentTeam BuildTeam(params AgentIdentity[] agents)
    {
        var team = AgentTeam.Empty;
        foreach (var a in agents) team = team.AddAgent(a);
        return team;
    }

    private AgentCoordinator CreateSut(AgentTeam? team = null, IDelegationStrategy? strategy = null)
    {
        var t = team ?? BuildTeam(CreateAgent("Agent1"));
        return strategy != null
            ? new AgentCoordinator(t, _messageBus, strategy, _taskExecutor)
            : new AgentCoordinator(t, _messageBus, _taskExecutor);
    }

    [Fact]
    public void Constructor_NullTeam_Throws()
    {
        var act = () => new AgentCoordinator(null!, _messageBus, _taskExecutor);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullMessageBus_Throws()
    {
        var team = BuildTeam(CreateAgent("A"));
        var act = () => new AgentCoordinator(team, null!, _taskExecutor);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullStrategy_Throws()
    {
        var team = BuildTeam(CreateAgent("A"));
        var act = () => new AgentCoordinator(team, _messageBus, null!, _taskExecutor);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Team_ReturnsProvidedTeam()
    {
        var agent = CreateAgent("A");
        var team = BuildTeam(agent);
        using var sut = CreateSut(team);

        sut.Team.Count.Should().Be(1);
    }

    [Fact]
    public void SetDelegationStrategy_NullStrategy_Throws()
    {
        using var sut = CreateSut();

        var act = () => sut.SetDelegationStrategy(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SetDelegationStrategy_ChangesStrategy()
    {
        using var sut = CreateSut();

        // Should not throw
        sut.SetDelegationStrategy(new LoadBalancingStrategy());
    }

    [Fact]
    public async Task ExecuteAsync_NullGoal_Throws()
    {
        using var sut = CreateSut();

        var act = () => sut.ExecuteAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteAsync_NoAvailableAgents_ReturnsFailure()
    {
        using var sut = CreateSut(AgentTeam.Empty);
        var goal = Goal.Atomic("test goal");

        var result = await sut.ExecuteAsync(goal);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("No available agents");
    }

    [Fact]
    public async Task ExecuteAsync_WithAgent_ExecutesTask()
    {
        var agent = CreateAgent("Worker");
        var team = BuildTeam(agent);
        _taskExecutor.ExecuteAsync(Arg.Any<AgentTask>(), Arg.Any<AgentState>(), Arg.Any<CancellationToken>())
            .Returns(new AgentTaskResult(true, "done"));

        using var sut = CreateSut(team);
        var goal = Goal.Atomic("test goal");

        var result = await sut.ExecuteAsync(goal);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_TaskFails_ReportsFailure()
    {
        var agent = CreateAgent("Worker");
        var team = BuildTeam(agent);
        _taskExecutor.ExecuteAsync(Arg.Any<AgentTask>(), Arg.Any<AgentState>(), Arg.Any<CancellationToken>())
            .Returns(new AgentTaskResult(false, "error occurred"));

        using var sut = CreateSut(team);
        var goal = Goal.Atomic("test goal");

        var result = await sut.ExecuteAsync(goal);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_PublishesMessages()
    {
        var agent = CreateAgent("Worker");
        var team = BuildTeam(agent);
        _taskExecutor.ExecuteAsync(Arg.Any<AgentTask>(), Arg.Any<AgentState>(), Arg.Any<CancellationToken>())
            .Returns(new AgentTaskResult(true, "done"));

        using var sut = CreateSut(team);
        var goal = Goal.Atomic("test goal");

        await sut.ExecuteAsync(goal);

        // Should have published at least assignment + result messages
        await _messageBus.Received(2).PublishAsync(Arg.Any<AgentMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteParallelAsync_NullGoals_Throws()
    {
        using var sut = CreateSut();

        var act = () => sut.ExecuteParallelAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteParallelAsync_EmptyGoals_ReturnsFailure()
    {
        using var sut = CreateSut();

        var result = await sut.ExecuteParallelAsync(Array.Empty<Goal>());

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("No goals");
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var sut = CreateSut();

        var act = () => sut.Dispose();

        act.Should().NotThrow();
    }

    [Fact]
    public async Task ExecuteAsync_AfterDispose_Throws()
    {
        var sut = CreateSut();
        sut.Dispose();

        var act = () => sut.ExecuteAsync(Goal.Atomic("test"));

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public void CreateExecutionStep_ReturnsNonNull()
    {
        using var sut = CreateSut();

        var step = sut.CreateExecutionStep();

        step.Should().NotBeNull();
    }

    [Fact]
    public void CreateParallelExecutionStep_ReturnsNonNull()
    {
        using var sut = CreateSut();

        var step = sut.CreateParallelExecutionStep();

        step.Should().NotBeNull();
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}
