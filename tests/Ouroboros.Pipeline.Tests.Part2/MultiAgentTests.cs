namespace Ouroboros.Pipeline.Tests;

using Ouroboros.Pipeline.MultiAgent;
using Ouroboros.Pipeline.Planning;

[Trait("Category", "Unit")]
public class AgentCapabilityTests
{
    #region Create

    [Fact]
    public void Create_NullName_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => AgentCapability.Create(null!, "desc"));
    }

    [Fact]
    public void Create_NullDescription_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => AgentCapability.Create("name", null!));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Create_InvalidProficiency_ShouldThrowArgumentOutOfRangeException(double proficiency)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AgentCapability.Create("name", "desc", proficiency));
    }

    [Fact]
    public void Create_ValidInput_ShouldReturnCapability()
    {
        var cap = AgentCapability.Create("coding", "Writes code", 0.9, "IDE", "Git");
        cap.Name.Should().Be("coding");
        cap.Description.Should().Be("Writes code");
        cap.Proficiency.Should().Be(0.9);
        cap.RequiredTools.Should().Contain("IDE", "Git");
    }

    #endregion
}

[Trait("Category", "Unit")]
public class AgentCoordinatorTests
{
    #region Construction

    [Fact]
    public void Constructor_NullTeam_ShouldThrowArgumentNullException()
    {
        var bus = new Mock<IMessageBus>().Object;
        Assert.Throws<ArgumentNullException>(() => new AgentCoordinator(null!, bus));
    }

    [Fact]
    public void Constructor_NullMessageBus_ShouldThrowArgumentNullException()
    {
        var team = AgentTeam.Empty;
        Assert.Throws<ArgumentNullException>(() => new AgentCoordinator(team, null!));
    }

    [Fact]
    public void Constructor_NullStrategy_ShouldThrowArgumentNullException()
    {
        var team = AgentTeam.Empty;
        var bus = new Mock<IMessageBus>().Object;
        Assert.Throws<ArgumentNullException>(() => new AgentCoordinator(team, bus, null!));
    }

    #endregion

    #region ExecuteAsync

    [Fact]
    public async Task ExecuteAsync_NoAvailableAgents_ShouldReturnFailure()
    {
        var team = AgentTeam.Empty;
        var bus = new Mock<IMessageBus>().Object;
        var coordinator = new AgentCoordinator(team, bus);

        var result = await coordinator.ExecuteAsync(Goal.Atomic("test"));

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_NullGoal_ShouldThrowArgumentNullException()
    {
        var team = AgentTeam.Empty;
        var bus = new Mock<IMessageBus>().Object;
        var coordinator = new AgentCoordinator(team, bus);

        await Assert.ThrowsAsync<ArgumentNullException>(() => coordinator.ExecuteAsync(null!));
    }

    #endregion

    #region ExecuteParallelAsync

    [Fact]
    public async Task ExecuteParallelAsync_NullGoals_ShouldThrowArgumentNullException()
    {
        var team = AgentTeam.Empty;
        var bus = new Mock<IMessageBus>().Object;
        var coordinator = new AgentCoordinator(team, bus);

        await Assert.ThrowsAsync<ArgumentNullException>(() => coordinator.ExecuteParallelAsync(null!));
    }

    [Fact]
    public async Task ExecuteParallelAsync_EmptyGoals_ShouldReturnFailure()
    {
        var team = AgentTeam.Empty;
        var bus = new Mock<IMessageBus>().Object;
        var coordinator = new AgentCoordinator(team, bus);

        var result = await coordinator.ExecuteParallelAsync(Array.Empty<Goal>());

        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region SetDelegationStrategy

    [Fact]
    public void SetDelegationStrategy_Null_ShouldThrowArgumentNullException()
    {
        var team = AgentTeam.Empty;
        var bus = new Mock<IMessageBus>().Object;
        var coordinator = new AgentCoordinator(team, bus);

        Assert.Throws<ArgumentNullException>(() => coordinator.SetDelegationStrategy(null!));
    }

    #endregion
}

[Trait("Category", "Unit")]
public class AgentCoordinatorExtensionsTests
{
    [Fact]
    public void ThenCoordinate_NullGoalStep_ShouldThrowArgumentNullException()
    {
        var coordinator = new Mock<IAgentCoordinator>().Object;
        Assert.Throws<ArgumentNullException>(() => ((Step<string, Goal>)null!).ThenCoordinate(coordinator));
    }

    [Fact]
    public void ThenCoordinate_NullCoordinator_ShouldThrowArgumentNullException()
    {
        var step = new Step<string, Goal>(_ => Task.FromResult(Goal.Atomic("test")));
        Assert.Throws<ArgumentNullException>(() => step.ThenCoordinate(null!));
    }

    [Fact]
    public void ToAgentTeam_ShouldCreateTeam()
    {
        var identities = new[] { AgentIdentity.Create("Agent1", AgentRole.Worker) };
        var team = identities.ToAgentTeam();
        team.GetAvailableAgents().Should().HaveCount(1);
    }

    [Fact]
    public void GetSuccessfulTasks_ShouldFilterCompleted()
    {
        var result = CoordinationResult.Success(
            Goal.Atomic("test"),
            new List<AgentTask> { AgentTask.Create(Goal.Atomic("g1")).Complete("done"), AgentTask.Create(Goal.Atomic("g2")).Fail("err") },
            new Dictionary<Guid, AgentIdentity>(),
            TimeSpan.FromSeconds(1));

        var successful = result.GetSuccessfulTasks();
        successful.Should().HaveCount(1);
    }

    [Fact]
    public void GetFailedTasks_ShouldFilterFailed()
    {
        var result = CoordinationResult.Success(
            Goal.Atomic("test"),
            new List<AgentTask> { AgentTask.Create(Goal.Atomic("g1")).Complete("done"), AgentTask.Create(Goal.Atomic("g2")).Fail("err") },
            new Dictionary<Guid, AgentIdentity>(),
            TimeSpan.FromSeconds(1));

        var failed = result.GetFailedTasks();
        failed.Should().HaveCount(1);
    }
}

[Trait("Category", "Unit")]
public class AgentIdentityTests
{
    #region Create

    [Fact]
    public void Create_NullName_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => AgentIdentity.Create(null!, AgentRole.Worker));
    }

