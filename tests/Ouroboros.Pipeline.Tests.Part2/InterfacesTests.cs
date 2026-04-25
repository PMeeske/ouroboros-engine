namespace Ouroboros.Pipeline.Tests;

using Ouroboros.Pipeline.Metacognition;
using Ouroboros.Pipeline.Middleware;
using Ouroboros.Pipeline.MultiAgent;

[Trait("Category", "Unit")]
public class InterfacesTests
{
    #region ICognitiveMonitor

    [Fact]
    public void ICognitiveMonitor_CanBeMocked()
    {
        var monitor = new Mock<ICognitiveMonitor>();
        monitor.Setup(m => m.RecordEvent(It.IsAny<CognitiveEvent>())).Returns(Result<Unit, string>.Success(Unit.Value));
        monitor.Setup(m => m.GetHealth()).Returns(CognitiveHealth.Optimal());
        monitor.Setup(m => m.GetRecentEvents(It.IsAny<int>())).Returns(ImmutableList<CognitiveEvent>.Empty);
        monitor.Setup(m => m.GetAlerts()).Returns(ImmutableList<MonitoringAlert>.Empty);
        monitor.Setup(m => m.AcknowledgeAlert(It.IsAny<Guid>())).Returns(Result<Unit, string>.Success(Unit.Value));
        monitor.Setup(m => m.SetThreshold(It.IsAny<string>(), It.IsAny<double>())).Returns(Result<Unit, string>.Success(Unit.Value));

        monitor.Object.RecordEvent(CognitiveEvent.Thought("test")).IsSuccess.Should().BeTrue();
        monitor.Object.GetHealth().Should().NotBeNull();
        monitor.Object.GetRecentEvents(5).Should().BeEmpty();
        monitor.Object.GetAlerts().Should().BeEmpty();
        monitor.Object.AcknowledgeAlert(Guid.NewGuid()).IsSuccess.Should().BeTrue();
        monitor.Object.SetThreshold("metric", 0.5).IsSuccess.Should().BeTrue();
    }

    #endregion

    #region IIntrospector

    [Fact]
    public void IIntrospector_CanBeMocked()
    {
        var introspector = new Mock<IIntrospector>();
        introspector.Setup(i => i.CaptureState()).Returns(Result<InternalState, string>.Success(InternalState.Initial()));
        introspector.Setup(i => i.Analyze(It.IsAny<InternalState>())).Returns(Result<IntrospectionReport, string>.Success(IntrospectionReport.Empty(InternalState.Initial())));
        introspector.Setup(i => i.CompareStates(It.IsAny<InternalState>(), It.IsAny<InternalState>())).Returns(Result<StateComparison, string>.Success(StateComparison.Create(InternalState.Initial(), InternalState.Initial())));
        introspector.Setup(i => i.IdentifyPatterns(It.IsAny<IEnumerable<InternalState>>())).Returns(Result<ImmutableList<string>, string>.Success(ImmutableList<string>.Empty));
        introspector.Setup(i => i.SetCurrentFocus(It.IsAny<string>())).Returns(Result<Unit, string>.Success(Unit.Value));
        introspector.Setup(i => i.AddGoal(It.IsAny<string>())).Returns(Result<Unit, string>.Success(Unit.Value));
        introspector.Setup(i => i.RemoveGoal(It.IsAny<string>())).Returns(Result<Unit, string>.Success(Unit.Value));
        introspector.Setup(i => i.SetCognitiveLoad(It.IsAny<double>())).Returns(Result<Unit, string>.Success(Unit.Value));
        introspector.Setup(i => i.SetValence(It.IsAny<double>())).Returns(Result<Unit, string>.Success(Unit.Value));
        introspector.Setup(i => i.SetMode(It.IsAny<ProcessingMode>())).Returns(Result<Unit, string>.Success(Unit.Value));
        introspector.Setup(i => i.GetStateHistory()).Returns(Result<ImmutableList<InternalState>, string>.Success(ImmutableList<InternalState>.Empty));

        introspector.Object.CaptureState().IsSuccess.Should().BeTrue();
        introspector.Object.Analyze(InternalState.Initial()).IsSuccess.Should().BeTrue();
        introspector.Object.CompareStates(InternalState.Initial(), InternalState.Initial()).IsSuccess.Should().BeTrue();
        introspector.Object.IdentifyPatterns(Array.Empty<InternalState>()).IsSuccess.Should().BeTrue();
        introspector.Object.SetCurrentFocus("focus").IsSuccess.Should().BeTrue();
        introspector.Object.AddGoal("goal").IsSuccess.Should().BeTrue();
        introspector.Object.RemoveGoal("goal").IsSuccess.Should().BeTrue();
        introspector.Object.SetCognitiveLoad(0.5).IsSuccess.Should().BeTrue();
        introspector.Object.SetValence(0.5).IsSuccess.Should().BeTrue();
        introspector.Object.SetMode(ProcessingMode.Analytical).IsSuccess.Should().BeTrue();
        introspector.Object.GetStateHistory().IsSuccess.Should().BeTrue();
    }

