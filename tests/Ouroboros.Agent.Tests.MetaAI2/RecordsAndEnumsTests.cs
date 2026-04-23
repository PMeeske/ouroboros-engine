using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Agent.Tests;

[Trait("Category", "Unit")]
public class RecordsAndEnumsTests
{
    #region NextNodeCandidate

    [Fact]
    public void NextNodeCandidate_Creation_ShouldSetProperties()
    {
        // Arrange / Act
        var candidate = new NextNodeCandidate("node-1", "execute", 0.85);

        // Assert
        candidate.NodeId.Should().Be("node-1");
        candidate.Action.Should().Be("execute");
        candidate.Confidence.Should().Be(0.85);
    }

    [Fact]
    public void NextNodeCandidate_Equality_SameValuesAreEqual()
    {
        // Arrange
        var a = new NextNodeCandidate("node-1", "execute", 0.85);
        var b = new NextNodeCandidate("node-1", "execute", 0.85);

        // Act / Assert
        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void NextNodeCandidate_Equality_DifferentValuesAreNotEqual()
    {
        // Arrange
        var a = new NextNodeCandidate("node-1", "execute", 0.85);
        var b = new NextNodeCandidate("node-2", "execute", 0.85);

        // Act / Assert
        a.Should().NotBe(b);
    }

    #endregion

    #region OuroborosCapability

    [Fact]
    public void OuroborosCapability_Creation_ShouldSetProperties()
    {
        // Arrange / Act
        var cap = new OuroborosCapability("planning", "Create plans", 0.8);

        // Assert
        cap.Name.Should().Be("planning");
        cap.Description.Should().Be("Create plans");
        cap.ConfidenceLevel.Should().Be(0.8);
    }

    [Fact]
    public void OuroborosCapability_With_ShouldCreateModifiedCopy()
    {
        // Arrange
        var cap = new OuroborosCapability("planning", "Create plans", 0.8);

        // Act
        var modified = cap with { ConfidenceLevel = 0.95 };

        // Assert
        modified.Name.Should().Be("planning");
        modified.ConfidenceLevel.Should().Be(0.95);
        cap.ConfidenceLevel.Should().Be(0.8);
    }

    #endregion

    #region OuroborosExperience

    [Fact]
    public void OuroborosExperience_Creation_ShouldSetProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var insights = new List<string> { "insight1" };

        // Act
        var exp = new OuroborosExperience(id, "goal-1", true, 0.9, insights, DateTime.UtcNow, TimeSpan.FromMinutes(1));

        // Assert
        exp.Id.Should().Be(id);
        exp.Goal.Should().Be("goal-1");
        exp.Success.Should().BeTrue();
        exp.QualityScore.Should().Be(0.9);
        exp.Insights.Should().BeEquivalentTo(insights);
        exp.Duration.Should().Be(TimeSpan.FromMinutes(1));
    }

    #endregion

    #region OuroborosLimitation

    [Fact]
    public void OuroborosLimitation_Creation_WithMitigation()
    {
        // Act
        var lim = new OuroborosLimitation("bounded", "Limited context", "Use chunking");

        // Assert
        lim.Name.Should().Be("bounded");
        lim.Description.Should().Be("Limited context");
        lim.Mitigation.Should().Be("Use chunking");
    }

    [Fact]
    public void OuroborosLimitation_Creation_WithoutMitigation()
    {
        // Act
        var lim = new OuroborosLimitation("bounded", "Limited context");

        // Assert
        lim.Mitigation.Should().BeNull();
    }

    #endregion

    #region OuroborosResult

    [Fact]
    public void OuroborosResult_Creation_ShouldSetProperties()
    {
        // Arrange
        var phases = new List<PhaseResult>();
        var metadata = new Dictionary<string, object> { ["key"] = "value" };

        // Act
        var result = new OuroborosResult(
            "test-goal", true, "output", phases, 1, ImprovementPhase.Plan, "reflection", TimeSpan.FromSeconds(5), metadata);

        // Assert
        result.Goal.Should().Be("test-goal");
        result.Success.Should().BeTrue();
        result.Output.Should().Be("output");
        result.CycleCount.Should().Be(1);
        result.CurrentPhase.Should().Be(ImprovementPhase.Plan);
        result.SelfReflection.Should().Be("reflection");
        result.Duration.Should().Be(TimeSpan.FromSeconds(5));
        result.Metadata.Should().ContainKey("key");
    }

