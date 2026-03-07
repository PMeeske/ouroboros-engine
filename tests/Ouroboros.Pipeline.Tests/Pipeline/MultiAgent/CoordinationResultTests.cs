namespace Ouroboros.Tests.Pipeline.MultiAgent;

using Ouroboros.Pipeline.MultiAgent;
using Ouroboros.Pipeline.Planning;

[Trait("Category", "Unit")]
public class CoordinationResultTests
{
    [Fact]
    public void Success_SetsProperties()
    {
        var goal = Goal.Atomic("test");
        var tasks = new List<AgentTask>();
        var agents = new Dictionary<Guid, AgentIdentity>();

        var result = CoordinationResult.Success(goal, tasks, agents, TimeSpan.FromSeconds(5));

        result.IsSuccess.Should().BeTrue();
        result.OriginalGoal.Should().Be(goal);
        result.TotalDuration.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Failure_SetsProperties()
    {
        var goal = Goal.Atomic("test");
        var tasks = new List<AgentTask>();

        var result = CoordinationResult.Failure(goal, "timeout", tasks, TimeSpan.FromSeconds(10));

        result.IsSuccess.Should().BeFalse();
        result.Summary.Should().Contain("timeout");
    }

    [Fact]
    public void SuccessRate_ReturnsOneWhenNoTasks()
    {
        var goal = Goal.Atomic("test");
        var result = CoordinationResult.Success(
            goal, new List<AgentTask>(), new Dictionary<Guid, AgentIdentity>(), TimeSpan.Zero);

        result.SuccessRate.Should().Be(1.0);
    }
}