    #endregion

    #region IReflectiveReasoner

    [Fact]
    public void IReflectiveReasoner_CanBeMocked()
    {
        var reasoner = new Mock<IReflectiveReasoner>();
        reasoner.Setup(r => r.ReflectOn(It.IsAny<ReasoningTrace>())).Returns(ReflectionResult.HighQuality(ReasoningTrace.Start()));
        reasoner.Setup(r => r.GetThinkingStyle()).Returns(ThinkingStyle.Balanced());
        reasoner.Setup(r => r.IdentifyBiases(It.IsAny<IEnumerable<ReasoningTrace>>())).Returns(ImmutableDictionary<string, double>.Empty);
        reasoner.Setup(r => r.SuggestImprovement(It.IsAny<ReasoningTrace>())).Returns(ImmutableList<string>.Empty);
        reasoner.Setup(r => r.GetHistory()).Returns(ImmutableList<ReasoningTrace>.Empty);

        reasoner.Object.ReflectOn(ReasoningTrace.Start()).Should().NotBeNull();
        reasoner.Object.GetThinkingStyle().Should().NotBeNull();
        reasoner.Object.IdentifyBiases(Array.Empty<ReasoningTrace>()).Should().BeEmpty();
        reasoner.Object.SuggestImprovement(ReasoningTrace.Start()).Should().BeEmpty();
        reasoner.Object.GetHistory().Should().BeEmpty();
    }

    #endregion

    #region ISelfAssessor

    [Fact]
    public void ISelfAssessor_CanBeMocked()
    {
        var assessor = new Mock<ISelfAssessor>();
        assessor.Setup(a => a.AssessAsync()).ReturnsAsync(Result<SelfAssessmentResult, string>.Success(SelfAssessmentResult.Empty()));
        assessor.Setup(a => a.AssessDimensionAsync(It.IsAny<PerformanceDimension>())).ReturnsAsync(Result<DimensionScore, string>.Success(DimensionScore.Unknown(PerformanceDimension.Accuracy)));
        assessor.Setup(a => a.GetCapabilityBelief(It.IsAny<string>())).Returns(Option<CapabilityBelief>.None());
        assessor.Setup(a => a.UpdateBelief(It.IsAny<string>(), It.IsAny<double>())).Returns(Result<CapabilityBelief, string>.Success(CapabilityBelief.Uninformative("test")));
        assessor.Setup(a => a.GetAllBeliefs()).Returns(ImmutableDictionary<string, CapabilityBelief>.Empty);
        assessor.Setup(a => a.CalibrateConfidence(It.IsAny<IEnumerable<(double, double)>>())).Returns(Result<Unit, string>.Success(Unit.Value));
        assessor.Setup(a => a.GetCalibrationFactor()).Returns(1.0);
        assessor.Setup(a => a.UpdateDimensionScore(It.IsAny<PerformanceDimension>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string>())).Returns(Result<DimensionScore, string>.Success(DimensionScore.Unknown(PerformanceDimension.Accuracy)));

        assessor.Object.AssessAsync().Result.IsSuccess.Should().BeTrue();
        assessor.Object.AssessDimensionAsync(PerformanceDimension.Accuracy).Result.IsSuccess.Should().BeTrue();
        assessor.Object.GetCapabilityBelief("test").HasValue.Should().BeFalse();
        assessor.Object.UpdateBelief("test", 0.5).IsSuccess.Should().BeTrue();
        assessor.Object.GetAllBeliefs().Should().BeEmpty();
        assessor.Object.CalibrateConfidence(Array.Empty<(double, double)>()).IsSuccess.Should().BeTrue();
        assessor.Object.GetCalibrationFactor().Should().Be(1.0);
    }