    #endregion

    #region PhaseResult

    [Fact]
    public void PhaseResult_Creation_WithMetadata()
    {
        // Arrange
        var metadata = new Dictionary<string, object> { ["score"] = 0.8 };

        // Act
        var pr = new PhaseResult(ImprovementPhase.Plan, true, "output", null, TimeSpan.FromSeconds(1), metadata);

        // Assert
        pr.Phase.Should().Be(ImprovementPhase.Plan);
        pr.Success.Should().BeTrue();
        pr.Output.Should().Be("output");
        pr.Error.Should().BeNull();
        pr.Metadata.Should().ContainKey("score");
    }

    [Fact]
    public void PhaseResult_Creation_WithoutMetadata_ShouldInitializeEmpty()
    {
        // Act
        var pr = new PhaseResult(ImprovementPhase.Execute, false, "", "error", TimeSpan.FromSeconds(1));

        // Assert
        pr.Metadata.Should().NotBeNull();
        pr.Metadata.Should().BeEmpty();
    }

    #endregion

    #region PromptResult

    [Fact]
    public void PromptResult_Creation_ShouldSetProperties()
    {
        // Act
        var pr = new PromptResult("prompt-1", true, 150.0, 0.9, "gpt-4", null);

        // Assert
        pr.Prompt.Should().Be("prompt-1");
        pr.Success.Should().BeTrue();
        pr.LatencyMs.Should().Be(150.0);
        pr.ConfidenceScore.Should().Be(0.9);
        pr.SelectedModel.Should().Be("gpt-4");
        pr.Error.Should().BeNull();
    }

    #endregion

    #region PullRequest

    [Fact]
    public void PullRequest_Creation_ShouldSetProperties()
    {
        // Arrange
        var reviewers = new List<string> { "alice", "bob" };
        var created = DateTime.UtcNow;

        // Act
        var pr = new PullRequest("pr-1", "Title", "Desc", "spec", reviewers, created);

        // Assert
        pr.Id.Should().Be("pr-1");
        pr.Title.Should().Be("Title");
        pr.Description.Should().Be("Desc");
        pr.DraftSpec.Should().Be("spec");
        pr.RequiredReviewers.Should().BeEquivalentTo(reviewers);
        pr.CreatedAt.Should().Be(created);
    }

    #endregion

    #region QdrantSkillConfig

    [Fact]
    public void QdrantSkillConfig_DefaultCreation_ShouldSetDefaults()
    {
        // Act
#pragma warning disable CS0618 // Type or member is obsolete
        var config = new QdrantSkillConfig();
#pragma warning restore CS0618

        // Assert
        config.ConnectionString.Should().Be("http://localhost:6334");
        config.CollectionName.Should().Be("ouroboros_skills");
        config.AutoSave.Should().BeTrue();
        config.VectorSize.Should().Be(1536);
    }

    [Fact]
    public void QdrantSkillConfig_CustomCreation_ShouldSetValues()
    {
        // Act
#pragma warning disable CS0618
        var config = new QdrantSkillConfig("http://custom:6334", "custom_skills", false, 768);
#pragma warning restore CS0618

        // Assert
        config.ConnectionString.Should().Be("http://custom:6334");
        config.CollectionName.Should().Be("custom_skills");
        config.AutoSave.Should().BeFalse();
        config.VectorSize.Should().Be(768);
    }

    #endregion

    #region QdrantSkillRegistryStats

    [Fact]
    public void QdrantSkillRegistryStats_Creation_ShouldSetProperties()
    {
        // Act
        var stats = new QdrantSkillRegistryStats(5, 0.8, 100, "skill-a", "skill-b", "conn", "coll", true);

        // Assert
        stats.TotalSkills.Should().Be(5);
        stats.AverageSuccessRate.Should().Be(0.8);
        stats.TotalExecutions.Should().Be(100);
        stats.MostUsedSkill.Should().Be("skill-a");
        stats.MostSuccessfulSkill.Should().Be("skill-b");
        stats.ConnectionString.Should().Be("conn");
        stats.CollectionName.Should().Be("coll");
        stats.IsConnected.Should().BeTrue();
    }

