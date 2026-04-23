using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Agent.Tests.SelfImprovement;

[Trait("Category", "Unit")]
public class SelfImprovementRecordsTests
{
    #region AgentCapability

    [Fact]
    public void AgentCapability_Creation_ShouldSetProperties()
    {
        var tools = new List<string> { "tool1" };
        var limitations = new List<string> { "lim1" };
        var metadata = new Dictionary<string, object> { ["key"] = "val" };
        var created = DateTime.UtcNow;
        var lastUsed = DateTime.UtcNow;

        var cap = new AgentCapability("name", "desc", tools, 0.9, 100.0, limitations, 10, created, lastUsed, metadata);

        cap.Name.Should().Be("name");
        cap.Description.Should().Be("desc");
        cap.RequiredTools.Should().BeEquivalentTo(tools);
        cap.SuccessRate.Should().Be(0.9);
        cap.AverageLatency.Should().Be(100.0);
        cap.KnownLimitations.Should().BeEquivalentTo(limitations);
        cap.UsageCount.Should().Be(10);
        cap.CreatedAt.Should().Be(created);
        cap.LastUsed.Should().Be(lastUsed);
        cap.Metadata.Should().BeEquivalentTo(metadata);
    }

    #endregion

    #region BayesianConfidence

    [Fact]
    public void Update_StrongEvidenceForHypothesis_ShouldIncreaseConfidence()
    {
        var posterior = BayesianConfidence.Update(0.5, 0.9, 0.1);
        posterior.Should().BeGreaterThan(0.5);
    }

    [Fact]
    public void Update_StrongEvidenceAgainstHypothesis_ShouldDecreaseConfidence()
    {
        var posterior = BayesianConfidence.Update(0.5, 0.1, 0.9);
        posterior.Should().BeLessThan(0.5);
    }

    [Fact]
    public void Update_NeutralEvidence_ShouldNotChangeMuch()
    {
        var posterior = BayesianConfidence.Update(0.5, 0.5, 0.5);
        posterior.Should().BeApproximately(0.5, 0.01);
    }

    [Fact]
    public void Update_ZeroEvidenceProbability_ShouldReturnPrior()
    {
        var posterior = BayesianConfidence.Update(0.7, 0.0, 0.0);
        posterior.Should().Be(0.7);
    }

    [Fact]
    public void Update_HighPriorAndLikelihood_ShouldApproachMax()
    {
        var posterior = BayesianConfidence.Update(0.99, 0.99, 0.01);
        posterior.Should().BeLessThanOrEqualTo(0.999);
    }

    [Fact]
    public void Update_LowPriorAndLikelihood_ShouldApproachMin()
    {
        var posterior = BayesianConfidence.Update(0.01, 0.01, 0.99);
        posterior.Should().BeGreaterOrEqualTo(0.001);
    }

    #endregion

    #region CapabilityRegistryConfig

    [Fact]
    public void CapabilityRegistryConfig_DefaultCreation_ShouldUseDefaults()
    {
        var config = new CapabilityRegistryConfig();

        config.MinSuccessRateThreshold.Should().Be(0.6);
        config.MinUsageCountForReliability.Should().Be(5);
    }

    #endregion

    #region CitationMetadata

    [Fact]
    public void CitationMetadata_Creation_ShouldSetProperties()
    {
        var refs = new List<string> { "ref1" };
        var citedBy = new List<string> { "cited1" };
        var meta = new CitationMetadata("id", "Title", 100, 20, refs, citedBy);

        meta.PaperId.Should().Be("id");
        meta.Title.Should().Be("Title");
        meta.CitationCount.Should().Be(100);
        meta.InfluentialCitationCount.Should().Be(20);
        meta.References.Should().BeEquivalentTo(refs);
        meta.CitedBy.Should().BeEquivalentTo(citedBy);
    }

    #endregion

    #region EvidenceStrength

    [Theory]
    [InlineData(EvidenceStrength.Negligible)]
    [InlineData(EvidenceStrength.Substantial)]
    [InlineData(EvidenceStrength.Strong)]
    [InlineData(EvidenceStrength.VeryStrong)]
    [InlineData(EvidenceStrength.Decisive)]
    public void EvidenceStrength_AllValues_ShouldBeDefined(EvidenceStrength strength)
    {
        ((int)strength).Should().BeGreaterOrEqualTo(0);
    }

    #endregion

    #region Experiment

