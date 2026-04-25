using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Agent.Tests;

[Trait("Category", "Unit")]
public class InterfaceMockTests
{
    #region IAdaptivePlanner

    [Fact]
    public void IAdaptivePlanner_CanBeMocked()
    {
        var mock = new Mock<IAdaptivePlanner>();
        mock.Setup(p => p.ExecuteWithAdaptationAsync(It.IsAny<Plan>(), It.IsAny<AdaptivePlanningConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PlanExecutionResult, string>.Success(new PlanExecutionResult(new List<StepResult>(), "output", TimeSpan.FromSeconds(1), true, new Dictionary<string, object>())));

        mock.Object.Should().NotBeNull();
    }

    #endregion

    #region ICompositeSkill

    [Fact]
    public void ICompositeSkill_CanBeMocked()
    {
        var mock = new Mock<ICompositeSkill>();
        mock.Setup(s => s.ComponentSkills).Returns(new List<string> { "skill1", "skill2" });

        mock.Object.ComponentSkills.Should().HaveCount(2);
    }

    #endregion

    #region ICostAwareRouter

    [Fact]
    public void ICostAwareRouter_CanBeMocked()
    {
        var mock = new Mock<ICostAwareRouter>();
        mock.Setup(r => r.RouteWithCostAwarenessAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<CostAwareRoutingConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CostBenefitAnalysis, string>.Success(new CostBenefitAnalysis("route", 0.5, 0.9, 1.8, "rationale")));

        mock.Object.Should().NotBeNull();
    }

    #endregion

    #region IDistributedOrchestrator

    [Fact]
    public void IDistributedOrchestrator_CanBeMocked()
    {
        var mock = new Mock<IDistributedOrchestrator>();
        mock.Setup(o => o.GetAgentStatus()).Returns(new List<AgentInfo>());

        mock.Object.GetAgentStatus().Should().BeEmpty();
    }

    #endregion

    #region IEpicBranchOrchestrator

    [Fact]
    public void IEpicBranchOrchestrator_CanBeMocked()
    {
        var mock = new Mock<IEpicBranchOrchestrator>();
        mock.Setup(o => o.GetSubIssueAssignments(It.IsAny<int>())).Returns(new List<SubIssueAssignment>());

        mock.Object.GetSubIssueAssignments(1).Should().BeEmpty();
    }

    #endregion

    #region IExperienceReplay

    [Fact]
    public void IExperienceReplay_CanBeMocked()
    {
        var mock = new Mock<IExperienceReplay>();
        mock.Setup(r => r.TrainOnExperiencesAsync(It.IsAny<ExperienceReplayConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TrainingResult, string>.Success(new TrainingResult(10, new Dictionary<string, double>(), new List<string>(), true)));

        mock.Object.Should().NotBeNull();
    }

    #endregion

    #region IHierarchicalPlanner

    [Fact]
    public void IHierarchicalPlanner_CanBeMocked()
    {
        var mock = new Mock<IHierarchicalPlanner>();
        mock.Setup(p => p.CreateHierarchicalPlanAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<HierarchicalPlanningConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<HierarchicalPlan, string>.Success(new HierarchicalPlan("goal", new Plan("goal", new List<PlanStep>()), new Dictionary<string, Plan>(), 3, DateTime.UtcNow)));

        mock.Object.Should().NotBeNull();
    }

    #endregion

    #region IHumanFeedbackProvider

    [Fact]
    public void IHumanFeedbackProvider_CanBeMocked()
    {
        var mock = new Mock<IHumanFeedbackProvider>();
        mock.Setup(p => p.RequestFeedbackAsync(It.IsAny<HumanFeedbackRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<HumanFeedbackResponse, string>.Success(new HumanFeedbackResponse("req-1", "response", null, DateTime.UtcNow)));

        mock.Object.Should().NotBeNull();
    }

    #endregion

    #region IHumanInTheLoopOrchestrator

    [Fact]
    public void IHumanInTheLoopOrchestrator_CanBeMocked()
    {
        var mock = new Mock<IHumanInTheLoopOrchestrator>();
        mock.Setup(o => o.ExecuteWithHumanOversightAsync(It.IsAny<Plan>(), It.IsAny<HumanInTheLoopConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PlanExecutionResult, string>.Success(new PlanExecutionResult(new List<StepResult>(), "output", TimeSpan.FromSeconds(1), true, new Dictionary<string, object>())));

        mock.Object.Should().NotBeNull();
    }

    #endregion

    #region IMetaAIPlannerOrchestrator

    [Fact]
    public void IMetaAIPlannerOrchestrator_CanBeMocked()
    {
        var mock = new Mock<IMetaAIPlannerOrchestrator>();
        mock.Setup(o => o.PlanAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Plan, string>.Success(new Plan("goal", new List<PlanStep>())));

        mock.Object.Should().NotBeNull();
    }

    #endregion

    #region IOrchestrationCache

    [Fact]
    public void IOrchestrationCache_CanBeMocked()
    {
        var mock = new Mock<IOrchestrationCache>();
        mock.Setup(c => c.GetStatistics()).Returns(new CacheStatistics(0, 100, 0, 0, 0, 0));

        mock.Object.GetStatistics().TotalEntries.Should().Be(0);
    }

    #endregion

    #region IOrchestrationExperiment

    [Fact]
    public void IOrchestrationExperiment_CanBeMocked()
    {
        var mock = new Mock<IOrchestrationExperiment>();
        mock.Setup(e => e.RunExperimentAsync(It.IsAny<string>(), It.IsAny<List<IModelOrchestrator>>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ExperimentResult, string>.Success(new ExperimentResult("exp-1", DateTime.UtcNow, DateTime.UtcNow, new List<VariantResult>(), null, null, ExperimentStatus.Completed)));

        mock.Object.Should().NotBeNull();
    }

    #endregion

    #region IReviewSystemProvider

    [Fact]
    public void IReviewSystemProvider_CanBeMocked()
    {
        var mock = new Mock<IReviewSystemProvider>();
        mock.Setup(p => p.OpenPullRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PullRequest, string>.Success(new PullRequest("pr-1", "Title", "Desc", "spec", new List<string>(), DateTime.UtcNow)));

        mock.Object.Should().NotBeNull();
    }

    #endregion

    #region ISkillComposer

    [Fact]
    public void ISkillComposer_CanBeMocked()
    {
        var mock = new Mock<ISkillComposer>();
        mock.Setup(c => c.ComposeSkillsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<SkillCompositionConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Skill, string>.Success(new Skill("name", "desc", new List<PlanStep>())));

        mock.Object.Should().NotBeNull();
    }

    #endregion

    #region IStakeholderReviewLoop

    [Fact]
    public void IStakeholderReviewLoop_CanBeMocked()
    {
        var mock = new Mock<IStakeholderReviewLoop>();
        mock.Setup(l => l.ExecuteReviewLoopAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<StakeholderReviewConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<StakeholderReviewResult, string>.Success(new StakeholderReviewResult(
                new ReviewState(new PullRequest("pr-1", "Title", "Desc", "spec", new List<string>(), DateTime.UtcNow), new List<ReviewDecision>(), new List<ReviewComment>(), ReviewStatus.Approved, DateTime.UtcNow),
                true, 2, 2, 0, 0, TimeSpan.FromMinutes(10), "Approved")));

        mock.Object.Should().NotBeNull();
    }

    #endregion

    #region IHomeostasisPolicy

    [Fact]
    public void IHomeostasisPolicy_CanBeMocked()
    {
        var mock = new Mock<IHomeostasisPolicy>();
        mock.Setup(p => p.GetRules(It.IsAny<bool>())).Returns(new List<HomeostasisRule>());

        mock.Object.GetRules().Should().BeEmpty();
    }

    #endregion

    #region IPriorityModulator

    [Fact]
    public void IPriorityModulator_CanBeMocked()
    {
        var mock = new Mock<IPriorityModulator>();
        mock.Setup(m => m.GetNextTask()).Returns((PrioritizedTask?)null);

        mock.Object.GetNextTask().Should().BeNull();
    }

    #endregion

    #region IGlobalWorkspace

    [Fact]
    public void IGlobalWorkspace_CanBeMocked()
    {
        var mock = new Mock<IGlobalWorkspace>();
        mock.Setup(w => w.GetItems()).Returns(new List<WorkspaceItem>());

        mock.Object.GetItems().Should().BeEmpty();
    }

    #endregion

    #region IIdentityGraph

    [Fact]
    public void IIdentityGraph_CanBeMocked()
    {
        var mock = new Mock<IIdentityGraph>();
        mock.Setup(g => g.GetStateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentIdentityState(Guid.NewGuid(), "Agent", new List<AgentCapability>(), new List<AgentResource>(), new List<AgentCommitment>(),
                new AgentPerformance(0.9, 100.0, 10, 9, 1, new Dictionary<string, double>(), new Dictionary<string, double>(), DateTime.UtcNow.AddDays(-7), DateTime.UtcNow),
                DateTime.UtcNow, new Dictionary<string, object>()));

        mock.Object.Should().NotBeNull();
    }

    #endregion

    #region IStatePredictor

    [Fact]
    public void IStatePredictor_CanBeMocked()
    {
        var mock = new Mock<IStatePredictor>();
        mock.Setup(p => p.PredictAsync(It.IsAny<State>(), It.IsAny<Action>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new State(new Dictionary<string, object>(), new float[] { 0.1f }));

        mock.Object.Should().NotBeNull();
    }

    #endregion

    #region IRewardPredictor

    [Fact]
    public void IRewardPredictor_CanBeMocked()
    {
        var mock = new Mock<IRewardPredictor>();
        mock.Setup(p => p.PredictRewardAsync(It.IsAny<State>(), It.IsAny<Action>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1.0);

        mock.Object.Should().NotBeNull();
    }

    #endregion

    #region ITerminalPredictor

    [Fact]
    public void ITerminalPredictor_CanBeMocked()
    {
        var mock = new Mock<ITerminalPredictor>();
        mock.Setup(p => p.IsTerminalAsync(It.IsAny<State>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        mock.Object.Should().NotBeNull();
    }

    #endregion

    #region IMetaLearner

    [Fact]
    public void IMetaLearner_CanBeMocked()
    {
        var mock = new Mock<IMetaLearner>();
        mock.Setup(l => l.RecordEpisodeAsync(It.IsAny<LearningEpisode>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        mock.Object.Should().NotBeNull();
    }

    #endregion
}