    #endregion

    #region ReviewComment

    [Fact]
    public void ReviewComment_Creation_ShouldSetProperties()
    {
        // Act
        var rc = new ReviewComment("c1", "reviewer-1", "Looks good", ReviewCommentStatus.Open, DateTime.UtcNow);

        // Assert
        rc.CommentId.Should().Be("c1");
        rc.ReviewerId.Should().Be("reviewer-1");
        rc.Content.Should().Be("Looks good");
        rc.Status.Should().Be(ReviewCommentStatus.Open);
        rc.ResolvedAt.Should().BeNull();
    }

    [Fact]
    public void ReviewComment_Creation_WithResolvedAt()
    {
        // Arrange
        var resolved = DateTime.UtcNow;

        // Act
        var rc = new ReviewComment("c1", "reviewer-1", "Looks good", ReviewCommentStatus.Resolved, DateTime.UtcNow, resolved);

        // Assert
        rc.ResolvedAt.Should().Be(resolved);
    }

    #endregion

    #region ReviewDecision

    [Fact]
    public void ReviewDecision_Creation_ShouldSetProperties()
    {
        // Arrange
        var comments = new List<ReviewComment>();

        // Act
        var rd = new ReviewDecision("reviewer-1", true, "Great work", comments, DateTime.UtcNow);

        // Assert
        rd.ReviewerId.Should().Be("reviewer-1");
        rd.Approved.Should().BeTrue();
        rd.Feedback.Should().Be("Great work");
        rd.Comments.Should().BeEquivalentTo(comments);
    }

    #endregion

    #region ReviewState

    [Fact]
    public void ReviewState_Creation_ShouldSetProperties()
    {
        // Arrange
        var pr = new PullRequest("pr-1", "Title", "Desc", "spec", new List<string>(), DateTime.UtcNow);
        var reviews = new List<ReviewDecision>();
        var comments = new List<ReviewComment>();

        // Act
        var rs = new ReviewState(pr, reviews, comments, ReviewStatus.Draft, DateTime.UtcNow);

        // Assert
        rs.PR.Should().Be(pr);
        rs.Reviews.Should().BeEquivalentTo(reviews);
        rs.AllComments.Should().BeEquivalentTo(comments);
        rs.Status.Should().Be(ReviewStatus.Draft);
    }

    #endregion

    #region ScheduledTask

    [Fact]
    public void ScheduledTask_Creation_ShouldSetProperties()
    {
        // Arrange
        var start = DateTime.UtcNow;
        var end = start.AddHours(1);
        var deps = new List<string> { "dep-1" };

        // Act
        var st = new ScheduledTask("task-1", start, end, deps);

        // Assert
        st.Name.Should().Be("task-1");
        st.StartTime.Should().Be(start);
        st.EndTime.Should().Be(end);
        st.Dependencies.Should().BeEquivalentTo(deps);
    }

    #endregion

    #region SkillRegistryStats

    [Fact]
    public void SkillRegistryStats_Creation_ShouldSetProperties()
    {
        // Act
        var stats = new SkillRegistryStats(10, 0.75, 500, "most-used", "best", "/path", true);

        // Assert
        stats.TotalSkills.Should().Be(10);
        stats.AverageSuccessRate.Should().Be(0.75);
        stats.TotalExecutions.Should().Be(500);
        stats.MostUsedSkill.Should().Be("most-used");
        stats.MostSuccessfulSkill.Should().Be("best");
        stats.StoragePath.Should().Be("/path");
        stats.IsPersisted.Should().BeTrue();
    }

    #endregion

    #region SkillSuggestion

    [Fact]
    public void SkillSuggestion_Creation_ShouldSetProperties()
    {
        // Arrange
        var skill = new Skill("name", "desc", new List<string>(), new List<PlanStep>(), 0.8, 5, DateTime.UtcNow, DateTime.UtcNow);

        // Act
        var sug = new SkillSuggestion("UseSkill_name", skill, 0.85, "example");

        // Assert
        sug.TokenName.Should().Be("UseSkill_name");
        sug.Skill.Should().Be(skill);
        sug.RelevanceScore.Should().Be(0.85);
        sug.UsageExample.Should().Be("example");
    }