    [Fact]
    public void Experiment_Creation_ShouldSetProperties()
    {
        var hypothesis = new Hypothesis(Guid.NewGuid(), "statement", "domain", 0.5, new List<string>(), new List<string>(), DateTime.UtcNow, false, null);
        var steps = new List<PlanStep>();
        var outcomes = new Dictionary<string, object> { ["key"] = "val" };
        var designedAt = DateTime.UtcNow;
        var exp = new Experiment(Guid.NewGuid(), hypothesis, "desc", steps, outcomes, designedAt);

        exp.Hypothesis.Should().Be(hypothesis);
        exp.Description.Should().Be("desc");
        exp.Steps.Should().BeEquivalentTo(steps);
        exp.ExpectedOutcomes.Should().BeEquivalentTo(outcomes);
        exp.DesignedAt.Should().Be(designedAt);
    }

    #endregion

    #region ExplorationOpportunity

    [Fact]
    public void ExplorationOpportunity_Creation_ShouldSetProperties()
    {
        var prereqs = new List<string> { "prereq1" };
        var identifiedAt = DateTime.UtcNow;
        var opp = new ExplorationOpportunity("desc", 0.8, 0.9, prereqs, identifiedAt);

        opp.Description.Should().Be("desc");
        opp.NoveltyScore.Should().Be(0.8);
        opp.InformationGainEstimate.Should().Be(0.9);
        opp.Prerequisites.Should().BeEquivalentTo(prereqs);
        opp.IdentifiedAt.Should().Be(identifiedAt);
    }

    #endregion

    #region ExternalKnowledgeConfig

    [Fact]
    public void ExternalKnowledgeConfig_DefaultCreation_ShouldUseDefaults()
    {
        var config = new ExternalKnowledgeConfig();

        config.ArxivBaseUrl.Should().Be("http://export.arxiv.org/api/query");
        config.SemanticScholarBaseUrl.Should().Be("https://api.semanticscholar.org/graph/v1");
        config.MaxPapersPerQuery.Should().Be(10);
        config.RequestTimeoutSeconds.Should().Be(30);
        config.RateLimitDelayMs.Should().Be(500);
        config.EnableCaching.Should().BeTrue();
    }

    #endregion

    #region Goal

    [Fact]
    public void Goal_Creation_WithDefaults_ShouldSetProperties()
    {
        var goal = new Goal("description", GoalType.Primary, 0.8);

        goal.Description.Should().Be("description");
        goal.Type.Should().Be(GoalType.Primary);
        goal.Priority.Should().Be(0.8);
        goal.ParentGoal.Should().BeNull();
        goal.Subgoals.Should().BeEmpty();
        goal.IsComplete.Should().BeFalse();
        goal.CompletionReason.Should().BeNull();
    }

    [Fact]
    public void Goal_Creation_Full_ShouldSetProperties()
    {
        var parent = new Goal("parent", GoalType.Primary, 1.0);
        var subgoals = new List<Goal>();
        var constraints = new Dictionary<string, object>();
        var goal = new Goal(Guid.NewGuid(), "desc", GoalType.Secondary, 0.5, parent, subgoals, constraints, DateTime.UtcNow, true, "done");

        goal.ParentGoal.Should().Be(parent);
        goal.IsComplete.Should().BeTrue();
        goal.CompletionReason.Should().Be("done");
    }

    #endregion

    #region GoalConflict

    [Fact]
    public void GoalConflict_Creation_ShouldSetProperties()
    {
        var g1 = new Goal("g1", GoalType.Primary, 1.0);
        var g2 = new Goal("g2", GoalType.Secondary, 0.8);
        var resolutions = new List<string> { "resolve1" };
        var conflict = new GoalConflict(g1, g2, "type", "desc", resolutions);

        conflict.Goal1.Should().Be(g1);
        conflict.Goal2.Should().Be(g2);
        conflict.ConflictType.Should().Be("type");
        conflict.Description.Should().Be("desc");
        conflict.SuggestedResolutions.Should().BeEquivalentTo(resolutions);
    }

    #endregion

    #region GoalHierarchyConfig

