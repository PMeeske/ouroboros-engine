using FluentAssertions;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Providers.Configuration;
using Xunit;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class AdaptivePlanningConfigTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var config = new AdaptivePlanningConfig();

        config.MaxRetries.Should().Be(3);
        config.EnableAutoReplan.Should().BeTrue();
        config.FailureThreshold.Should().Be(0.5);
    }

    [Fact]
    public void CustomValues_ShouldOverrideDefaults()
    {
        var config = new AdaptivePlanningConfig(
            MaxRetries: 5,
            EnableAutoReplan: false,
            FailureThreshold: 0.8);

        config.MaxRetries.Should().Be(5);
        config.EnableAutoReplan.Should().BeFalse();
        config.FailureThreshold.Should().Be(0.8);
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        var a = new AdaptivePlanningConfig(3, true, 0.5);
        var b = new AdaptivePlanningConfig(3, true, 0.5);
        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentValues_ShouldNotBeEqual()
    {
        var a = new AdaptivePlanningConfig(3, true, 0.5);
        var b = new AdaptivePlanningConfig(5, true, 0.5);
        a.Should().NotBe(b);
    }
}

[Trait("Category", "Unit")]
public class CostAwareRoutingConfigTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var config = new CostAwareRoutingConfig();

        config.MaxCostPerPlan.Should().Be(1.0);
        config.MinAcceptableQuality.Should().Be(0.7);
        config.Strategy.Should().Be(CostOptimizationStrategy.Balanced);
    }

    [Fact]
    public void CustomValues_ShouldOverrideDefaults()
    {
        var config = new CostAwareRoutingConfig(
            MaxCostPerPlan: 5.0,
            MinAcceptableQuality: 0.9,
            Strategy: CostOptimizationStrategy.MinimizeCost);

        config.MaxCostPerPlan.Should().Be(5.0);
        config.MinAcceptableQuality.Should().Be(0.9);
        config.Strategy.Should().Be(CostOptimizationStrategy.MinimizeCost);
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        var a = new CostAwareRoutingConfig(1.0, 0.7, CostOptimizationStrategy.Balanced);
        var b = new CostAwareRoutingConfig(1.0, 0.7, CostOptimizationStrategy.Balanced);
        a.Should().Be(b);
    }
}

[Trait("Category", "Unit")]
public class DistributedOrchestrationConfigTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var config = new DistributedOrchestrationConfig();

        config.MaxAgents.Should().Be(10);
        config.HeartbeatTimeout.Should().Be(default(TimeSpan));
        config.EnableLoadBalancing.Should().BeTrue();
    }

    [Fact]
    public void CustomValues_ShouldOverrideDefaults()
    {
        var timeout = TimeSpan.FromSeconds(30);
        var config = new DistributedOrchestrationConfig(
            MaxAgents: 20,
            HeartbeatTimeout: timeout,
            EnableLoadBalancing: false);

        config.MaxAgents.Should().Be(20);
        config.HeartbeatTimeout.Should().Be(timeout);
        config.EnableLoadBalancing.Should().BeFalse();
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        var a = new DistributedOrchestrationConfig(10, default, true);
        var b = new DistributedOrchestrationConfig(10, default, true);
        a.Should().Be(b);
    }
}

[Trait("Category", "Unit")]
public class EpicBranchConfigTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var config = new EpicBranchConfig();

        config.BranchPrefix.Should().Be("epic");
        config.AgentPoolPrefix.Should().Be("sub-issue-agent");
        config.AutoCreateBranches.Should().BeTrue();
        config.AutoAssignAgents.Should().BeTrue();
        config.MaxConcurrentSubIssues.Should().Be(5);
    }

    [Fact]
    public void CustomValues_ShouldOverrideDefaults()
    {
        var config = new EpicBranchConfig(
            BranchPrefix: "feature",
            AgentPoolPrefix: "worker",
            AutoCreateBranches: false,
            AutoAssignAgents: false,
            MaxConcurrentSubIssues: 10);

        config.BranchPrefix.Should().Be("feature");
        config.AgentPoolPrefix.Should().Be("worker");
        config.AutoCreateBranches.Should().BeFalse();
        config.AutoAssignAgents.Should().BeFalse();
        config.MaxConcurrentSubIssues.Should().Be(10);
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        var a = new EpicBranchConfig("epic", "sub-issue-agent", true, true, 5);
        var b = new EpicBranchConfig("epic", "sub-issue-agent", true, true, 5);
        a.Should().Be(b);
    }
}