    #endregion

    #region SkillCompositionConfig

    [Fact]
    public void SkillCompositionConfig_DefaultCreation_ShouldSetDefaults()
    {
        // Act
        var config = new SkillCompositionConfig();

        // Assert
        config.MaxComponentSkills.Should().Be(5);
        config.MinComponentQuality.Should().Be(0.7);
        config.AllowRecursiveComposition.Should().BeFalse();
    }

    [Fact]
    public void SkillCompositionConfig_CustomCreation_ShouldSetValues()
    {
        // Act
        var config = new SkillCompositionConfig(3, 0.8, true);

        // Assert
        config.MaxComponentSkills.Should().Be(3);
        config.MinComponentQuality.Should().Be(0.8);
        config.AllowRecursiveComposition.Should().BeTrue();
    }

    #endregion

    #region StakeholderReviewConfig

    [Fact]
    public void StakeholderReviewConfig_DefaultCreation_ShouldSetDefaults()
    {
        // Act
        var config = new StakeholderReviewConfig();

        // Assert
        config.MinimumRequiredApprovals.Should().Be(2);
        config.RequireAllReviewersApprove.Should().BeTrue();
        config.AutoResolveNonBlockingComments.Should().BeFalse();
        config.ReviewTimeout.Should().Be(default);
        config.PollingInterval.Should().Be(default);
    }

    [Fact]
    public void StakeholderReviewConfig_CustomCreation_ShouldSetValues()
    {
        // Act
        var config = new StakeholderReviewConfig(1, false, true, TimeSpan.FromHours(12), TimeSpan.FromMinutes(2));

        // Assert
        config.MinimumRequiredApprovals.Should().Be(1);
        config.RequireAllReviewersApprove.Should().BeFalse();
        config.AutoResolveNonBlockingComments.Should().BeTrue();
        config.ReviewTimeout.Should().Be(TimeSpan.FromHours(12));
        config.PollingInterval.Should().Be(TimeSpan.FromMinutes(2));
    }

    #endregion

    #region StakeholderReviewResult

    [Fact]
    public void StakeholderReviewResult_Creation_ShouldSetProperties()
    {
        // Arrange
        var pr = new PullRequest("pr-1", "Title", "Desc", "spec", new List<string>(), DateTime.UtcNow);
        var state = new ReviewState(pr, new List<ReviewDecision>(), new List<ReviewComment>(), ReviewStatus.Merged, DateTime.UtcNow);

        // Act
        var result = new StakeholderReviewResult(state, true, 3, 3, 2, 0, TimeSpan.FromMinutes(10), "success");

        // Assert
        result.FinalState.Should().Be(state);
        result.AllApproved.Should().BeTrue();
        result.TotalReviewers.Should().Be(3);
        result.ApprovedCount.Should().Be(3);
        result.CommentsResolved.Should().Be(2);
        result.CommentsRemaining.Should().Be(0);
        result.Duration.Should().Be(TimeSpan.FromMinutes(10));
        result.Summary.Should().Be("success");
    }

    #endregion

    #region StatisticalAnalysis

    [Fact]
    public void StatisticalAnalysis_Creation_ShouldSetProperties()
    {
        // Act
        var sa = new StatisticalAnalysis(0.5, true, "Medium difference");

        // Assert
        sa.EffectSize.Should().Be(0.5);
        sa.IsSignificant.Should().BeTrue();
        sa.Interpretation.Should().Be("Medium difference");
    }

    #endregion

    #region TaskAssignment

    [Fact]
    public void TaskAssignment_Creation_ShouldSetProperties()
    {
        // Arrange
        var step = new PlanStep("action", new Dictionary<string, object>(), "outcome", 0.8);
        var assigned = DateTime.UtcNow;

        // Act
        var ta = new TaskAssignment("task-1", "agent-1", step, assigned, TaskAssignmentStatus.Pending);

        // Assert
        ta.TaskId.Should().Be("task-1");
        ta.AgentId.Should().Be("agent-1");
        ta.Step.Should().Be(step);
        ta.AssignedAt.Should().Be(assigned);
        ta.Status.Should().Be(TaskAssignmentStatus.Pending);
    }