    [Fact]
    public void Create_ValidInput_ShouldInitialize()
    {
        var identity = AgentIdentity.Create("TestAgent", AgentRole.Worker);
        identity.Name.Should().Be("TestAgent");
        identity.Role.Should().Be(AgentRole.Worker);
        identity.Id.Should().NotBe(Guid.Empty);
        identity.Capabilities.Should().BeEmpty();
    }

    #endregion

    #region WithCapability

    [Fact]
    public void WithCapability_Null_ShouldThrowArgumentNullException()
    {
        var identity = AgentIdentity.Create("Test", AgentRole.Worker);
        Assert.Throws<ArgumentNullException>(() => identity.WithCapability(null!));
    }

    [Fact]
    public void WithCapability_Valid_ShouldAddCapability()
    {
        var identity = AgentIdentity.Create("Test", AgentRole.Worker);
        var cap = AgentCapability.Create("coding", "desc");
        var updated = identity.WithCapability(cap);
        updated.Capabilities.Should().ContainSingle();
    }

    #endregion

    #region WithMetadata

    [Fact]
    public void WithMetadata_NullKey_ShouldThrowArgumentNullException()
    {
        var identity = AgentIdentity.Create("Test", AgentRole.Worker);
        Assert.Throws<ArgumentNullException>(() => identity.WithMetadata(null!, "value"));
    }

    [Fact]
    public void WithMetadata_NullValue_ShouldThrowArgumentNullException()
    {
        var identity = AgentIdentity.Create("Test", AgentRole.Worker);
        Assert.Throws<ArgumentNullException>(() => identity.WithMetadata("key", null!));
    }

    [Fact]
    public void WithMetadata_Valid_ShouldAddMetadata()
    {
        var identity = AgentIdentity.Create("Test", AgentRole.Worker);
        var updated = identity.WithMetadata("key", "value");
        updated.Metadata["key"].Should().Be("value");
    }

    #endregion

    #region GetCapability

    [Fact]
    public void GetCapability_Null_ShouldThrowArgumentNullException()
    {
        var identity = AgentIdentity.Create("Test", AgentRole.Worker);
        Assert.Throws<ArgumentNullException>(() => identity.GetCapability(null!));
    }