    #endregion

    #region IPipelineMiddleware

    [Fact]
    public void IPipelineMiddleware_CanBeMocked()
    {
        var middleware = new Mock<IPipelineMiddleware>();
        middleware.Setup(m => m.InvokeAsync(It.IsAny<PipelineContext>(), It.IsAny<Func<Task<PipelineResult>>>())).ReturnsAsync(PipelineResult.Successful("output"));

        middleware.Object.InvokeAsync(new PipelineContext("input", new Dictionary<string, object>()), () => Task.FromResult(PipelineResult.Successful("next"))).Result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region IDelegationStrategy

    [Fact]
    public void IDelegationStrategy_CanBeMocked()
    {
        var strategy = new Mock<IDelegationStrategy>();
        strategy.Setup(s => s.SelectAgent(It.IsAny<DelegationCriteria>(), It.IsAny<AgentTeam>())).Returns(DelegationResult.NoMatch("test"));

        strategy.Object.SelectAgent(DelegationCriteria.FromGoal(Goal.Atomic("test")), AgentTeam.Empty).HasMatch.Should().BeFalse();
    }

    #endregion

    #region IMessageBus

    [Fact]
    public void IMessageBus_CanBeMocked()
    {
        var bus = new Mock<IMessageBus>();
        bus.Setup(b => b.PublishAsync(It.IsAny<AgentMessage>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        bus.Setup(b => b.RequestAsync(It.IsAny<AgentMessage>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>())).ReturnsAsync(AgentMessage.CreateBroadcast(Guid.NewGuid(), "topic", "payload"));
        bus.Setup(b => b.SubscribeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Func<AgentMessage, Task>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        bus.Setup(b => b.UnsubscribeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        bus.Object.PublishAsync(AgentMessage.CreateBroadcast(Guid.NewGuid(), "topic", "payload")).IsCompleted.Should().BeTrue();
        bus.Object.RequestAsync(AgentMessage.CreateRequest(Guid.NewGuid(), Guid.NewGuid(), "topic", "payload"), TimeSpan.FromSeconds(1)).Result.Should().NotBeNull();
    }

    #endregion

    #region IAgentCoordinator

    [Fact]
    public void IAgentCoordinator_CanBeMocked()
    {
        var coordinator = new Mock<IAgentCoordinator>();
        coordinator.Setup(c => c.ExecuteAsync(It.IsAny<Goal>())).ReturnsAsync(Result<CoordinationResult, string>.Success(CoordinationResult.Success(Goal.Atomic("test"), new List<AgentTask>(), new Dictionary<Guid, AgentIdentity>(), TimeSpan.Zero)));
        coordinator.Setup(c => c.ExecuteParallelAsync(It.IsAny<IEnumerable<Goal>>())).ReturnsAsync(Result<CoordinationResult, string>.Success(CoordinationResult.Success(Goal.Atomic("test"), new List<AgentTask>(), new Dictionary<Guid, AgentIdentity>(), TimeSpan.Zero)));

        coordinator.Object.ExecuteAsync(Goal.Atomic("test")).Result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region IConsensusProtocol

    [Fact]
    public void IConsensusProtocol_CanBeMocked()
    {
        var protocol = new Mock<IConsensusProtocol>();
        protocol.Setup(p => p.ReachConsensusAsync(It.IsAny<VotingSession>(), It.IsAny<CancellationToken>())).ReturnsAsync(ConsensusResult.NoConsensus(ImmutableList<AgentVote>.Empty, "test"));

        protocol.Object.ReachConsensusAsync(new VotingSession(Guid.NewGuid(), "topic", ImmutableList<Guid>.Empty, ConsensusProtocol.Majority)).Result.Should().NotBeNull();
    }

    #endregion
}
