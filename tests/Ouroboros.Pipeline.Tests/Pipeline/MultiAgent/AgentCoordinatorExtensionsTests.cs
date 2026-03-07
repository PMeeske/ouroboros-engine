using System.Collections.Immutable;
using FluentAssertions;
using NSubstitute;
using Ouroboros.Pipeline.MultiAgent;
using Ouroboros.Pipeline.Planning;
using Xunit;

namespace Ouroboros.Tests.Pipeline.MultiAgent;

[Trait("Category", "Unit")]
public sealed class AgentCoordinatorExtensionsTests
{
    private static AgentIdentity CreateAgent(string name, AgentRole role = AgentRole.Executor)
        => AgentIdentity.Create(name, role);

    [Fact]
    public void ToAgentTeam_CreatesTeamFromIdentities()
    {
        var identities = new[] { CreateAgent("A"), CreateAgent("B"), CreateAgent("C") };

        var team = identities.ToAgentTeam();

        team.Count.Should().Be(3);
    }

    [Fact]
    public void ToAgentTeam_EmptyList_ReturnsEmptyTeam()
    {
        var identities = Array.Empty<AgentIdentity>();

        var team = identities.ToAgentTeam();

        team.Count.Should().Be(0);
    }

    [Fact]
    public void ToAgentTeam_NullEnumerable_Throws()
    {
        IEnumerable<AgentIdentity>? nullList = null;

        var act = () => nullList!.ToAgentTeam();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetSuccessfulTasks_FiltersCompletedTasks()
    {
        var goal = Goal.Atomic("test");
        var completedTask = AgentTask.Create(goal).Start().Complete("done");
        var failedTask = AgentTask.Create(goal).Start().Fail("error");
        var result = CoordinationResult.Success(
            goal,
            new[] { completedTask, failedTask },
            ImmutableDictionary<Guid, AgentIdentity>.Empty,
            TimeSpan.FromSeconds(1));

        var successful = result.GetSuccessfulTasks();

        successful.Should().HaveCount(1);
        successful[0].Status.Should().Be(Ouroboros.Pipeline.MultiAgent.TaskStatus.Completed);
    }

    [Fact]
    public void GetFailedTasks_FiltersFailedTasks()
    {
        var goal = Goal.Atomic("test");
        var completedTask = AgentTask.Create(goal).Start().Complete("done");
        var failedTask = AgentTask.Create(goal).Start().Fail("error");
        var result = CoordinationResult.Success(
            goal,
            new[] { completedTask, failedTask },
            ImmutableDictionary<Guid, AgentIdentity>.Empty,
            TimeSpan.FromSeconds(1));

        var failed = result.GetFailedTasks();

        failed.Should().HaveCount(1);
        failed[0].Status.Should().Be(Ouroboros.Pipeline.MultiAgent.TaskStatus.Failed);
    }

    [Fact]
    public void GetSuccessfulTasks_NullResult_Throws()
    {
        CoordinationResult? nullResult = null;

        var act = () => nullResult!.GetSuccessfulTasks();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetFailedTasks_NullResult_Throws()
    {
        CoordinationResult? nullResult = null;

        var act = () => nullResult!.GetFailedTasks();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetSuccessfulTasks_AllFailed_ReturnsEmpty()
    {
        var goal = Goal.Atomic("test");
        var failedTask = AgentTask.Create(goal).Start().Fail("error");
        var result = CoordinationResult.Failure(
            goal,
            "all failed",
            new[] { failedTask },
            TimeSpan.FromSeconds(1));

        var successful = result.GetSuccessfulTasks();

        successful.Should().BeEmpty();
    }

    [Fact]
    public void GetFailedTasks_AllSucceeded_ReturnsEmpty()
    {
        var goal = Goal.Atomic("test");
        var completedTask = AgentTask.Create(goal).Start().Complete("done");
        var result = CoordinationResult.Success(
            goal,
            new[] { completedTask },
            ImmutableDictionary<Guid, AgentIdentity>.Empty,
            TimeSpan.FromSeconds(1));

        var failed = result.GetFailedTasks();

        failed.Should().BeEmpty();
    }
}