    [Fact]
    public void GetCapability_Existing_ShouldReturnSome()
    {
        var cap = AgentCapability.Create("coding", "desc");
        var identity = AgentIdentity.Create("Test", AgentRole.Worker).WithCapability(cap);
        var result = identity.GetCapability("coding");
        result.HasValue.Should().BeTrue();
    }

    [Fact]
    public void GetCapability_Missing_ShouldReturnNone()
    {
        var identity = AgentIdentity.Create("Test", AgentRole.Worker);
        var result = identity.GetCapability("coding");
        result.HasValue.Should().BeFalse();
    }

    #endregion

    #region HasCapability

    [Fact]
    public void HasCapability_Null_ShouldThrowArgumentNullException()
    {
        var identity = AgentIdentity.Create("Test", AgentRole.Worker);
        Assert.Throws<ArgumentNullException>(() => identity.HasCapability(null!));
    }

    [Fact]
    public void HasCapability_Existing_ShouldReturnTrue()
    {
        var cap = AgentCapability.Create("coding", "desc");
        var identity = AgentIdentity.Create("Test", AgentRole.Worker).WithCapability(cap);
        identity.HasCapability("coding").Should().BeTrue();
    }

    [Fact]
    public void HasCapability_Missing_ShouldReturnFalse()
    {
        var identity = AgentIdentity.Create("Test", AgentRole.Worker);
        identity.HasCapability("coding").Should().BeFalse();
    }

    #endregion

    #region GetProficiencyFor

    [Fact]
    public void GetProficiencyFor_Null_ShouldThrowArgumentNullException()
    {
        var identity = AgentIdentity.Create("Test", AgentRole.Worker);
        Assert.Throws<ArgumentNullException>(() => identity.GetProficiencyFor(null!));
    }

    [Fact]
    public void GetProficiencyFor_Existing_ShouldReturnProficiency()
    {
        var cap = AgentCapability.Create("coding", "desc", 0.85);
        var identity = AgentIdentity.Create("Test", AgentRole.Worker).WithCapability(cap);
        identity.GetProficiencyFor("coding").Should().Be(0.85);
    }

    [Fact]
    public void GetProficiencyFor_Missing_ShouldReturnZero()
    {
        var identity = AgentIdentity.Create("Test", AgentRole.Worker);
        identity.GetProficiencyFor("coding").Should().Be(0.0);
    }

    #endregion

    #region GetCapabilitiesAbove

    [Fact]
    public void GetCapabilitiesAbove_ShouldFilterByThreshold()
    {
        var cap1 = AgentCapability.Create("high", "desc", 0.9);
        var cap2 = AgentCapability.Create("low", "desc", 0.3);
        var identity = AgentIdentity.Create("Test", AgentRole.Worker).WithCapability(cap1).WithCapability(cap2);
        var above = identity.GetCapabilitiesAbove(0.5);
        above.Should().ContainSingle();
        above[0].Name.Should().Be("high");
    }

    #endregion
}

[Trait("Category", "Unit")]
public class AgentMessageTests
{
    #region Factory Methods

