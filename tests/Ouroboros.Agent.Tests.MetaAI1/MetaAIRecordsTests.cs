using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Agent.Tests;

[Trait("Category", "Unit")]
public class MetaAIRecordsTests
{
    #region AbstractTask

    [Fact]
    public void AbstractTask_Creation_WithValidData_ShouldSetProperties()
    {
        // Arrange
        var preconditions = new List<string> { "pre1", "pre2" };
        var decompositions = new List<TaskDecomposition>();

        // Act
        var task = new AbstractTask("TestTask", preconditions, decompositions);

        // Assert
        task.Name.Should().Be("TestTask");
        task.Preconditions.Should().BeEquivalentTo(preconditions);
        task.PossibleDecompositions.Should().BeEquivalentTo(decompositions);
    }

    [Fact]
    public void AbstractTask_Equality_SameValues_ShouldBeEqual()
    {
        var preconditions = new List<string> { "pre1" };
        var decompositions = new List<TaskDecomposition>();
        var a = new AbstractTask("Task", preconditions, decompositions);
        var b = new AbstractTask("Task", new List<string> { "pre1" }, new List<TaskDecomposition>());

        a.Should().Be(b);
    }

    #endregion

    #region AdaptationAction

    [Fact]
    public void AdaptationAction_Creation_WithValidData_ShouldSetProperties()
    {
        var action = new AdaptationAction(AdaptationStrategy.Retry, "Network timeout");

        action.Strategy.Should().Be(AdaptationStrategy.Retry);
        action.Reason.Should().Be("Network timeout");
        action.RevisedPlan.Should().BeNull();
        action.ReplacementStep.Should().BeNull();
    }

    [Fact]
    public void AdaptationAction_Creation_WithOptionalParameters_ShouldSetProperties()
    {
        var plan = new Plan("goal", new List<PlanStep>());
        var step = new PlanStep("action", "expected", new Dictionary<string, object>());
        var action = new AdaptationAction(AdaptationStrategy.ReplaceStep, "Step failed", plan, step);

        action.RevisedPlan.Should().Be(plan);
        action.ReplacementStep.Should().Be(step);
    }

    #endregion

    #region AdaptationStrategy

    [Theory]
    [InlineData(AdaptationStrategy.Retry)]
    [InlineData(AdaptationStrategy.ReplaceStep)]
    [InlineData(AdaptationStrategy.AddStep)]
    [InlineData(AdaptationStrategy.Replan)]
    [InlineData(AdaptationStrategy.Abort)]
    public void AdaptationStrategy_AllValues_ShouldBeDefined(AdaptationStrategy strategy)
    {
        ((int)strategy).Should().BeGreaterOrEqualTo(0);
    }

    #endregion

    #region AdaptationTrigger

    [Fact]
    public void AdaptationTrigger_Creation_ShouldSetProperties()
    {
        Func<ExecutionContext, bool> condition = ctx => true;
        var trigger = new AdaptationTrigger("Timeout", condition, AdaptationStrategy.Retry);

        trigger.Name.Should().Be("Timeout");
        trigger.Condition.Should().BeSameAs(condition);
        trigger.Strategy.Should().Be(AdaptationStrategy.Retry);
    }

    #endregion

    #region AgentInfo

    [Fact]
    public void AgentInfo_Creation_ShouldSetProperties()
    {
        var capabilities = new HashSet<string> { "code", "review" };
        var heartbeat = DateTime.UtcNow;
        var agent = new AgentInfo("agent-1", "TestAgent", capabilities, AgentStatus.Available, heartbeat);

        agent.AgentId.Should().Be("agent-1");
        agent.Name.Should().Be("TestAgent");
        agent.Capabilities.Should().BeEquivalentTo(capabilities);
        agent.Status.Should().Be(AgentStatus.Available);
        agent.LastHeartbeat.Should().Be(heartbeat);
    }

    #endregion

    #region AgentStatus

    [Theory]
    [InlineData(AgentStatus.Available)]
    [InlineData(AgentStatus.Busy)]
    [InlineData(AgentStatus.Offline)]
    public void AgentStatus_AllValues_ShouldBeDefined(AgentStatus status)
    {
        ((int)status).Should().BeGreaterOrEqualTo(0);
    }

    #endregion

    #region ApprovalRequest

    [Fact]
    public void ApprovalRequest_Creation_ShouldSetProperties()
    {
        var parameters = new Dictionary<string, object> { ["key"] = "value" };
        var requestedAt = DateTime.UtcNow;
        var request = new ApprovalRequest("req-1", "Deploy", parameters, "Critical deployment", requestedAt);

        request.RequestId.Should().Be("req-1");
        request.Action.Should().Be("Deploy");
        request.Parameters.Should().BeEquivalentTo(parameters);
        request.Rationale.Should().Be("Critical deployment");
        request.RequestedAt.Should().Be(requestedAt);
    }

    #endregion

    #region ApprovalResponse

    [Fact]
    public void ApprovalResponse_Approved_ShouldSetProperties()
    {
        var mods = new Dictionary<string, object> { ["param"] = "val" };
        var respondedAt = DateTime.UtcNow;
        var response = new ApprovalResponse("req-1", true, null, mods, respondedAt);

        response.RequestId.Should().Be("req-1");
        response.Approved.Should().BeTrue();
        response.Reason.Should().BeNull();
        response.Modifications.Should().BeEquivalentTo(mods);
        response.RespondedAt.Should().Be(respondedAt);
    }

    [Fact]
    public void ApprovalResponse_Rejected_ShouldSetProperties()
    {
        var respondedAt = DateTime.UtcNow;
        var response = new ApprovalResponse("req-1", false, "Not authorized", null, respondedAt);

        response.Approved.Should().BeFalse();
        response.Reason.Should().Be("Not authorized");
        response.Modifications.Should().BeNull();
    }

    #endregion

    #region CacheStatistics

    [Fact]
    public void CacheStatistics_Creation_ShouldSetProperties()
    {
        var stats = new CacheStatistics(50, 100, 80, 20, 0.8, 50000);

        stats.TotalEntries.Should().Be(50);
        stats.MaxEntries.Should().Be(100);
        stats.HitCount.Should().Be(80);
        stats.MissCount.Should().Be(20);
        stats.HitRate.Should().Be(0.8);
        stats.MemoryEstimateBytes.Should().Be(50000);
    }

    [Fact]
    public void CacheStatistics_UtilizationPercent_ShouldCalculateCorrectly()
    {
        var stats = new CacheStatistics(50, 100, 0, 0, 0, 0);
        stats.UtilizationPercent.Should().Be(50.0);
    }

