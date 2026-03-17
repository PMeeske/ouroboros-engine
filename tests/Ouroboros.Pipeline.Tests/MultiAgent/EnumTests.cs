using FluentAssertions;
using Ouroboros.Pipeline.MultiAgent;

namespace Ouroboros.Tests.MultiAgent;

[Trait("Category", "Unit")]
public sealed class AgentRoleTests
{
    [Theory]
    [InlineData(AgentRole.Analyst, 0)]
    [InlineData(AgentRole.Coder, 1)]
    [InlineData(AgentRole.Reviewer, 2)]
    [InlineData(AgentRole.Planner, 3)]
    [InlineData(AgentRole.Executor, 4)]
    [InlineData(AgentRole.Specialist, 5)]
    public void AgentRole_HasExpectedValue(AgentRole role, int expected)
    {
        ((int)role).Should().Be(expected);
    }

    [Fact]
    public void AgentRole_HasExactlySixValues()
    {
        Enum.GetValues<AgentRole>().Should().HaveCount(6);
    }
}

[Trait("Category", "Unit")]
public sealed class AgentStatusTests
{
    [Theory]
    [InlineData(AgentStatus.Idle, 0)]
    [InlineData(AgentStatus.Busy, 1)]
    [InlineData(AgentStatus.Waiting, 2)]
    [InlineData(AgentStatus.Error, 3)]
    [InlineData(AgentStatus.Offline, 4)]
    public void AgentStatus_HasExpectedValue(AgentStatus status, int expected)
    {
        ((int)status).Should().Be(expected);
    }

    [Fact]
    public void AgentStatus_HasExactlyFiveValues()
    {
        Enum.GetValues<AgentStatus>().Should().HaveCount(5);
    }
}

[Trait("Category", "Unit")]
public sealed class MessagePriorityTests
{
    [Theory]
    [InlineData(MessagePriority.Low, 0)]
    [InlineData(MessagePriority.Normal, 1)]
    [InlineData(MessagePriority.High, 2)]
    [InlineData(MessagePriority.Critical, 3)]
    public void MessagePriority_HasExpectedValue(MessagePriority priority, int expected)
    {
        ((int)priority).Should().Be(expected);
    }

    [Fact]
    public void MessagePriority_HasExactlyFourValues()
    {
        Enum.GetValues<MessagePriority>().Should().HaveCount(4);
    }
}

[Trait("Category", "Unit")]
public sealed class MessageTypeTests
{
    [Theory]
    [InlineData(MessageType.Request, 0)]
    [InlineData(MessageType.Response, 1)]
    [InlineData(MessageType.Broadcast, 2)]
    [InlineData(MessageType.Notification, 3)]
    [InlineData(MessageType.Error, 4)]
    public void MessageType_HasExpectedValue(MessageType type, int expected)
    {
        ((int)type).Should().Be(expected);
    }

    [Fact]
    public void MessageType_HasExactlyFiveValues()
    {
        Enum.GetValues<MessageType>().Should().HaveCount(5);
    }
}

[Trait("Category", "Unit")]
public sealed class TaskStatusTests
{
    [Theory]
    [InlineData(Ouroboros.Pipeline.MultiAgent.TaskStatus.Pending, 0)]
    [InlineData(Ouroboros.Pipeline.MultiAgent.TaskStatus.Assigned, 1)]
    [InlineData(Ouroboros.Pipeline.MultiAgent.TaskStatus.InProgress, 2)]
    [InlineData(Ouroboros.Pipeline.MultiAgent.TaskStatus.Completed, 3)]
    [InlineData(Ouroboros.Pipeline.MultiAgent.TaskStatus.Failed, 4)]
    [InlineData(Ouroboros.Pipeline.MultiAgent.TaskStatus.Cancelled, 5)]
    public void TaskStatus_HasExpectedValue(Ouroboros.Pipeline.MultiAgent.TaskStatus status, int expected)
    {
        ((int)status).Should().Be(expected);
    }

    [Fact]
    public void TaskStatus_HasExactlySixValues()
    {
        Enum.GetValues<Ouroboros.Pipeline.MultiAgent.TaskStatus>().Should().HaveCount(6);
    }
}

[Trait("Category", "Unit")]
public sealed class ConsensusStrategyTests
{
    [Theory]
    [InlineData(ConsensusStrategy.Majority, 0)]
    [InlineData(ConsensusStrategy.SuperMajority, 1)]
    [InlineData(ConsensusStrategy.Unanimous, 2)]
    [InlineData(ConsensusStrategy.WeightedByConfidence, 3)]
    [InlineData(ConsensusStrategy.HighestConfidence, 4)]
    [InlineData(ConsensusStrategy.RankedChoice, 5)]
    public void ConsensusStrategy_HasExpectedValue(ConsensusStrategy strategy, int expected)
    {
        ((int)strategy).Should().Be(expected);
    }

    [Fact]
    public void ConsensusStrategy_HasExactlySixValues()
    {
        Enum.GetValues<ConsensusStrategy>().Should().HaveCount(6);
    }
}