    [Fact]
    public void CreateRequest_NullTopic_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => AgentMessage.CreateRequest(Guid.NewGuid(), Guid.NewGuid(), null!, "payload"));
    }

    [Fact]
    public void CreateRequest_NullPayload_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => AgentMessage.CreateRequest(Guid.NewGuid(), Guid.NewGuid(), "topic", null!));
    }

    [Fact]
    public void CreateRequest_Valid_ShouldCreateRequestMessage()
    {
        var sender = Guid.NewGuid();
        var receiver = Guid.NewGuid();
        var msg = AgentMessage.CreateRequest(sender, receiver, "test.topic", "payload");
        msg.Type.Should().Be(MessageType.Request);
        msg.SenderId.Should().Be(sender);
        msg.ReceiverId.Should().Be(receiver);
        msg.IsRequest.Should().BeTrue();
        msg.IsBroadcast.Should().BeFalse();
        msg.CorrelationId.Should().NotBeNull();
    }

    [Fact]
    public void CreateResponse_NonRequest_ShouldThrowArgumentException()
    {
        var broadcast = AgentMessage.CreateBroadcast(Guid.NewGuid(), "topic", "payload");
        Assert.Throws<ArgumentException>(() => AgentMessage.CreateResponse(broadcast, "response"));
    }

    [Fact]
    public void CreateResponse_NullRequest_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => AgentMessage.CreateResponse(null!, "response"));
    }

    [Fact]
    public void CreateResponse_Valid_ShouldCorrelateToRequest()
    {
        var request = AgentMessage.CreateRequest(Guid.NewGuid(), Guid.NewGuid(), "topic", "payload");
        var response = AgentMessage.CreateResponse(request, "result");
        response.Type.Should().Be(MessageType.Response);
        response.CorrelationId.Should().Be(request.CorrelationId);
        response.SenderId.Should().Be(request.ReceiverId!.Value);
        response.ReceiverId.Should().Be(request.SenderId);
    }

    [Fact]
    public void CreateBroadcast_ShouldCreateBroadcastMessage()
    {
        var msg = AgentMessage.CreateBroadcast(Guid.NewGuid(), "topic", "payload");
        msg.Type.Should().Be(MessageType.Broadcast);
        msg.ReceiverId.Should().BeNull();
        msg.IsBroadcast.Should().BeTrue();
    }

    [Fact]
    public void CreateNotification_ShouldCreateNotificationMessage()
    {
        var msg = AgentMessage.CreateNotification(Guid.NewGuid(), "topic", "payload");
        msg.Type.Should().Be(MessageType.Notification);
    }

    [Fact]
    public void CreateError_ShouldCreateErrorMessage()
    {
        var msg = AgentMessage.CreateError(Guid.NewGuid(), Guid.NewGuid(), "topic", "error msg");
        msg.Type.Should().Be(MessageType.Error);
        msg.Priority.Should().Be(MessagePriority.High);
    }

    #endregion
}

[Trait("Category", "Unit")]
public class AgentRoleTests
{
    [Fact]
    public void Enum_ShouldHaveExpectedValues()
    {
        var values = Enum.GetValues<AgentRole>();
        values.Length.Should().BeGreaterThan(0);
    }
}

[Trait("Category", "Unit")]
public class AgentStateTests
{
    #region ForAgent

    [Fact]
    public void ForAgent_Null_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => AgentState.ForAgent(null!));
    }

    [Fact]
    public void ForAgent_Valid_ShouldCreateIdleState()
    {
        var identity = AgentIdentity.Create("Test", AgentRole.Worker);
        var state = AgentState.ForAgent(identity);
        state.Identity.Should().Be(identity);
        state.Status.Should().Be(AgentStatus.Idle);
        state.IsAvailable.Should().BeTrue();
        state.SuccessRate.Should().Be(1.0);
    }

    #endregion

    #region SuccessRate

    [Fact]
    public void SuccessRate_WithTasks_ShouldCalculateCorrectly()
    {
        var identity = AgentIdentity.Create("Test", AgentRole.Worker);
        var state = new AgentState(identity, AgentStatus.Idle, Option<Guid>.None(), 8, 2, DateTime.UtcNow);
        state.SuccessRate.Should().Be(0.8);
    }

    #endregion

    #region State Transitions

    [Fact]
    public void WithStatus_ShouldUpdateStatus()
    {
        var identity = AgentIdentity.Create("Test", AgentRole.Worker);
        var state = AgentState.ForAgent(identity);
        var updated = state.WithStatus(AgentStatus.Busy);
        updated.Status.Should().Be(AgentStatus.Busy);
        updated.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public void StartTask_ShouldSetBusyAndTaskId()
    {
        var identity = AgentIdentity.Create("Test", AgentRole.Worker);
        var state = AgentState.ForAgent(identity);
        var taskId = Guid.NewGuid();
        var updated = state.StartTask(taskId);
        updated.Status.Should().Be(AgentStatus.Busy);
        updated.CurrentTaskId.HasValue.Should().BeTrue();
        updated.CurrentTaskId.Value.Should().Be(taskId);
    }

    [Fact]
    public void CompleteTask_ShouldIncrementCompleted()
    {
        var identity = AgentIdentity.Create("Test", AgentRole.Worker);
        var state = AgentState.ForAgent(identity).StartTask(Guid.NewGuid());
        var updated = state.CompleteTask();
        updated.Status.Should().Be(AgentStatus.Idle);
        updated.CompletedTasks.Should().Be(1);
        updated.CurrentTaskId.HasValue.Should().BeFalse();
    }

    [Fact]
    public void FailTask_ShouldIncrementFailed()
    {
        var identity = AgentIdentity.Create("Test", AgentRole.Worker);
        var state = AgentState.ForAgent(identity).StartTask(Guid.NewGuid());
        var updated = state.FailTask();
        updated.Status.Should().Be(AgentStatus.Error);
        updated.FailedTasks.Should().Be(1);
    }

    #endregion
}