    [Fact]
    public void CacheStatistics_UtilizationPercent_ZeroMax_ShouldReturnZero()
    {
        var stats = new CacheStatistics(0, 0, 0, 0, 0, 0);
        stats.UtilizationPercent.Should().Be(0.0);
    }

    [Fact]
    public void CacheStatistics_IsHealthy_HighHitRate_ShouldBeTrue()
    {
        var stats = new CacheStatistics(0, 100, 60, 40, 0.6, 0);
        stats.IsHealthy.Should().BeTrue();
    }

    [Fact]
    public void CacheStatistics_IsHealthy_WarmingUp_ShouldBeTrue()
    {
        var stats = new CacheStatistics(0, 100, 10, 20, 0.33, 0);
        stats.IsHealthy.Should().BeTrue();
    }

    [Fact]
    public void CacheStatistics_IsHealthy_Unhealthy_ShouldBeFalse()
    {
        var stats = new CacheStatistics(0, 100, 100, 200, 0.33, 0);
        stats.IsHealthy.Should().BeFalse();
    }

    #endregion

    #region ConcretePlan

    [Fact]
    public void ConcretePlan_Creation_ShouldSetProperties()
    {
        var steps = new List<string> { "step1", "step2" };
        var plan = new ConcretePlan("AbstractTask", steps);

        plan.AbstractTaskName.Should().Be("AbstractTask");
        plan.ConcreteSteps.Should().BeEquivalentTo(steps);
    }

    #endregion

    #region CostInfo

    [Fact]
    public void CostInfo_Creation_ShouldSetProperties()
    {
        var info = new CostInfo("gpt-4", 0.03, 0.01, 0.95);

        info.ResourceId.Should().Be("gpt-4");
        info.CostPerToken.Should().Be(0.03);
        info.CostPerRequest.Should().Be(0.01);
        info.EstimatedQuality.Should().Be(0.95);
    }

    #endregion

    #region CostAwareRoutingConfig

    [Fact]
    public void CostAwareRoutingConfig_DefaultCreation_ShouldUseDefaults()
    {
        var config = new CostAwareRoutingConfig();

        config.MaxCostPerPlan.Should().Be(1.0);
        config.MinAcceptableQuality.Should().Be(0.7);
        config.Strategy.Should().Be(CostOptimizationStrategy.Balanced);
    }

    [Fact]
    public void CostAwareRoutingConfig_CustomCreation_ShouldSetValues()
    {
        var config = new CostAwareRoutingConfig(2.0, 0.9, CostOptimizationStrategy.MaximizeQuality);

        config.MaxCostPerPlan.Should().Be(2.0);
        config.MinAcceptableQuality.Should().Be(0.9);
        config.Strategy.Should().Be(CostOptimizationStrategy.MaximizeQuality);
    }

    #endregion

    #region CostBenefitAnalysis

    [Fact]
    public void CostBenefitAnalysis_Creation_ShouldSetProperties()
    {
        var analysis = new CostBenefitAnalysis("route-a", 0.5, 0.9, 1.8, "Best value");

        analysis.RecommendedRoute.Should().Be("route-a");
        analysis.EstimatedCost.Should().Be(0.5);
        analysis.EstimatedQuality.Should().Be(0.9);
        analysis.ValueScore.Should().Be(1.8);
        analysis.Rationale.Should().Be("Best value");
    }

    #endregion

    #region CostOptimizationStrategy

    [Theory]
    [InlineData(CostOptimizationStrategy.MinimizeCost)]
    [InlineData(CostOptimizationStrategy.MaximizeQuality)]
    [InlineData(CostOptimizationStrategy.Balanced)]
    [InlineData(CostOptimizationStrategy.MaximizeValue)]
    public void CostOptimizationStrategy_AllValues_ShouldBeDefined(CostOptimizationStrategy strategy)
    {
        ((int)strategy).Should().BeGreaterOrEqualTo(0);
    }

    #endregion

    #region DistributedOrchestrationConfig

    [Fact]
    public void DistributedOrchestrationConfig_DefaultCreation_ShouldUseDefaults()
    {
        var config = new DistributedOrchestrationConfig();

        config.MaxAgents.Should().Be(10);
        config.EnableLoadBalancing.Should().BeTrue();
    }

    [Fact]
    public void DistributedOrchestrationConfig_CustomCreation_ShouldSetValues()
    {
        var config = new DistributedOrchestrationConfig(5, TimeSpan.FromMinutes(2), false);

        config.MaxAgents.Should().Be(5);
        config.HeartbeatTimeout.Should().Be(TimeSpan.FromMinutes(2));
        config.EnableLoadBalancing.Should().BeFalse();
    }

    #endregion

    #region DynamicSkillToken

