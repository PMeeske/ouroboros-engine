using FluentAssertions;
using Ouroboros.Pipeline.MultiAgent;
using Ouroboros.Pipeline.Planning;

namespace Ouroboros.Tests.MultiAgent;

[Trait("Category", "Unit")]
public sealed class AgentCoordinatorExtensionsTests
{
    [Fact]
    public void ToAgentTeam_WithIdentities_ReturnsTeam()
    {
        // Arrange
        var identities = new[]
        {
            AgentIdentity.Create("Agent1", AgentRole.Coder),
            AgentIdentity.Create("Agent2", AgentRole.Analyst),
        };

        // Act
        var team = identities.ToAgentTeam();

        // Assert
        team.Count.Should().Be(2);
    }

    [Fact]
    public void ToAgentTeam_WithEmptyIdentities_ReturnsEmptyTeam()
    {
        var team = Array.Empty<AgentIdentity>().ToAgentTeam();
        team.Count.Should().Be(0);
    }

    [Fact]
    public void ToAgentTeam_WithNullIdentities_ThrowsArgumentNullException()
    {
        Action act = () => ((IEnumerable<AgentIdentity>)null!).ToAgentTeam();
        act.Should().Throw<ArgumentNullException>().WithParameterName("identities");
    }

    [Fact]
    public void GetSuccessfulTasks_ReturnsOnlyCompletedTasks()
    {
        // Arrange
        var goal = Goal.Atomic("Test");
        var completed = AgentTask.Create(goal).Start().Complete("done");
        var failed = AgentTask.Create(goal).Start().Fail("error");
        var pending = AgentTask.Create(goal);
        var tasks = new List<AgentTask> { completed, failed, pending };

        var result = CoordinationResult.Success(goal, tasks,
            new Dictionary<Guid, AgentIdentity>(), TimeSpan.Zero);

        // Act
        var successful = result.GetSuccessfulTasks();

        // Assert
        successful.Should().HaveCount(1);
        successful[0].Status.Should().Be(Ouroboros.Pipeline.MultiAgent.TaskStatus.Completed);
    }

    [Fact]
    public void GetFailedTasks_ReturnsOnlyFailedTasks()
    {
        // Arrange
        var goal = Goal.Atomic("Test");
        var completed = AgentTask.Create(goal).Start().Complete("done");
        var failed = AgentTask.Create(goal).Start().Fail("error");
        var tasks = new List<AgentTask> { completed, failed };

        var result = CoordinationResult.Success(goal, tasks,
            new Dictionary<Guid, AgentIdentity>(), TimeSpan.Zero);

        // Act
        var failures = result.GetFailedTasks();

        // Assert
        failures.Should().HaveCount(1);
        failures[0].Status.Should().Be(Ouroboros.Pipeline.MultiAgent.TaskStatus.Failed);
    }

    [Fact]
    public void GetSuccessfulTasks_WithNullResult_ThrowsArgumentNullException()
    {
        Action act = () => ((CoordinationResult)null!).GetSuccessfulTasks();
        act.Should().Throw<ArgumentNullException>().WithParameterName("result");
    }

    [Fact]
    public void GetFailedTasks_WithNullResult_ThrowsArgumentNullException()
    {
        Action act = () => ((CoordinationResult)null!).GetFailedTasks();
        act.Should().Throw<ArgumentNullException>().WithParameterName("result");
    }
}