[Trait("Category", "Unit")]
public class AgentStatusTests
{
    [Fact]
    public void Enum_ShouldHaveExpectedValues()
    {
        var values = Enum.GetValues<AgentStatus>();
        values.Length.Should().BeGreaterThan(0);
    }
}

[Trait("Category", "Unit")]
public class AgentTaskTests
{
    #region Create

    [Fact]
    public void Create_NullGoal_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => AgentTask.Create(null!));
    }

    [Fact]
    public void Create_Valid_ShouldInitializePending()
    {
        var goal = Goal.Atomic("test");
        var task = AgentTask.Create(goal);
        task.Status.Should().Be(TaskStatus.Pending);
        task.Goal.Should().Be(goal);
        task.AssignedAgentId.Should().BeNull();
        task.Duration.Should().BeNull();
    }

    #endregion

    #region Lifecycle

    [Fact]
    public void AssignTo_ShouldSetAgentAndStatus()
    {
        var task = AgentTask.Create(Goal.Atomic("test"));
        var agentId = Guid.NewGuid();
        var assigned = task.AssignTo(agentId);
        assigned.AssignedAgentId.Should().Be(agentId);
        assigned.Status.Should().Be(TaskStatus.Assigned);
    }

    [Fact]
    public void Start_ShouldSetInProgress()
    {
        var task = AgentTask.Create(Goal.Atomic("test")).AssignTo(Guid.NewGuid());
        var started = task.Start();
        started.Status.Should().Be(TaskStatus.InProgress);
        started.StartedAt.Should().NotBeNull();
    }

    [Fact]
    public void Complete_NullResult_ShouldThrowArgumentNullException()
    {
        var task = AgentTask.Create(Goal.Atomic("test")).Start();
        Assert.Throws<ArgumentNullException>(() => task.Complete(null!));
    }

    [Fact]
    public void Complete_Valid_ShouldSetCompleted()
    {
        var task = AgentTask.Create(Goal.Atomic("test")).Start();
        var completed = task.Complete("result");
        completed.Status.Should().Be(TaskStatus.Completed);
        completed.Result.HasValue.Should().BeTrue();
        completed.Duration.Should().NotBeNull();
    }

    [Fact]
    public void Fail_NullError_ShouldThrowArgumentNullException()
    {
        var task = AgentTask.Create(Goal.Atomic("test"));
        Assert.Throws<ArgumentNullException>(() => task.Fail(null!));
    }

    [Fact]
    public void Fail_Valid_ShouldSetFailed()
    {
        var task = AgentTask.Create(Goal.Atomic("test")).Start();
        var failed = task.Fail("error");
        failed.Status.Should().Be(TaskStatus.Failed);
        failed.Error.HasValue.Should().BeTrue();
    }

    #endregion
}

[Trait("Category", "Unit")]
public class AgentTeamTests
{
    #region Empty

    [Fact]
    public void Empty_ShouldHaveNoAgents()
    {
        AgentTeam.Empty.GetAvailableAgents().Should().BeEmpty();
    }

    #endregion

    #region AddAgent

    [Fact]
    public void AddAgent_ShouldAddToTeam()
    {
        var team = AgentTeam.Empty.AddAgent(AgentIdentity.Create("A1", AgentRole.Worker));
        team.GetAvailableAgents().Should().HaveCount(1);
    }

    #endregion

    #region RemoveAgent