    #endregion

    #region TaskDecomposition

    [Fact]
    public void TaskDecomposition_Creation_ShouldSetProperties()
    {
        // Arrange
        var subtasks = new List<string> { "sub-1", "sub-2" };
        var constraints = new List<string> { "sub-1 before sub-2" };

        // Act
        var td = new TaskDecomposition("main", subtasks, constraints);

        // Assert
        td.AbstractTask.Should().Be("main");
        td.SubTasks.Should().BeEquivalentTo(subtasks);
        td.OrderingConstraints.Should().BeEquivalentTo(constraints);
    }

    #endregion

    #region TemporalConstraint

    [Fact]
    public void TemporalConstraint_Creation_ShouldSetProperties()
    {
        // Act
        var tc = new TemporalConstraint("task-a", "task-b", TemporalRelation.Before, TimeSpan.FromMinutes(5));

        // Assert
        tc.TaskA.Should().Be("task-a");
        tc.TaskB.Should().Be("task-b");
        tc.Relation.Should().Be(TemporalRelation.Before);
        tc.Duration.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void TemporalConstraint_Creation_WithoutDuration()
    {
        // Act
        var tc = new TemporalConstraint("task-a", "task-b", TemporalRelation.After);

        // Assert
        tc.Duration.Should().BeNull();
    }

    #endregion

    #region TemporalPlan

    [Fact]
    public void TemporalPlan_Creation_ShouldSetProperties()
    {
        // Arrange
        var tasks = new List<ScheduledTask>();

        // Act
        var tp = new TemporalPlan("goal", tasks, TimeSpan.FromHours(2));

        // Assert
        tp.Goal.Should().Be("goal");
        tp.Tasks.Should().BeEquivalentTo(tasks);
        tp.TotalDuration.Should().Be(TimeSpan.FromHours(2));
    }

    #endregion

    #region TestCase

    [Fact]
    public void TestCase_Creation_WithNullContextAndValidator()
    {
        // Act
        var tc = new TestCase("test-1", "goal", null, null);

        // Assert
        tc.Name.Should().Be("test-1");
        tc.Goal.Should().Be("goal");
        tc.Context.Should().BeNull();
        tc.CustomValidator.Should().BeNull();
    }

    #endregion

    #region ToolRecommendation

    [Fact]
    public void ToolRecommendation_Creation_ShouldSetProperties()
    {
        // Act
        var tr = new ToolRecommendation("tool-1", "Desc", 0.85, ToolCategory.Code);

        // Assert
        tr.ToolName.Should().Be("tool-1");
        tr.Description.Should().Be("Desc");
        tr.RelevanceScore.Should().Be(0.85);
        tr.Category.Should().Be(ToolCategory.Code);
    }

    [Fact]
    public void ToolRecommendation_IsHighlyRecommended_True_WhenAboveThreshold()
    {
        // Act
        var tr = new ToolRecommendation("tool-1", "Desc", 0.85, ToolCategory.Code);

        // Assert
        tr.IsHighlyRecommended.Should().BeTrue();
        tr.IsRecommended.Should().BeTrue();
    }

    [Fact]
    public void ToolRecommendation_IsHighlyRecommended_False_WhenBelowThreshold()
    {
        // Act
        var tr = new ToolRecommendation("tool-1", "Desc", 0.5, ToolCategory.Code);

        // Assert
        tr.IsHighlyRecommended.Should().BeFalse();
        tr.IsRecommended.Should().BeTrue();
    }

    [Fact]
    public void ToolRecommendation_IsRecommended_False_WhenBelowThreshold()
    {
        // Act
        var tr = new ToolRecommendation("tool-1", "Desc", 0.3, ToolCategory.Code);

        // Assert
        tr.IsRecommended.Should().BeFalse();
    }

    #endregion

    #region ToolSelection

    [Fact]
    public void ToolSelection_Creation_ShouldSetProperties()
    {
        // Act
        var ts = new ToolSelection("tool-1", "{\"arg\":1}");

        // Assert
        ts.ToolName.Should().Be("tool-1");
        ts.ArgumentsJson.Should().Be("{\"arg\":1}");
    }

    #endregion

    #region ToolSelectionContext

    [Fact]
    public void ToolSelectionContext_Creation_ShouldSetDefaults()
    {
        // Act
        var tsc = new ToolSelectionContext();

        // Assert
        tsc.MaxTools.Should().BeNull();
        tsc.RequiredCategories.Should().BeNull();
        tsc.ExcludedCategories.Should().BeNull();
        tsc.RequiredToolNames.Should().BeNull();
        tsc.PreferFastTools.Should().BeFalse();
        tsc.PreferReliableTools.Should().BeFalse();
    }

    [Fact]
    public void ToolSelectionContext_Creation_WithValues()
    {
        // Act
        var tsc = new ToolSelectionContext
        {
            MaxTools = 3,
            RequiredCategories = new List<ToolCategory> { ToolCategory.Code },
            ExcludedCategories = new List<ToolCategory> { ToolCategory.Creative },
            RequiredToolNames = new List<string> { "tool-1" },
            PreferFastTools = true,
            PreferReliableTools = true
        };

        // Assert
        tsc.MaxTools.Should().Be(3);
        tsc.RequiredCategories.Should().ContainSingle().Which.Should().Be(ToolCategory.Code);
        tsc.ExcludedCategories.Should().ContainSingle().Which.Should().Be(ToolCategory.Creative);
        tsc.RequiredToolNames.Should().ContainSingle().Which.Should().Be("tool-1");
        tsc.PreferFastTools.Should().BeTrue();
        tsc.PreferReliableTools.Should().BeTrue();
    }

    #endregion

    #region TrainingBatch

    [Fact]
    public void TrainingBatch_Creation_ShouldSetProperties()
    {
        // Arrange
        var experiences = new List<Experience>();
        var metrics = new Dictionary<string, double> { ["accuracy"] = 0.9 };
        var created = DateTime.UtcNow;

        // Act
        var tb = new TrainingBatch(experiences, metrics, created);

        // Assert
        tb.Experiences.Should().BeEquivalentTo(experiences);
        tb.Metrics.Should().ContainKey("accuracy");
        tb.CreatedAt.Should().Be(created);
    }

    #endregion

    #region TrainingResult

    [Fact]
    public void TrainingResult_Creation_ShouldSetProperties()
    {
        // Arrange
        var metrics = new Dictionary<string, double> { ["accuracy"] = 0.95 };
        var patterns = new List<string> { "pattern-1" };

        // Act
        var tr = new TrainingResult(10, metrics, patterns, true);

        // Assert
        tr.ExperiencesProcessed.Should().Be(10);
        tr.ImprovedMetrics.Should().ContainKey("accuracy");
        tr.LearnedPatterns.Should().BeEquivalentTo(patterns);
        tr.Success.Should().BeTrue();
    }

    #endregion

    #region VariantMetrics

    [Fact]
    public void VariantMetrics_Creation_ShouldSetProperties()
    {
        // Act
        var vm = new VariantMetrics(0.9, 100.0, 150.0, 200.0, 0.85, 10, 9);

        // Assert
        vm.SuccessRate.Should().Be(0.9);
        vm.AverageLatencyMs.Should().Be(100.0);
        vm.P95LatencyMs.Should().Be(150.0);
        vm.P99LatencyMs.Should().Be(200.0);
        vm.AverageConfidence.Should().Be(0.85);
        vm.TotalPrompts.Should().Be(10);
        vm.SuccessfulPrompts.Should().Be(9);
    }

    #endregion

    #region VariantResult

    [Fact]
    public void VariantResult_Creation_ShouldSetProperties()
    {
        // Arrange
        var prompts = new List<PromptResult>();
        var metrics = new VariantMetrics(0.9, 100.0, 150.0, 200.0, 0.85, 10, 9);

        // Act
        var vr = new VariantResult("variant-1", prompts, metrics);

        // Assert
        vr.VariantId.Should().Be("variant-1");
        vr.PromptResults.Should().BeEquivalentTo(prompts);
        vr.Metrics.Should().Be(metrics);
    }

    #endregion

    #region SubIssueAssignment

    [Fact]
    public void SubIssueAssignment_Creation_ShouldSetProperties()
    {
        // Act
        var sia = new SubIssueAssignment(
            1, "Title", "Desc", "agent-1", "branch-1", null, SubIssueStatus.Pending, DateTime.UtcNow);

        // Assert
        sia.IssueNumber.Should().Be(1);
        sia.Title.Should().Be("Title");
        sia.Description.Should().Be("Desc");
        sia.AssignedAgentId.Should().Be("agent-1");
        sia.BranchName.Should().Be("branch-1");
        sia.Branch.Should().BeNull();
        sia.Status.Should().Be(SubIssueStatus.Pending);
        sia.CompletedAt.Should().BeNull();
        sia.ErrorMessage.Should().BeNull();
    }

    #endregion

    #region OrchestrationObservabilityConfig

    [Fact]
    public void OrchestrationObservabilityConfig_DefaultCreation_ShouldSetDefaults()
    {
        // Act
        var config = new OrchestrationObservabilityConfig();

        // Assert
        config.EnableTracing.Should().BeTrue();
        config.EnableMetrics.Should().BeTrue();
        config.EnableDetailedTags.Should().BeFalse();
        config.SamplingRate.Should().Be(1.0);
    }

    [Fact]
    public void OrchestrationObservabilityConfig_CustomCreation_ShouldSetValues()
    {
        // Act
        var config = new OrchestrationObservabilityConfig(false, false, true, 0.5);

        // Assert
        config.EnableTracing.Should().BeFalse();
        config.EnableMetrics.Should().BeFalse();
        config.EnableDetailedTags.Should().BeTrue();
        config.SamplingRate.Should().Be(0.5);
    }

    #endregion

    #region PersistentMetricsConfig

    [Fact]
    public void PersistentMetricsConfig_DefaultCreation_ShouldSetDefaults()
    {
        // Act
        var config = new PersistentMetricsConfig();

        // Assert
        config.StoragePath.Should().Be("metrics");
        config.FileName.Should().Be("performance_metrics.json");
        config.AutoSave.Should().BeTrue();
        config.AutoSaveInterval.Should().Be(default);
        config.MaxMetricsAge.Should().Be(90);
    }

    #endregion

    #region PersistentSkillConfig

    [Fact]
    public void PersistentSkillConfig_DefaultCreation_ShouldSetDefaults()
    {
        // Act
        var config = new PersistentSkillConfig();

        // Assert
        config.StoragePath.Should().Be("skills.json");
        config.UseVectorStore.Should().BeTrue();
        config.CollectionName.Should().Be("ouroboros_skills");
        config.AutoSave.Should().BeTrue();
    }

    #endregion

    #region Enums

    [Theory]
    [InlineData(OuroborosConfidence.High)]
    [InlineData(OuroborosConfidence.Medium)]
    [InlineData(OuroborosConfidence.Low)]
    public void OuroborosConfidence_AllValues_AreDefined(OuroborosConfidence value)
    {
        // Assert
        ((int)value).Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public void OuroborosConfidence_HasThreeValues()
    {
        // Assert
        Enum.GetValues<OuroborosConfidence>().Should().HaveCount(3);
    }

    [Theory]
    [InlineData(RepairStrategy.Replan)]
    [InlineData(RepairStrategy.Patch)]
    [InlineData(RepairStrategy.CaseBased)]
    [InlineData(RepairStrategy.Backtrack)]
    public void RepairStrategy_AllValues_AreDefined(RepairStrategy value)
    {
        ((int)value).Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public void RepairStrategy_HasFourValues()
    {
        Enum.GetValues<RepairStrategy>().Should().HaveCount(4);
    }

    [Theory]
    [InlineData(ReviewCommentStatus.Open)]
    [InlineData(ReviewCommentStatus.Resolved)]
    [InlineData(ReviewCommentStatus.Dismissed)]
    public void ReviewCommentStatus_AllValues_AreDefined(ReviewCommentStatus value)
    {
        ((int)value).Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public void ReviewCommentStatus_HasThreeValues()
    {
        Enum.GetValues<ReviewCommentStatus>().Should().HaveCount(3);
    }

    [Theory]
    [InlineData(ReviewStatus.Draft)]
    [InlineData(ReviewStatus.AwaitingReview)]
    [InlineData(ReviewStatus.ChangesRequested)]
    [InlineData(ReviewStatus.Approved)]
    [InlineData(ReviewStatus.Merged)]
    public void ReviewStatus_AllValues_AreDefined(ReviewStatus value)
    {
        ((int)value).Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public void ReviewStatus_HasFiveValues()
    {
        Enum.GetValues<ReviewStatus>().Should().HaveCount(5);
    }

    [Theory]
    [InlineData(SafetyConstraints.None)]
    [InlineData(SafetyConstraints.NoSelfDestruction)]
    [InlineData(SafetyConstraints.PreserveHumanOversight)]
    [InlineData(SafetyConstraints.BoundedResourceUse)]
    [InlineData(SafetyConstraints.ReversibleActions)]
    [InlineData(SafetyConstraints.All)]
    public void SafetyConstraints_AllValues_AreDefined(SafetyConstraints value)
    {
        ((int)value).Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public void SafetyConstraints_All_IsCombinationOfFlags()
    {
        // Assert
        SafetyConstraints.All.Should().Be(
            SafetyConstraints.NoSelfDestruction |
            SafetyConstraints.PreserveHumanOversight |
            SafetyConstraints.BoundedResourceUse |
            SafetyConstraints.ReversibleActions);
    }

    [Fact]
    public void SafetyConstraints_Flags_CanBeCombined()
    {
        // Arrange
        var combined = SafetyConstraints.NoSelfDestruction | SafetyConstraints.PreserveHumanOversight;

        // Assert
        combined.Should().HaveFlag(SafetyConstraints.NoSelfDestruction);
        combined.Should().HaveFlag(SafetyConstraints.PreserveHumanOversight);
        combined.Should().NotHaveFlag(SafetyConstraints.BoundedResourceUse);
    }

    [Theory]
    [InlineData(SubIssueStatus.Pending)]
    [InlineData(SubIssueStatus.BranchCreated)]
    [InlineData(SubIssueStatus.InProgress)]
    [InlineData(SubIssueStatus.Completed)]
    [InlineData(SubIssueStatus.Failed)]
    public void SubIssueStatus_AllValues_AreDefined(SubIssueStatus value)
    {
        ((int)value).Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public void SubIssueStatus_HasFiveValues()
    {
        Enum.GetValues<SubIssueStatus>().Should().HaveCount(5);
    }

    [Theory]
    [InlineData(TaskAssignmentStatus.Pending)]
    [InlineData(TaskAssignmentStatus.InProgress)]
    [InlineData(TaskAssignmentStatus.Completed)]
    [InlineData(TaskAssignmentStatus.Failed)]
    public void TaskAssignmentStatus_AllValues_AreDefined(TaskAssignmentStatus value)
    {
        ((int)value).Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public void TaskAssignmentStatus_HasFourValues()
    {
        Enum.GetValues<TaskAssignmentStatus>().Should().HaveCount(4);
    }

    [Theory]
    [InlineData(TemporalRelation.Before)]
    [InlineData(TemporalRelation.After)]
    [InlineData(TemporalRelation.During)]
    [InlineData(TemporalRelation.Overlaps)]
    [InlineData(TemporalRelation.MustFinishBefore)]
    [InlineData(TemporalRelation.Simultaneous)]
    public void TemporalRelation_AllValues_AreDefined(TemporalRelation value)
    {
        ((int)value).Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public void TemporalRelation_HasSixValues()
    {
        Enum.GetValues<TemporalRelation>().Should().HaveCount(6);
    }

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
    public void ToolCategory_AllValues_AreDefined(ToolCategory value)
    {
        ((int)value).Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public void ToolCategory_HasElevenValues()
    {
        Enum.GetValues<ToolCategory>().Should().HaveCount(11);
    }

    #endregion
}