    [Fact]
    public void DynamicSkillToken_Constructor_NullSkill_ShouldThrow()
    {
        var mockModel = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        Action act = () => new DynamicSkillToken(null!, mockModel.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("skill");
    }

    [Fact]
    public void DynamicSkillToken_Constructor_NullModel_ShouldThrow()
    {
        var skill = new Skill("test", "desc", new List<PlanStep>());
        Action act = () => new DynamicSkillToken(skill, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("model");
    }

    [Fact]
    public void DynamicSkillToken_Constructor_ValidArgs_ShouldSetSkill()
    {
        var mockModel = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        var skill = new Skill("test", "desc", new List<PlanStep>());
        var token = new DynamicSkillToken(skill, mockModel.Object);

        token.Skill.Should().BeSameAs(skill);
    }

    #endregion

    #region Epic

    [Fact]
    public void Epic_Creation_ShouldSetProperties()
    {
        var createdAt = DateTime.UtcNow;
        var epic = new Epic(42, "Feature X", "Description", new List<int> { 1, 2 }, createdAt);

        epic.EpicNumber.Should().Be(42);
        epic.Title.Should().Be("Feature X");
        epic.Description.Should().Be("Description");
        epic.SubIssueNumbers.Should().BeEquivalentTo(new List<int> { 1, 2 });
        epic.CreatedAt.Should().Be(createdAt);
    }

    #endregion

    #region EpicBranchConfig

    [Fact]
    public void EpicBranchConfig_DefaultCreation_ShouldUseDefaults()
    {
        var config = new EpicBranchConfig();

        config.BranchPrefix.Should().Be("epic");
        config.AgentPoolPrefix.Should().Be("sub-issue-agent");
        config.AutoCreateBranches.Should().BeTrue();
        config.AutoAssignAgents.Should().BeTrue();
        config.MaxConcurrentSubIssues.Should().Be(5);
    }

    #endregion

    #region EvaluationMetrics

    [Fact]
    public void EvaluationMetrics_Creation_ShouldSetProperties()
    {
        var custom = new Dictionary<string, double> { ["accuracy"] = 0.95 };
        var metrics = new EvaluationMetrics("test1", true, 0.8, TimeSpan.FromSeconds(1), 5, 0.9, custom);

        metrics.TestCase.Should().Be("test1");
        metrics.Success.Should().BeTrue();
        metrics.QualityScore.Should().Be(0.8);
        metrics.ExecutionTime.Should().Be(TimeSpan.FromSeconds(1));
        metrics.PlanSteps.Should().Be(5);
        metrics.ConfidenceScore.Should().Be(0.9);
        metrics.CustomMetrics.Should().BeEquivalentTo(custom);
    }

    #endregion

    #region EvaluationResults

    [Fact]
    public void EvaluationResults_Creation_ShouldSetProperties()
    {
        var testResults = new List<EvaluationMetrics>();
        var aggregated = new Dictionary<string, double> { ["avg"] = 0.8 };
        var results = new EvaluationResults(10, 8, 2, 0.85, 0.9, TimeSpan.FromSeconds(2), testResults, aggregated);

        results.TotalTests.Should().Be(10);
        results.SuccessfulTests.Should().Be(8);
        results.FailedTests.Should().Be(2);
        results.AverageQualityScore.Should().Be(0.85);
        results.AverageConfidence.Should().Be(0.9);
        results.AverageExecutionTime.Should().Be(TimeSpan.FromSeconds(2));
        results.TestResults.Should().BeEquivalentTo(testResults);
        results.AggregatedMetrics.Should().BeEquivalentTo(aggregated);
    }

    #endregion

    #region ExecutedStep

    [Fact]
    public void ExecutedStep_Creation_ShouldSetProperties()
    {
        var outputs = new Dictionary<string, object> { ["result"] = "ok" };
        var step = new ExecutedStep("step1", true, TimeSpan.FromSeconds(1), outputs);

        step.StepName.Should().Be("step1");
        step.Success.Should().BeTrue();
        step.Duration.Should().Be(TimeSpan.FromSeconds(1));
        step.Outputs.Should().BeEquivalentTo(outputs);
    }

    #endregion

    #region ExecutionContext

    [Fact]
    public void ExecutionContext_Creation_ShouldSetProperties()
    {
        var plan = new Plan("goal", new List<PlanStep>());
        var completed = new List<StepResult>();
        var currentStep = new PlanStep("action", "expected", new Dictionary<string, object>());
        var metadata = new Dictionary<string, object> { ["key"] = "val" };
        var ctx = new ExecutionContext(plan, completed, currentStep, 0, metadata);

        ctx.OriginalPlan.Should().Be(plan);
        ctx.CompletedSteps.Should().BeEquivalentTo(completed);
        ctx.CurrentStep.Should().Be(currentStep);
        ctx.CurrentStepIndex.Should().Be(0);
        ctx.Metadata.Should().BeEquivalentTo(metadata);
    }

    #endregion

    #region ExecutionTrace

    [Fact]
    public void ExecutionTrace_Creation_ShouldSetProperties()
    {
        var steps = new List<ExecutedStep>();
        var trace = new ExecutionTrace(steps, -1, "none");

        trace.Steps.Should().BeEquivalentTo(steps);
        trace.FailedAtIndex.Should().Be(-1);
        trace.FailureReason.Should().Be("none");
    }

    #endregion

    #region ExperienceReplayConfig

    [Fact]
    public void ExperienceReplayConfig_DefaultCreation_ShouldUseDefaults()
    {
        var config = new ExperienceReplayConfig();

        config.BatchSize.Should().Be(10);
        config.MinQualityScore.Should().Be(0.6);
        config.MaxExperiences.Should().Be(100);
        config.PrioritizeHighQuality.Should().BeTrue();
    }

    #endregion

    #region ExperimentResult

    [Fact]
    public void ExperimentResult_Creation_ShouldSetProperties()
    {
        var started = DateTime.UtcNow.AddHours(-1);
        var completed = DateTime.UtcNow;
        var variants = new List<VariantResult>();
        var result = new ExperimentResult("exp-1", started, completed, variants, null, null, ExperimentStatus.Completed);

        result.ExperimentId.Should().Be("exp-1");
        result.StartedAt.Should().Be(started);
        result.CompletedAt.Should().Be(completed);
        result.VariantResults.Should().BeEquivalentTo(variants);
        result.Analysis.Should().BeNull();
        result.Winner.Should().BeNull();
        result.Status.Should().Be(ExperimentStatus.Completed);
    }

    [Fact]
    public void ExperimentResult_Duration_ShouldCalculateCorrectly()
    {
        var started = DateTime.UtcNow.AddHours(-1);
        var completed = DateTime.UtcNow;
        var result = new ExperimentResult("exp-1", started, completed, new List<VariantResult>(), null, null, ExperimentStatus.Completed);

        result.Duration.Should().BeCloseTo(TimeSpan.FromHours(1), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ExperimentResult_IsCompleted_CompletedStatus_ShouldBeTrue()
    {
        var result = new ExperimentResult("exp-1", DateTime.UtcNow, DateTime.UtcNow, new List<VariantResult>(), null, null, ExperimentStatus.Completed);
        result.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public void ExperimentResult_IsCompleted_RunningStatus_ShouldBeFalse()
    {
        var result = new ExperimentResult("exp-1", DateTime.UtcNow, DateTime.UtcNow, new List<VariantResult>(), null, null, ExperimentStatus.Running);
        result.IsCompleted.Should().BeFalse();
    }

    #endregion

    #region ExperimentState

    [Fact]
    public void ExperimentState_Creation_ShouldSetProperties()
    {
        var started = DateTime.UtcNow;
        var state = new ExperimentState("exp-1", started);

        state.ExperimentId.Should().Be("exp-1");
        state.StartedAt.Should().Be(started);
    }

    #endregion

    #region ExperimentStatus

    [Theory]
    [InlineData(ExperimentStatus.Running)]
    [InlineData(ExperimentStatus.Completed)]
    [InlineData(ExperimentStatus.Cancelled)]
    [InlineData(ExperimentStatus.Failed)]
    public void ExperimentStatus_AllValues_ShouldBeDefined(ExperimentStatus status)
    {
        ((int)status).Should().BeGreaterOrEqualTo(0);
    }

    #endregion

    #region ExplanationLevel

    [Theory]
    [InlineData(ExplanationLevel.Brief)]
    [InlineData(ExplanationLevel.Detailed)]
    [InlineData(ExplanationLevel.Causal)]
    [InlineData(ExplanationLevel.Counterfactual)]
    public void ExplanationLevel_AllValues_ShouldBeDefined(ExplanationLevel level)
    {
        ((int)level).Should().BeGreaterOrEqualTo(0);
    }

    #endregion

    #region HierarchicalPlan

    [Fact]
    public void HierarchicalPlan_Creation_ShouldSetProperties()
    {
        var subPlans = new Dictionary<string, Plan>();
        var createdAt = DateTime.UtcNow;
        var plan = new HierarchicalPlan("goal", new Plan("goal", new List<PlanStep>()), subPlans, 3, createdAt);

        plan.Goal.Should().Be("goal");
        plan.TopLevelPlan.Should().NotBeNull();
        plan.SubPlans.Should().BeEquivalentTo(subPlans);
        plan.MaxDepth.Should().Be(3);
        plan.CreatedAt.Should().Be(createdAt);
    }

    #endregion

    #region HierarchicalPlanningConfig

    [Fact]
    public void HierarchicalPlanningConfig_DefaultCreation_ShouldUseDefaults()
    {
        var config = new HierarchicalPlanningConfig();

        config.MaxDepth.Should().Be(3);
        config.MinStepsForDecomposition.Should().Be(3);
        config.ComplexityThreshold.Should().Be(0.7);
    }

    #endregion

    #region HtnHierarchicalPlan

    [Fact]
    public void HtnHierarchicalPlan_Creation_ShouldSetProperties()
    {
        var abstractTasks = new List<AbstractTask>();
        var refinements = new List<ConcretePlan>();
        var plan = new HtnHierarchicalPlan("goal", abstractTasks, refinements);

        plan.Goal.Should().Be("goal");
        plan.AbstractTasks.Should().BeEquivalentTo(abstractTasks);
        plan.Refinements.Should().BeEquivalentTo(refinements);
    }

    #endregion

    #region HumanFeedbackRequest

    [Fact]
    public void HumanFeedbackRequest_Creation_ShouldSetProperties()
    {
        var options = new List<string> { "yes", "no" };
        var requestedAt = DateTime.UtcNow;
        var request = new HumanFeedbackRequest("req-1", "Context", "Question?", options, requestedAt, TimeSpan.FromMinutes(5));

        request.RequestId.Should().Be("req-1");
        request.Context.Should().Be("Context");
        request.Question.Should().Be("Question?");
        request.Options.Should().BeEquivalentTo(options);
        request.RequestedAt.Should().Be(requestedAt);
        request.Timeout.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void HumanFeedbackRequest_Creation_NullOptions_ShouldAllow()
    {
        var request = new HumanFeedbackRequest("req-1", "Context", "Question?", null, DateTime.UtcNow, TimeSpan.FromMinutes(5));
        request.Options.Should().BeNull();
    }

    #endregion

    #region HumanFeedbackResponse

    [Fact]
    public void HumanFeedbackResponse_Creation_ShouldSetProperties()
    {
        var metadata = new Dictionary<string, object> { ["key"] = "val" };
        var respondedAt = DateTime.UtcNow;
        var response = new HumanFeedbackResponse("req-1", "Answer", metadata, respondedAt);

        response.RequestId.Should().Be("req-1");
        response.Response.Should().Be("Answer");
        response.Metadata.Should().BeEquivalentTo(metadata);
        response.RespondedAt.Should().Be(respondedAt);
    }

    #endregion

    #region HumanInTheLoopConfig

    [Fact]
    public void HumanInTheLoopConfig_DefaultCreation_ShouldUseDefaults()
    {
        var config = new HumanInTheLoopConfig();

        config.RequireApprovalForCriticalSteps.Should().BeTrue();
        config.EnableInteractiveRefinement.Should().BeTrue();
    }

    [Fact]
    public void HumanInTheLoopConfig_Creation_WithCriticalPatterns_ShouldSetProperties()
    {
        var patterns = new List<string> { "deploy", "delete" };
        var config = new HumanInTheLoopConfig(true, true, TimeSpan.FromMinutes(10), patterns);

        config.CriticalActionPatterns.Should().BeEquivalentTo(patterns);
    }

    #endregion

    #region ImprovementPhase

    [Theory]
    [InlineData(ImprovementPhase.Plan, 1)]
    [InlineData(ImprovementPhase.Execute, 2)]
    [InlineData(ImprovementPhase.Verify, 3)]
    [InlineData(ImprovementPhase.Learn, 4)]
    public void ImprovementPhase_AllValues_ShouldHaveCorrectValues(ImprovementPhase phase, int expected)
    {
        ((int)phase).Should().Be(expected);
    }

    #endregion

    #region NextNodeCandidate

    [Fact]
    public void NextNodeCandidate_Creation_ShouldSetProperties()
    {
        var candidate = new NextNodeCandidate("node-1", "action", 0.95);

        candidate.NodeId.Should().Be("node-1");
        candidate.Action.Should().Be("action");
        candidate.Confidence.Should().Be(0.95);
    }

    #endregion

    #region OrchestrationObservabilityConfig

    [Fact]
    public void OrchestrationObservabilityConfig_DefaultCreation_ShouldUseDefaults()
    {
        var config = new OrchestrationObservabilityConfig();

        config.EnableTracing.Should().BeTrue();
        config.EnableMetrics.Should().BeTrue();
        config.EnableDetailedTags.Should().BeFalse();
        config.SamplingRate.Should().Be(1.0);
    }

    #endregion

    #region OuroborosCapability

    [Fact]
    public void OuroborosCapability_Creation_ShouldSetProperties()
    {
        var cap = new OuroborosCapability("planning", "Create plans", 0.8);

        cap.Name.Should().Be("planning");
        cap.Description.Should().Be("Create plans");
        cap.ConfidenceLevel.Should().Be(0.8);
    }

    #endregion

    #region OuroborosConfidence

    [Theory]
    [InlineData(OuroborosConfidence.High)]
    [InlineData(OuroborosConfidence.Medium)]
    [InlineData(OuroborosConfidence.Low)]
    public void OuroborosConfidence_AllValues_ShouldBeDefined(OuroborosConfidence confidence)
    {
        ((int)confidence).Should().BeGreaterOrEqualTo(0);
    }

    #endregion

    #region OuroborosExperience

    [Fact]
    public void OuroborosExperience_Creation_ShouldSetProperties()
    {
        var id = Guid.NewGuid();
        var insights = new List<string> { "insight1" };
        var timestamp = DateTime.UtcNow;
        var exp = new OuroborosExperience(id, "goal", true, 0.9, insights, timestamp, TimeSpan.FromMinutes(1));

        exp.Id.Should().Be(id);
        exp.Goal.Should().Be("goal");
        exp.Success.Should().BeTrue();
        exp.QualityScore.Should().Be(0.9);
        exp.Insights.Should().BeEquivalentTo(insights);
        exp.Timestamp.Should().Be(timestamp);
        exp.Duration.Should().Be(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void OuroborosExperience_Creation_DefaultDuration_ShouldBeZero()
    {
        var exp = new OuroborosExperience(Guid.NewGuid(), "goal", true, 0.9, new List<string>(), DateTime.UtcNow);
        exp.Duration.Should().Be(TimeSpan.Zero);
    }

    #endregion

    #region OuroborosLimitation

    [Fact]
    public void OuroborosLimitation_Creation_WithMitigation_ShouldSetProperties()
    {
        var lim = new OuroborosLimitation("bounded_context", "Limited context", "Use chunking");

        lim.Name.Should().Be("bounded_context");
        lim.Description.Should().Be("Limited context");
        lim.Mitigation.Should().Be("Use chunking");
    }

    [Fact]
    public void OuroborosLimitation_Creation_WithoutMitigation_ShouldAllowNull()
    {
        var lim = new OuroborosLimitation("name", "desc");
        lim.Mitigation.Should().BeNull();
    }

    #endregion

    #region OuroborosResult

    [Fact]
    public void OuroborosResult_Creation_ShouldSetProperties()
    {
        var phaseResults = new List<PhaseResult>();
        var metadata = new Dictionary<string, object> { ["key"] = "val" };
        var result = new OuroborosResult("goal", true, "output", phaseResults, 5, ImprovementPhase.Plan, "reflection", TimeSpan.FromMinutes(2), metadata);

        result.Goal.Should().Be("goal");
        result.Success.Should().BeTrue();
        result.Output.Should().Be("output");
        result.PhaseResults.Should().BeEquivalentTo(phaseResults);
        result.CycleCount.Should().Be(5);
        result.CurrentPhase.Should().Be(ImprovementPhase.Plan);
        result.SelfReflection.Should().Be("reflection");
        result.Duration.Should().Be(TimeSpan.FromMinutes(2));
        result.Metadata.Should().BeEquivalentTo(metadata);
    }

    #endregion

    #region PhaseResult

    [Fact]
    public void PhaseResult_Creation_ShouldSetProperties()
    {
        var result = new PhaseResult(ImprovementPhase.Plan, true, "output", null, TimeSpan.FromSeconds(1));

        result.Phase.Should().Be(ImprovementPhase.Plan);
        result.Success.Should().BeTrue();
        result.Output.Should().Be("output");
        result.Error.Should().BeNull();
        result.Duration.Should().Be(TimeSpan.FromSeconds(1));
        result.Metadata.Should().NotBeNull();
        result.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void PhaseResult_Creation_WithMetadata_ShouldSetProperties()
    {
        var metadata = new Dictionary<string, object> { ["key"] = "val" };
        var result = new PhaseResult(ImprovementPhase.Execute, false, "", "error", TimeSpan.FromSeconds(1), metadata);

        result.Metadata.Should().BeEquivalentTo(metadata);
    }

    #endregion

    #region PromptResult

    [Fact]
    public void PromptResult_Creation_Success_ShouldSetProperties()
    {
        var result = new PromptResult("prompt", true, 150.0, 0.9, "model-1", null);

        result.Prompt.Should().Be("prompt");
        result.Success.Should().BeTrue();
        result.LatencyMs.Should().Be(150.0);
        result.ConfidenceScore.Should().Be(0.9);
        result.SelectedModel.Should().Be("model-1");
        result.Error.Should().BeNull();
    }

    [Fact]
    public void PromptResult_Creation_Failure_ShouldSetProperties()
    {
        var result = new PromptResult("prompt", false, 0, 0, null, "timeout");

        result.Success.Should().BeFalse();
        result.Error.Should().Be("timeout");
    }

    #endregion

    #region PullRequest

    [Fact]
    public void PullRequest_Creation_ShouldSetProperties()
    {
        var reviewers = new List<string> { "alice", "bob" };
        var createdAt = DateTime.UtcNow;
        var pr = new PullRequest("pr-1", "Title", "Desc", "spec", reviewers, createdAt);

        pr.Id.Should().Be("pr-1");
        pr.Title.Should().Be("Title");
        pr.Description.Should().Be("Desc");
        pr.DraftSpec.Should().Be("spec");
        pr.RequiredReviewers.Should().BeEquivalentTo(reviewers);
        pr.CreatedAt.Should().Be(createdAt);
    }

    #endregion

    #region QdrantSkillConfig

    [Fact]
    public void QdrantSkillConfig_DefaultCreation_ShouldUseDefaults()
    {
        var config = new QdrantSkillConfig();

        config.ConnectionString.Should().Be("http://localhost:6334");
        config.CollectionName.Should().Be("ouroboros_skills");
        config.AutoSave.Should().BeTrue();
        config.VectorSize.Should().Be(1536);
    }

    #endregion

    #region QdrantSkillRegistryStats

    [Fact]
    public void QdrantSkillRegistryStats_Creation_ShouldSetProperties()
    {
        var stats = new QdrantSkillRegistryStats(10, 0.8, 100, "skill1", "skill2", "conn", "collection", true);

        stats.TotalSkills.Should().Be(10);
        stats.AverageSuccessRate.Should().Be(0.8);
        stats.TotalExecutions.Should().Be(100);
        stats.MostUsedSkill.Should().Be("skill1");
        stats.MostSuccessfulSkill.Should().Be("skill2");
        stats.ConnectionString.Should().Be("conn");
        stats.CollectionName.Should().Be("collection");
        stats.IsConnected.Should().BeTrue();
    }

    #endregion

    #region RepairStrategy

    [Theory]
    [InlineData(RepairStrategy.Replan)]
    [InlineData(RepairStrategy.Patch)]
    [InlineData(RepairStrategy.CaseBased)]
    [InlineData(RepairStrategy.Backtrack)]
    public void RepairStrategy_AllValues_ShouldBeDefined(RepairStrategy strategy)
    {
        ((int)strategy).Should().BeGreaterOrEqualTo(0);
    }

    #endregion

    #region ReviewComment

    [Fact]
    public void ReviewComment_Creation_ShouldSetProperties()
    {
        var createdAt = DateTime.UtcNow;
        var comment = new ReviewComment("c1", "reviewer", "content", ReviewCommentStatus.Open, createdAt);

        comment.CommentId.Should().Be("c1");
        comment.ReviewerId.Should().Be("reviewer");
        comment.Content.Should().Be("content");
        comment.Status.Should().Be(ReviewCommentStatus.Open);
        comment.CreatedAt.Should().Be(createdAt);
        comment.ResolvedAt.Should().BeNull();
    }

    [Fact]
    public void ReviewComment_WithResolvedAt_ShouldSetProperties()
    {
        var createdAt = DateTime.UtcNow;
        var resolvedAt = DateTime.UtcNow.AddMinutes(5);
        var comment = new ReviewComment("c1", "reviewer", "content", ReviewCommentStatus.Resolved, createdAt, resolvedAt);

        comment.ResolvedAt.Should().Be(resolvedAt);
    }

    #endregion

    #region ReviewCommentStatus

    [Theory]
    [InlineData(ReviewCommentStatus.Open)]
    [InlineData(ReviewCommentStatus.Resolved)]
    [InlineData(ReviewCommentStatus.Dismissed)]
    public void ReviewCommentStatus_AllValues_ShouldBeDefined(ReviewCommentStatus status)
    {
        ((int)status).Should().BeGreaterOrEqualTo(0);
    }

    #endregion

    #region ReviewDecision

    [Fact]
    public void ReviewDecision_Creation_Approved_ShouldSetProperties()
    {
        var reviewedAt = DateTime.UtcNow;
        var decision = new ReviewDecision("reviewer", true, "LGTM", null, reviewedAt);

        decision.ReviewerId.Should().Be("reviewer");
        decision.Approved.Should().BeTrue();
        decision.Feedback.Should().Be("LGTM");
        decision.Comments.Should().BeNull();
        decision.ReviewedAt.Should().Be(reviewedAt);
    }

    #endregion

    #region ReviewState

    [Fact]
    public void ReviewState_Creation_ShouldSetProperties()
    {
        var pr = new PullRequest("pr-1", "Title", "Desc", "spec", new List<string>(), DateTime.UtcNow);
        var reviews = new List<ReviewDecision>();
        var comments = new List<ReviewComment>();
        var lastUpdated = DateTime.UtcNow;
        var state = new ReviewState(pr, reviews, comments, ReviewStatus.AwaitingReview, lastUpdated);

        state.PR.Should().Be(pr);
        state.Reviews.Should().BeEquivalentTo(reviews);
        state.AllComments.Should().BeEquivalentTo(comments);
        state.Status.Should().Be(ReviewStatus.AwaitingReview);
        state.LastUpdatedAt.Should().Be(lastUpdated);
    }

    #endregion

    #region ReviewStatus

    [Theory]
    [InlineData(ReviewStatus.Draft)]
    [InlineData(ReviewStatus.AwaitingReview)]
    [InlineData(ReviewStatus.ChangesRequested)]
    [InlineData(ReviewStatus.Approved)]
    [InlineData(ReviewStatus.Merged)]
    public void ReviewStatus_AllValues_ShouldBeDefined(ReviewStatus status)
    {
        ((int)status).Should().BeGreaterOrEqualTo(0);
    }

    #endregion

    #region SafetyConstraints

    [Fact]
    public void SafetyConstraints_None_ShouldBeZero()
    {
        ((int)SafetyConstraints.None).Should().Be(0);
    }

    [Fact]
    public void SafetyConstraints_All_ShouldCombineAllFlags()
    {
        var all = SafetyConstraints.NoSelfDestruction | SafetyConstraints.PreserveHumanOversight | SafetyConstraints.BoundedResourceUse | SafetyConstraints.ReversibleActions;
        SafetyConstraints.All.Should().Be(all);
    }

    [Theory]
    [InlineData(SafetyConstraints.NoSelfDestruction, 1)]
    [InlineData(SafetyConstraints.PreserveHumanOversight, 2)]
    [InlineData(SafetyConstraints.BoundedResourceUse, 4)]
    [InlineData(SafetyConstraints.ReversibleActions, 8)]
    public void SafetyConstraints_IndividualValues_ShouldBeCorrect(SafetyConstraints constraint, int expected)
    {
        ((int)constraint).Should().Be(expected);
    }

    [Fact]
    public void SafetyConstraints_HasFlag_Combination_ShouldWork()
    {
        var constraints = SafetyConstraints.NoSelfDestruction | SafetyConstraints.PreserveHumanOversight;
        constraints.HasFlag(SafetyConstraints.NoSelfDestruction).Should().BeTrue();
        constraints.HasFlag(SafetyConstraints.PreserveHumanOversight).Should().BeTrue();
        constraints.HasFlag(SafetyConstraints.BoundedResourceUse).Should().BeFalse();
    }

    #endregion

    #region ScheduledTask

    [Fact]
    public void ScheduledTask_Creation_ShouldSetProperties()
    {
        var start = DateTime.UtcNow;
        var end = start.AddHours(1);
        var deps = new List<string> { "dep1" };
        var task = new ScheduledTask("task1", start, end, deps);

        task.Name.Should().Be("task1");
        task.StartTime.Should().Be(start);
        task.EndTime.Should().Be(end);
        task.Dependencies.Should().BeEquivalentTo(deps);
    }

    #endregion

    #region SkillCompositionConfig

    [Fact]
    public void SkillCompositionConfig_DefaultCreation_ShouldUseDefaults()
    {
        var config = new SkillCompositionConfig();

        config.MaxComponentSkills.Should().Be(5);
        config.MinComponentQuality.Should().Be(0.7);
        config.AllowRecursiveComposition.Should().BeFalse();
    }

    #endregion

    #region SkillRegistryStats

    [Fact]
    public void SkillRegistryStats_Creation_ShouldSetProperties()
    {
        var stats = new SkillRegistryStats(10, 0.8, 100, "mostUsed", "mostSuccessful", "/path", true);

        stats.TotalSkills.Should().Be(10);
        stats.AverageSuccessRate.Should().Be(0.8);
        stats.TotalExecutions.Should().Be(100);
        stats.MostUsedSkill.Should().Be("mostUsed");
        stats.MostSuccessfulSkill.Should().Be("mostSuccessful");
        stats.StoragePath.Should().Be("/path");
        stats.IsPersisted.Should().BeTrue();
    }

    #endregion

    #region SkillSuggestion

    [Fact]
    public void SkillSuggestion_Creation_ShouldSetProperties()
    {
        var skill = new Skill("test", "desc", new List<PlanStep>());
        var suggestion = new SkillSuggestion("token", skill, 0.95, "usage example");

        suggestion.TokenName.Should().Be("token");
        suggestion.Skill.Should().Be(skill);
        suggestion.RelevanceScore.Should().Be(0.95);
        suggestion.UsageExample.Should().Be("usage example");
    }

    #endregion

    #region StakeholderReviewConfig

    [Fact]
    public void StakeholderReviewConfig_DefaultCreation_ShouldUseDefaults()
    {
        var config = new StakeholderReviewConfig();

        config.MinimumRequiredApprovals.Should().Be(2);
        config.RequireAllReviewersApprove.Should().BeTrue();
        config.AutoResolveNonBlockingComments.Should().BeFalse();
    }

    #endregion

    #region StakeholderReviewResult

    [Fact]
    public void StakeholderReviewResult_Creation_ShouldSetProperties()
    {
        var pr = new PullRequest("pr-1", "Title", "Desc", "spec", new List<string>(), DateTime.UtcNow);
        var state = new ReviewState(pr, new List<ReviewDecision>(), new List<ReviewComment>(), ReviewStatus.Approved, DateTime.UtcNow);
        var result = new StakeholderReviewResult(state, true, 3, 3, 2, 0, TimeSpan.FromHours(1), "All approved");

        result.FinalState.Should().Be(state);
        result.AllApproved.Should().BeTrue();
        result.TotalReviewers.Should().Be(3);
        result.ApprovedCount.Should().Be(3);
        result.CommentsResolved.Should().Be(2);
        result.CommentsRemaining.Should().Be(0);
        result.Duration.Should().Be(TimeSpan.FromHours(1));
        result.Summary.Should().Be("All approved");
    }

    #endregion

    #region StatisticalAnalysis

    [Fact]
    public void StatisticalAnalysis_Creation_ShouldSetProperties()
    {
        var analysis = new StatisticalAnalysis(0.5, true, "Significant improvement");

        analysis.EffectSize.Should().Be(0.5);
        analysis.IsSignificant.Should().BeTrue();
        analysis.Interpretation.Should().Be("Significant improvement");
    }

    #endregion

    #region SubIssueAssignment

    [Fact]
    public void SubIssueAssignment_Creation_ShouldSetProperties()
    {
        var createdAt = DateTime.UtcNow;
        var assignment = new SubIssueAssignment(1, "Title", "Desc", "agent-1", "branch-1", null, SubIssueStatus.Pending, createdAt);

        assignment.IssueNumber.Should().Be(1);
        assignment.Title.Should().Be("Title");
        assignment.Description.Should().Be("Desc");
        assignment.AssignedAgentId.Should().Be("agent-1");
        assignment.BranchName.Should().Be("branch-1");
        assignment.Branch.Should().BeNull();
        assignment.Status.Should().Be(SubIssueStatus.Pending);
        assignment.CreatedAt.Should().Be(createdAt);
        assignment.CompletedAt.Should().BeNull();
        assignment.ErrorMessage.Should().BeNull();
    }

    #endregion

    #region SubIssueStatus

    [Theory]
    [InlineData(SubIssueStatus.Pending)]
    [InlineData(SubIssueStatus.BranchCreated)]
    [InlineData(SubIssueStatus.InProgress)]
    [InlineData(SubIssueStatus.Completed)]
    [InlineData(SubIssueStatus.Failed)]
    public void SubIssueStatus_AllValues_ShouldBeDefined(SubIssueStatus status)
    {
        ((int)status).Should().BeGreaterOrEqualTo(0);
    }

    #endregion

    #region TaskAssignment

    [Fact]
    public void TaskAssignment_Creation_ShouldSetProperties()
    {
        var step = new PlanStep("action", "expected", new Dictionary<string, object>());
        var assignedAt = DateTime.UtcNow;
        var assignment = new TaskAssignment("task-1", "agent-1", step, assignedAt, TaskAssignmentStatus.Pending);

        assignment.TaskId.Should().Be("task-1");
        assignment.AgentId.Should().Be("agent-1");
        assignment.Step.Should().Be(step);
        assignment.AssignedAt.Should().Be(assignedAt);
        assignment.Status.Should().Be(TaskAssignmentStatus.Pending);
    }

    #endregion

    #region TaskAssignmentStatus

    [Theory]
    [InlineData(TaskAssignmentStatus.Pending)]
    [InlineData(TaskAssignmentStatus.InProgress)]
    [InlineData(TaskAssignmentStatus.Completed)]
    [InlineData(TaskAssignmentStatus.Failed)]
    public void TaskAssignmentStatus_AllValues_ShouldBeDefined(TaskAssignmentStatus status)
    {
        ((int)status).Should().BeGreaterOrEqualTo(0);
    }

    #endregion

    #region TaskDecomposition

    [Fact]
    public void TaskDecomposition_Creation_ShouldSetProperties()
    {
        var subTasks = new List<string> { "sub1", "sub2" };
        var ordering = new List<string> { "sub1 before sub2" };
        var decomp = new TaskDecomposition("task", subTasks, ordering);

        decomp.AbstractTask.Should().Be("task");
        decomp.SubTasks.Should().BeEquivalentTo(subTasks);
        decomp.OrderingConstraints.Should().BeEquivalentTo(ordering);
    }

    #endregion

    #region TemporalConstraint

    [Fact]
    public void TemporalConstraint_Creation_ShouldSetProperties()
    {
        var constraint = new TemporalConstraint("taskA", "taskB", TemporalRelation.Before, TimeSpan.FromMinutes(5));

        constraint.TaskA.Should().Be("taskA");
        constraint.TaskB.Should().Be("taskB");
        constraint.Relation.Should().Be(TemporalRelation.Before);
        constraint.Duration.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void TemporalConstraint_Creation_WithoutDuration_ShouldAllowNull()
    {
        var constraint = new TemporalConstraint("taskA", "taskB", TemporalRelation.After);
        constraint.Duration.Should().BeNull();
    }

    #endregion

    #region TemporalPlan

    [Fact]
    public void TemporalPlan_Creation_ShouldSetProperties()
    {
        var tasks = new List<ScheduledTask>();
        var plan = new TemporalPlan("goal", tasks, TimeSpan.FromHours(1));

        plan.Goal.Should().Be("goal");
        plan.Tasks.Should().BeEquivalentTo(tasks);
        plan.TotalDuration.Should().Be(TimeSpan.FromHours(1));
    }

    #endregion

    #region TemporalRelation

    [Theory]
    [InlineData(TemporalRelation.Before)]
    [InlineData(TemporalRelation.After)]
    [InlineData(TemporalRelation.During)]
    [InlineData(TemporalRelation.Overlaps)]
    [InlineData(TemporalRelation.MustFinishBefore)]
    [InlineData(TemporalRelation.Simultaneous)]
    public void TemporalRelation_AllValues_ShouldBeDefined(TemporalRelation relation)
    {
        ((int)relation).Should().BeGreaterOrEqualTo(0);
    }

    #endregion

    #region TestCase

    [Fact]
    public void TestCase_Creation_ShouldSetProperties()
    {
        var context = new Dictionary<string, object> { ["key"] = "val" };
        Func<PlanVerificationResult, bool> validator = result => true;
        var testCase = new TestCase("test1", "goal", context, validator);

        testCase.Name.Should().Be("test1");
        testCase.Goal.Should().Be("goal");
        testCase.Context.Should().BeEquivalentTo(context);
        testCase.CustomValidator.Should().BeSameAs(validator);
    }

    [Fact]
    public void TestCase_Creation_NullContextAndValidator_ShouldAllow()
    {
        var testCase = new TestCase("test1", "goal", null, null);
        testCase.Context.Should().BeNull();
        testCase.CustomValidator.Should().BeNull();
    }

    #endregion

    #region ToolCategory

    [Theory]
    [InlineData(ToolCategory.General)]
    [InlineData(ToolCategory.Code)]
    [InlineData(ToolCategory.FileSystem)]
    [InlineData(ToolCategory.Web)]
    [InlineData(ToolCategory.Knowledge)]
    [InlineData(ToolCategory.Analysis)]
    [InlineData(ToolCategory.Validation)]
    [InlineData(ToolCategory.Text)]
    [InlineData(ToolCategory.Reasoning)]
    [InlineData(ToolCategory.Creative)]
    [InlineData(ToolCategory.Utility)]
    public void ToolCategory_AllValues_ShouldBeDefined(ToolCategory category)
    {
        ((int)category).Should().BeGreaterOrEqualTo(0);
    }

    #endregion

    #region ToolRecommendation

    [Fact]
    public void ToolRecommendation_Creation_ShouldSetProperties()
    {
        var rec = new ToolRecommendation("tool1", "Description", 0.8, ToolCategory.Code);

        rec.ToolName.Should().Be("tool1");
        rec.Description.Should().Be("Description");
        rec.RelevanceScore.Should().Be(0.8);
        rec.Category.Should().Be(ToolCategory.Code);
    }

    [Fact]
    public void ToolRecommendation_IsHighlyRecommended_HighScore_ShouldBeTrue()
    {
        var rec = new ToolRecommendation("tool1", "Description", 0.8, ToolCategory.Code);
        rec.IsHighlyRecommended.Should().BeTrue();
    }

    [Fact]
    public void ToolRecommendation_IsHighlyRecommended_LowScore_ShouldBeFalse()
    {
        var rec = new ToolRecommendation("tool1", "Description", 0.6, ToolCategory.Code);
        rec.IsHighlyRecommended.Should().BeFalse();
    }

    [Fact]
    public void ToolRecommendation_IsRecommended_MediumScore_ShouldBeTrue()
    {
        var rec = new ToolRecommendation("tool1", "Description", 0.5, ToolCategory.Code);
        rec.IsRecommended.Should().BeTrue();
    }

    [Fact]
    public void ToolRecommendation_IsRecommended_LowScore_ShouldBeFalse()
    {
        var rec = new ToolRecommendation("tool1", "Description", 0.3, ToolCategory.Code);
        rec.IsRecommended.Should().BeFalse();
    }

    #endregion

    #region ToolSelection

    [Fact]
    public void ToolSelection_Creation_ShouldSetProperties()
    {
        var selection = new ToolSelection("tool1", "{\"arg\":\"value\"}");

        selection.ToolName.Should().Be("tool1");
        selection.ArgumentsJson.Should().Be("{\"arg\":\"value\"}");
    }

    #endregion

    #region ToolSelectionContext

    [Fact]
    public void ToolSelectionContext_Creation_ShouldSetDefaults()
    {
        var ctx = new ToolSelectionContext();

        ctx.MaxTools.Should().BeNull();
        ctx.RequiredCategories.Should().BeNull();
        ctx.ExcludedCategories.Should().BeNull();
        ctx.RequiredToolNames.Should().BeNull();
        ctx.PreferFastTools.Should().BeFalse();
        ctx.PreferReliableTools.Should().BeFalse();
    }

    [Fact]
    public void ToolSelectionContext_WithValues_ShouldSetProperties()
    {
        var ctx = new ToolSelectionContext
        {
            MaxTools = 5,
            RequiredCategories = new List<ToolCategory> { ToolCategory.Code },
            ExcludedCategories = new List<ToolCategory> { ToolCategory.Web },
            RequiredToolNames = new List<string> { "tool1" },
            PreferFastTools = true,
            PreferReliableTools = true
        };

        ctx.MaxTools.Should().Be(5);
        ctx.RequiredCategories.Should().ContainSingle().Which.Should().Be(ToolCategory.Code);
        ctx.ExcludedCategories.Should().ContainSingle().Which.Should().Be(ToolCategory.Web);
        ctx.RequiredToolNames.Should().ContainSingle().Which.Should().Be("tool1");
        ctx.PreferFastTools.Should().BeTrue();
        ctx.PreferReliableTools.Should().BeTrue();
    }

    #endregion

    #region TrainingBatch

    [Fact]
    public void TrainingBatch_Creation_ShouldSetProperties()
    {
        var experiences = new List<Experience>();
        var metrics = new Dictionary<string, double> { ["accuracy"] = 0.9 };
        var createdAt = DateTime.UtcNow;
        var batch = new TrainingBatch(experiences, metrics, createdAt);

        batch.Experiences.Should().BeEquivalentTo(experiences);
        batch.Metrics.Should().BeEquivalentTo(metrics);
        batch.CreatedAt.Should().Be(createdAt);
    }

    #endregion

    #region TrainingResult

    [Fact]
    public void TrainingResult_Creation_ShouldSetProperties()
    {
        var metrics = new Dictionary<string, double> { ["accuracy"] = 0.95 };
        var patterns = new List<string> { "pattern1" };
        var result = new TrainingResult(10, metrics, patterns, true);

        result.ExperiencesProcessed.Should().Be(10);
        result.ImprovedMetrics.Should().BeEquivalentTo(metrics);
        result.LearnedPatterns.Should().BeEquivalentTo(patterns);
        result.Success.Should().BeTrue();
    }

    #endregion

    #region VariantMetrics

    [Fact]
    public void VariantMetrics_Creation_ShouldSetProperties()
    {
        var metrics = new VariantMetrics(0.9, 100.0, 200.0, 300.0, 0.8, 50, 45);

        metrics.SuccessRate.Should().Be(0.9);
        metrics.AverageLatencyMs.Should().Be(100.0);
        metrics.P95LatencyMs.Should().Be(200.0);
        metrics.P99LatencyMs.Should().Be(300.0);
        metrics.AverageConfidence.Should().Be(0.8);
        metrics.TotalPrompts.Should().Be(50);
        metrics.SuccessfulPrompts.Should().Be(45);
    }

    #endregion

    #region VariantResult

    [Fact]
    public void VariantResult_Creation_ShouldSetProperties()
    {
        var prompts = new List<PromptResult>();
        var metrics = new VariantMetrics(0.9, 100.0, 200.0, 300.0, 0.8, 50, 45);
        var result = new VariantResult("variant-1", prompts, metrics);

        result.VariantId.Should().Be("variant-1");
        result.PromptResults.Should().BeEquivalentTo(prompts);
        result.Metrics.Should().Be(metrics);
    }

    #endregion
}