    [Fact]
    public void RemoveAgent_ShouldRemoveAgent()
    {
        var agent = AgentIdentity.Create("A1", AgentRole.Worker);
        var team = AgentTeam.Empty.AddAgent(agent).RemoveAgent(agent.Id);
        team.GetAvailableAgents().Should().BeEmpty();
    }

    #endregion

    #region GetAgent

    [Fact]
    public void GetAgent_Existing_ShouldReturnSome()
    {
        var agent = AgentIdentity.Create("A1", AgentRole.Worker);
        var team = AgentTeam.Empty.AddAgent(agent);
        var result = team.GetAgent(agent.Id);
        result.HasValue.Should().BeTrue();
    }

    [Fact]
    public void GetAgent_Missing_ShouldReturnNone()
    {
        var result = AgentTeam.Empty.GetAgent(Guid.NewGuid());
        result.HasValue.Should().BeFalse();
    }

    #endregion

    #region GetAvailableAgents

    [Fact]
    public void GetAvailableAgents_ShouldReturnOnlyIdle()
    {
        var agent1 = AgentIdentity.Create("A1", AgentRole.Worker);
        var agent2 = AgentIdentity.Create("A2", AgentRole.Worker);
        var team = AgentTeam.Empty.AddAgent(agent1).AddAgent(agent2);
        var busyTeam = team.UpdateAgent(agent1.Id, AgentState.ForAgent(agent1).StartTask(Guid.NewGuid()));
        busyTeam.GetAvailableAgents().Should().HaveCount(1);
    }

    #endregion
}

[Trait("Category", "Unit")]
public class AgentVoteTests
{
    [Fact]
    public void Create_ShouldInitializeProperties()
    {
        var agentId = Guid.NewGuid();
        var vote = AgentVote.Create(agentId, "OptionA", 0.9, "reasoning");
        vote.AgentId.Should().Be(agentId);
        vote.Option.Should().Be("OptionA");
        vote.Confidence.Should().Be(0.9);
        vote.Reasoning.Should().Be("reasoning");
    }
}

[Trait("Category", "Unit")]
public class ConsensusResultTests
{
    [Fact]
    public void ParticipationRate_ShouldCalculateCorrectly()
    {
        var result = new ConsensusResult(true, "OptionA", ImmutableList<AgentVote>.Empty, "protocol", 0.8);
        result.ParticipationRate(10).Should().Be(0.0);
    }

    [Fact]
    public void NoConsensus_ShouldCreateFailedResult()
    {
        var result = ConsensusResult.NoConsensus(ImmutableList<AgentVote>.Empty, "protocol");
        result.Reached.Should().BeFalse();
        result.Decision.Should().BeNull();
    }
}

[Trait("Category", "Unit")]
public class CoordinationResultTests
{
    [Fact]
    public void Success_ShouldCreateSuccessResult()
    {
        var result = CoordinationResult.Success(Goal.Atomic("test"), new List<AgentTask>(), new Dictionary<Guid, AgentIdentity>(), TimeSpan.FromSeconds(1));
        result.IsSuccess.Should().BeTrue();
        result.CompletedTaskCount.Should().Be(0);
        result.FailedTaskCount.Should().Be(0);
    }

    [Fact]
    public void Failure_ShouldCreateFailureResult()
    {
        var result = CoordinationResult.Failure(Goal.Atomic("test"), "error", new List<AgentTask>(), TimeSpan.FromSeconds(1));
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("error");
    }
}

[Trait("Category", "Unit")]
public class DelegationCriteriaTests
{
    [Fact]
    public void FromGoal_NullGoal_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => DelegationCriteria.FromGoal(null!));
    }

    [Fact]
    public void WithMinProficiency_ShouldSetMinimum()
    {
        var criteria = DelegationCriteria.FromGoal(Goal.Atomic("test")).WithMinProficiency(0.8);
        criteria.MinProficiency.Should().Be(0.8);
    }

    [Fact]
    public void WithPreferredRole_ShouldSetRole()
    {
        var criteria = DelegationCriteria.FromGoal(Goal.Atomic("test")).WithPreferredRole(AgentRole.Worker);
        criteria.PreferredRole.HasValue.Should().BeTrue();
    }

    [Fact]
    public void RequireCapability_ShouldAddCapability()
    {
        var criteria = DelegationCriteria.FromGoal(Goal.Atomic("test")).RequireCapability("coding");
        criteria.RequiredCapabilities.Should().Contain("coding");
    }
}