    [Fact]
    public void GoalHierarchyConfig_DefaultCreation_ShouldHaveSafetyConstraints()
    {
        var config = new GoalHierarchyConfig();

        config.MaxDepth.Should().Be(3);
        config.MaxSubgoalsPerGoal.Should().Be(5);
        config.SafetyConstraints.Should().NotBeNullOrEmpty();
        config.CoreValues.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region GoalType

    [Theory]
    [InlineData(GoalType.Primary)]
    [InlineData(GoalType.Secondary)]
    [InlineData(GoalType.Instrumental)]
    [InlineData(GoalType.Safety)]
    public void GoalType_AllValues_ShouldBeDefined(GoalType type)
    {
        ((int)type).Should().BeGreaterOrEqualTo(0);
    }

    #endregion

    #region Hypothesis

    [Fact]
    public void Hypothesis_Creation_ShouldSetProperties()
    {
        var supporting = new List<string> { "ev1" };
        var counter = new List<string> { "cev1" };
        var created = DateTime.UtcNow;
        var h = new Hypothesis(Guid.NewGuid(), "statement", "domain", 0.5, supporting, counter, created, false, null);

        h.Statement.Should().Be("statement");
        h.Domain.Should().Be("domain");
        h.Confidence.Should().Be(0.5);
        h.SupportingEvidence.Should().BeEquivalentTo(supporting);
        h.CounterEvidence.Should().BeEquivalentTo(counter);
        h.CreatedAt.Should().Be(created);
        h.Tested.Should().BeFalse();
        h.Validated.Should().BeNull();
    }

    [Fact]
    public void Hypothesis_Creation_TestedAndValidated_ShouldSetProperties()
    {
        var h = new Hypothesis(Guid.NewGuid(), "stmt", "domain", 0.8, new List<string>(), new List<string>(), DateTime.UtcNow, true, true);
        h.Tested.Should().BeTrue();
        h.Validated.Should().BeTrue();
    }

    #endregion

    #region HypothesisEngineConfig

    [Fact]
    public void HypothesisEngineConfig_DefaultCreation_ShouldUseDefaults()
    {
        var config = new HypothesisEngineConfig();

        config.MinConfidenceForTesting.Should().Be(0.3);
        config.MaxHypothesesPerDomain.Should().Be(10);
        config.EnableAbductiveReasoning.Should().BeTrue();
        config.AutoGenerateCounterExamples.Should().BeTrue();
    }

    #endregion

    #region HypothesisTestResult

    [Fact]
    public void HypothesisTestResult_Creation_ShouldSetProperties()
    {
        var hypothesis = new Hypothesis(Guid.NewGuid(), "stmt", "domain", 0.5, new List<string>(), new List<string>(), DateTime.UtcNow, false, null);
        var experiment = new Experiment(Guid.NewGuid(), hypothesis, "desc", new List<PlanStep>(), new Dictionary<string, object>(), DateTime.UtcNow);
        var execution = new PlanExecutionResult(new List<StepResult>(), "out", TimeSpan.FromSeconds(1), true, new Dictionary<string, object>());
        var testedAt = DateTime.UtcNow;
        var result = new HypothesisTestResult(hypothesis, experiment, execution, true, 0.2, "explained", testedAt);

        result.Hypothesis.Should().Be(hypothesis);
        result.Experiment.Should().Be(experiment);
        result.Execution.Should().Be(execution);
        result.HypothesisSupported.Should().BeTrue();
        result.ConfidenceAdjustment.Should().Be(0.2);
        result.Explanation.Should().Be("explained");
        result.TestedAt.Should().Be(testedAt);
    }

    #endregion

    #region ImprovementPlan

    [Fact]
    public void ImprovementPlan_Creation_ShouldSetProperties()
    {
        var actions = new List<string> { "action1" };
        var improvements = new Dictionary<string, double> { ["metric"] = 0.1 };
        var created = DateTime.UtcNow;
        var plan = new ImprovementPlan("goal", actions, improvements, TimeSpan.FromHours(1), 0.8, created);

        plan.Goal.Should().Be("goal");
        plan.Actions.Should().BeEquivalentTo(actions);
        plan.ExpectedImprovements.Should().BeEquivalentTo(improvements);
        plan.EstimatedDuration.Should().Be(TimeSpan.FromHours(1));
        plan.Priority.Should().Be(0.8);
        plan.CreatedAt.Should().Be(created);
    }

    #endregion

    #region Insight

    [Fact]
    public void Insight_Creation_ShouldSetProperties()
    {
        var evidence = new List<string> { "ev1" };
        var discovered = DateTime.UtcNow;
        var insight = new Insight("category", "desc", 0.9, evidence, discovered);

        insight.Category.Should().Be("category");
        insight.Description.Should().Be("desc");
        insight.Confidence.Should().Be(0.9);
        insight.SupportingEvidence.Should().BeEquivalentTo(evidence);
        insight.DiscoveredAt.Should().Be(discovered);
    }

    #endregion

    #region MemoryType

    [Theory]
    [InlineData(MemoryType.Episodic)]
    [InlineData(MemoryType.Semantic)]
    public void MemoryType_AllValues_ShouldBeDefined(MemoryType type)
    {
        ((int)type).Should().BeGreaterOrEqualTo(0);
    }

    #endregion
}