[Trait("Category", "Unit")]
public class HierarchicalPlanningConfigTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var config = new HierarchicalPlanningConfig();

        config.MaxDepth.Should().Be(3);
        config.MinStepsForDecomposition.Should().Be(3);
        config.ComplexityThreshold.Should().Be(0.7);
    }

    [Fact]
    public void CustomValues_ShouldOverrideDefaults()
    {
        var config = new HierarchicalPlanningConfig(
            MaxDepth: 5,
            MinStepsForDecomposition: 2,
            ComplexityThreshold: 0.9);

        config.MaxDepth.Should().Be(5);
        config.MinStepsForDecomposition.Should().Be(2);
        config.ComplexityThreshold.Should().Be(0.9);
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        var a = new HierarchicalPlanningConfig(3, 3, 0.7);
        var b = new HierarchicalPlanningConfig(3, 3, 0.7);
        a.Should().Be(b);
    }
}

[Trait("Category", "Unit")]
public class HumanInTheLoopConfigTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var config = new HumanInTheLoopConfig();

        config.RequireApprovalForCriticalSteps.Should().BeTrue();
        config.EnableInteractiveRefinement.Should().BeTrue();
        config.DefaultTimeout.Should().Be(default(TimeSpan));
    }

    [Fact]
    public void CustomValues_ShouldOverrideDefaults()
    {
        var patterns = new List<string> { "delete*", "deploy*" };
        var timeout = TimeSpan.FromMinutes(5);
        var config = new HumanInTheLoopConfig(
            RequireApprovalForCriticalSteps: false,
            EnableInteractiveRefinement: false,
            DefaultTimeout: timeout,
            CriticalActionPatterns: patterns);

        config.RequireApprovalForCriticalSteps.Should().BeFalse();
        config.EnableInteractiveRefinement.Should().BeFalse();
        config.DefaultTimeout.Should().Be(timeout);
        config.CriticalActionPatterns.Should().BeEquivalentTo(patterns);
    }
}

[Trait("Category", "Unit")]
public class OrchestrationObservabilityConfigTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var config = new OrchestrationObservabilityConfig();

        config.EnableTracing.Should().BeTrue();
        config.EnableMetrics.Should().BeTrue();
        config.EnableDetailedTags.Should().BeFalse();
        config.SamplingRate.Should().Be(1.0);
    }

    [Fact]
    public void CustomValues_ShouldOverrideDefaults()
    {
        var config = new OrchestrationObservabilityConfig(
            EnableTracing: false,
            EnableMetrics: false,
            EnableDetailedTags: true,
            SamplingRate: 0.5);

        config.EnableTracing.Should().BeFalse();
        config.EnableMetrics.Should().BeFalse();
        config.EnableDetailedTags.Should().BeTrue();
        config.SamplingRate.Should().Be(0.5);
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        var a = new OrchestrationObservabilityConfig(true, true, false, 1.0);
        var b = new OrchestrationObservabilityConfig(true, true, false, 1.0);
        a.Should().Be(b);
    }
}

[Trait("Category", "Unit")]
public class PersistentMetricsConfigTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var config = new PersistentMetricsConfig();

        config.StoragePath.Should().Be("metrics");
        config.FileName.Should().Be("performance_metrics.json");
        config.AutoSave.Should().BeTrue();
        config.AutoSaveInterval.Should().Be(default(TimeSpan));
        config.MaxMetricsAge.Should().Be(90);
    }

    [Fact]
    public void CustomValues_ShouldOverrideDefaults()
    {
        var interval = TimeSpan.FromMinutes(10);
        var config = new PersistentMetricsConfig(
            StoragePath: "/data/metrics",
            FileName: "custom_metrics.json",
            AutoSave: false,
            AutoSaveInterval: interval,
            MaxMetricsAge: 30);

        config.StoragePath.Should().Be("/data/metrics");
        config.FileName.Should().Be("custom_metrics.json");
        config.AutoSave.Should().BeFalse();
        config.AutoSaveInterval.Should().Be(interval);
        config.MaxMetricsAge.Should().Be(30);
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        var a = new PersistentMetricsConfig("metrics", "m.json", true, default, 90);
        var b = new PersistentMetricsConfig("metrics", "m.json", true, default, 90);
        a.Should().Be(b);
    }
}