[Trait("Category", "Unit")]
public class DelegationResultTests
{
    [Fact]
    public void Success_ShouldCreateMatch()
    {
        var agentId = Guid.NewGuid();
        var result = DelegationResult.Success(agentId, "reason", 0.9);
        result.HasMatch.Should().BeTrue();
        result.SelectedAgentId.Should().Be(agentId);
        result.MatchScore.Should().Be(0.9);
    }

    [Fact]
    public void NoMatch_ShouldCreateNonMatch()
    {
        var result = DelegationResult.NoMatch("no suitable agent");
        result.HasMatch.Should().BeFalse();
        result.SelectedAgentId.Should().BeNull();
    }
}

[Trait("Category", "Unit")]
public class DelegationStrategyFactoryTests
{
    [Fact]
    public void RoundRobin_ShouldReturnStrategy()
    {
        DelegationStrategyFactory.RoundRobin().Should().NotBeNull();
    }

    [Fact]
    public void CapabilityBased_ShouldReturnStrategy()
    {
        DelegationStrategyFactory.CapabilityBased().Should().NotBeNull();
    }

    [Fact]
    public void LoadBalancing_ShouldReturnStrategy()
    {
        DelegationStrategyFactory.LoadBalancing().Should().NotBeNull();
    }

    [Fact]
    public void BestFit_ShouldReturnStrategy()
    {
        DelegationStrategyFactory.BestFit().Should().NotBeNull();
    }

    [Fact]
    public void RoleBased_ShouldReturnStrategy()
    {
        DelegationStrategyFactory.RoleBased().Should().NotBeNull();
    }
}

[Trait("Category", "Unit")]
public class InMemoryMessageBusTests
{
    #region Construction

    [Fact]
    public void Constructor_Default_ShouldInitialize()
    {
        var bus = new InMemoryMessageBus();
        bus.MessageHistory.Should().BeEmpty();
    }

    #endregion

    #region PublishAsync

    [Fact]
    public async Task PublishAsync_ShouldAddToHistory()
    {
        var bus = new InMemoryMessageBus();
        var msg = AgentMessage.CreateBroadcast(Guid.NewGuid(), "topic", "payload");
        await bus.PublishAsync(msg);
        bus.MessageHistory.Should().ContainSingle();
    }

    #endregion

    #region Dispose

    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        var bus = new InMemoryMessageBus();
        var act = () => bus.Dispose();
        act.Should().NotThrow();
    }

    #endregion
}

[Trait("Category", "Unit")]
public class MessagePriorityTests
{
    [Fact]
    public void Enum_ShouldHaveExpectedValues()
    {
        var values = Enum.GetValues<MessagePriority>();
        values.Length.Should().BeGreaterThan(0);
    }
}

[Trait("Category", "Unit")]
public class MessageTypeTests
{
    [Fact]
    public void Enum_ShouldHaveExpectedValues()
    {
        var values = Enum.GetValues<MessageType>();
        values.Length.Should().BeGreaterThan(0);
    }
}

[Trait("Category", "Unit")]
public class TaskStatusTests
{
    [Fact]
    public void Enum_ShouldHaveExpectedValues()
    {
        var values = Enum.GetValues<TaskStatus>();
        values.Length.Should().BeGreaterThan(0);
    }
}

[Trait("Category", "Unit")]
public class ConsensusStrategyTests
{
    [Fact]
    public void Enum_ShouldHaveExpectedValues()
    {
        var values = Enum.GetValues<ConsensusStrategy>();
        values.Length.Should().BeGreaterThan(0);
    }
}

[Trait("Category", "Unit")]
public class SubscriptionTests
{
    [Fact]
    public void Create_ShouldInitializeProperties()
    {
        var sub = new Subscription(Guid.NewGuid(), "topic", msg => { });
        sub.AgentId.Should().NotBe(Guid.Empty);
        sub.Topic.Should().Be("topic");
    }
}

[Trait("Category", "Unit")]
public class ConsensusProtocolTests
{
    [Fact]
    public void Majority_ShouldReturnProtocol()
    {
        ConsensusProtocol.Majority.Should().NotBeNull();
    }

    [Fact]
    public void SuperMajority_ShouldReturnProtocol()
    {
        ConsensusProtocol.SuperMajority.Should().NotBeNull();
    }

    [Fact]
    public void Unanimous_ShouldReturnProtocol()
    {
        ConsensusProtocol.Unanimous.Should().NotBeNull();
    }

    [Fact]
    public void WeightedByConfidence_ShouldReturnProtocol()
    {
        ConsensusProtocol.WeightedByConfidence.Should().NotBeNull();
    }
}

[Trait("Category", "Unit")]
public class BestFitStrategyTests
{
    [Fact]
    public void SelectAgent_EmptyTeam_ShouldReturnNoMatch()
    {
        var strategy = new BestFitStrategy();
        var criteria = DelegationCriteria.FromGoal(Goal.Atomic("test"));
        var result = strategy.SelectAgent(criteria, AgentTeam.Empty);
        result.HasMatch.Should().BeFalse();
    }
}

[Trait("Category", "Unit")]
public class CapabilityBasedStrategyTests
{
    [Fact]
    public void SelectAgent_EmptyTeam_ShouldReturnNoMatch()
    {
        var strategy = new CapabilityBasedStrategy();
        var criteria = DelegationCriteria.FromGoal(Goal.Atomic("test"));
        var result = strategy.SelectAgent(criteria, AgentTeam.Empty);
        result.HasMatch.Should().BeFalse();
    }
}

[Trait("Category", "Unit")]
public class CompositeStrategyTests
{
    [Fact]
    public void Create_ShouldReturnStrategy()
    {
        var strategy = CompositeStrategy.Create((new RoundRobinStrategy(), 0.5), (new BestFitStrategy(), 0.5));
        strategy.Should().NotBeNull();
    }
}

[Trait("Category", "Unit")]
public class LoadBalancingStrategyTests
{
    [Fact]
    public void SelectAgent_EmptyTeam_ShouldReturnNoMatch()
    {
        var strategy = new LoadBalancingStrategy();
        var criteria = DelegationCriteria.FromGoal(Goal.Atomic("test"));
        var result = strategy.SelectAgent(criteria, AgentTeam.Empty);
        result.HasMatch.Should().BeFalse();
    }
}

[Trait("Category", "Unit")]
public class RoleBasedStrategyTests
{
    [Fact]
    public void SelectAgent_EmptyTeam_ShouldReturnNoMatch()
    {
        var strategy = new RoleBasedStrategy();
        var criteria = DelegationCriteria.FromGoal(Goal.Atomic("test"));
        var result = strategy.SelectAgent(criteria, AgentTeam.Empty);
        result.HasMatch.Should().BeFalse();
    }
}

[Trait("Category", "Unit")]
public class RoundRobinStrategyTests
{
    [Fact]
    public void SelectAgent_EmptyTeam_ShouldReturnNoMatch()
    {
        var strategy = new RoundRobinStrategy();
        var criteria = DelegationCriteria.FromGoal(Goal.Atomic("test"));
        var result = strategy.SelectAgent(criteria, AgentTeam.Empty);
        result.HasMatch.Should().BeFalse();
    }
}

[Trait("Category", "Unit")]
public class MessageBusExtensionsTests
{
    [Fact]
    public async Task TryRequestAsync_ShouldReturnResult()
    {
        var bus = new Mock<IMessageBus>();
        var request = AgentMessage.CreateRequest(Guid.NewGuid(), Guid.NewGuid(), "topic", "payload");
        var response = AgentMessage.CreateResponse(request, "result");
        bus.Setup(b => b.RequestAsync(request, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

        var result = await bus.Object.TryRequestAsync(request, TimeSpan.FromSeconds(1));
        result.IsSuccess.Should().BeTrue();
    }
}

[Trait("Category", "Unit")]
public class VotingSessionTests
{
    [Fact]
    public void Constructor_ShouldInitialize()
    {
        var session = new VotingSession(Guid.NewGuid(), "topic", ImmutableList<Guid>.Empty, ConsensusProtocol.Majority);
        session.Topic.Should().Be("topic");
        session.IsOpen.Should().BeTrue();
    }
}