[Trait("Category", "Unit")]
public class PersistentSkillConfigTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var config = new PersistentSkillConfig();

        config.StoragePath.Should().Be("skills.json");
        config.UseVectorStore.Should().BeTrue();
        config.CollectionName.Should().Be("ouroboros_skills");
        config.AutoSave.Should().BeTrue();
    }

    [Fact]
    public void CustomValues_ShouldOverrideDefaults()
    {
        var config = new PersistentSkillConfig(
            StoragePath: "/data/skills.json",
            UseVectorStore: false,
            CollectionName: "custom_skills",
            AutoSave: false);

        config.StoragePath.Should().Be("/data/skills.json");
        config.UseVectorStore.Should().BeFalse();
        config.CollectionName.Should().Be("custom_skills");
        config.AutoSave.Should().BeFalse();
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        var a = new PersistentSkillConfig("skills.json", true, "ouroboros_skills", true);
        var b = new PersistentSkillConfig("skills.json", true, "ouroboros_skills", true);
        a.Should().Be(b);
    }
}

[Trait("Category", "Unit")]
public class SkillCompositionConfigTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var config = new SkillCompositionConfig();

        config.MaxComponentSkills.Should().Be(5);
        config.MinComponentQuality.Should().Be(0.7);
        config.AllowRecursiveComposition.Should().BeFalse();
    }

    [Fact]
    public void CustomValues_ShouldOverrideDefaults()
    {
        var config = new SkillCompositionConfig(
            MaxComponentSkills: 10,
            MinComponentQuality: 0.9,
            AllowRecursiveComposition: true);

        config.MaxComponentSkills.Should().Be(10);
        config.MinComponentQuality.Should().Be(0.9);
        config.AllowRecursiveComposition.Should().BeTrue();
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        var a = new SkillCompositionConfig(5, 0.7, false);
        var b = new SkillCompositionConfig(5, 0.7, false);
        a.Should().Be(b);
    }
}

[Trait("Category", "Unit")]
public class StakeholderReviewConfigTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var config = new StakeholderReviewConfig();

        config.MinimumRequiredApprovals.Should().Be(2);
        config.RequireAllReviewersApprove.Should().BeTrue();
        config.AutoResolveNonBlockingComments.Should().BeFalse();
        config.ReviewTimeout.Should().Be(default(TimeSpan));
        config.PollingInterval.Should().Be(default(TimeSpan));
    }

    [Fact]
    public void CustomValues_ShouldOverrideDefaults()
    {
        var reviewTimeout = TimeSpan.FromHours(24);
        var pollingInterval = TimeSpan.FromMinutes(5);
        var config = new StakeholderReviewConfig(
            MinimumRequiredApprovals: 3,
            RequireAllReviewersApprove: false,
            AutoResolveNonBlockingComments: true,
            ReviewTimeout: reviewTimeout,
            PollingInterval: pollingInterval);

        config.MinimumRequiredApprovals.Should().Be(3);
        config.RequireAllReviewersApprove.Should().BeFalse();
        config.AutoResolveNonBlockingComments.Should().BeTrue();
        config.ReviewTimeout.Should().Be(reviewTimeout);
        config.PollingInterval.Should().Be(pollingInterval);
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        var a = new StakeholderReviewConfig(2, true, false, default, default);
        var b = new StakeholderReviewConfig(2, true, false, default, default);
        a.Should().Be(b);
    }
}

#pragma warning disable CS0618 // Obsolete type under test
[Trait("Category", "Unit")]
public class QdrantSkillConfigTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var config = new QdrantSkillConfig();

        config.ConnectionString.Should().Be(DefaultEndpoints.QdrantGrpc);
        config.CollectionName.Should().Be("ouroboros_skills");
        config.AutoSave.Should().BeTrue();
        config.VectorSize.Should().Be(1536);
    }

    [Fact]
    public void CustomValues_ShouldOverrideDefaults()
    {
        var config = new QdrantSkillConfig(
            ConnectionString: "http://custom:6334",
            CollectionName: "custom_skills",
            AutoSave: false,
            VectorSize: 768);

        config.ConnectionString.Should().Be("http://custom:6334");
        config.CollectionName.Should().Be("custom_skills");
        config.AutoSave.Should().BeFalse();
        config.VectorSize.Should().Be(768);
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        var a = new QdrantSkillConfig("conn", "coll", true, 1536);
        var b = new QdrantSkillConfig("conn", "coll", true, 1536);
        a.Should().Be(b);
    }
}
#pragma warning restore CS0618
