using FluentAssertions;
using Ouroboros.Agent.MetaAI;
using Xunit;
using MetaAgentStatus = Ouroboros.Agent.MetaAI.AgentStatus;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class AgentInfoTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var capabilities = new HashSet<string> { "coding", "review" };
        var now = DateTime.UtcNow;
        var info = new AgentInfo("agent-1", "Coder", capabilities, MetaAgentStatus.Available, now);

        info.AgentId.Should().Be("agent-1");
        info.Name.Should().Be("Coder");
        info.Capabilities.Should().HaveCount(2);
        info.Status.Should().Be(MetaAgentStatus.Available);
        info.LastHeartbeat.Should().Be(now);
    }

    [Fact]
    public void Record_Equality_SameCapabilities_ShouldBeEqual()
    {
        var caps = new HashSet<string> { "a" };
        var now = DateTime.UtcNow;
        var a = new AgentInfo("1", "n", caps, MetaAgentStatus.Busy, now);
        var b = new AgentInfo("1", "n", caps, MetaAgentStatus.Busy, now);
        a.Should().Be(b);
    }
}

[Trait("Category", "Unit")]
public class CostInfoTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var cost = new CostInfo("gpt-4", 0.03, 0.01, 0.95);

        cost.ResourceId.Should().Be("gpt-4");
        cost.CostPerToken.Should().Be(0.03);
        cost.CostPerRequest.Should().Be(0.01);
        cost.EstimatedQuality.Should().Be(0.95);
    }

    [Fact]
    public void Record_Equality_ShouldWorkByValue()
    {
        var a = new CostInfo("r1", 0.01, 0.005, 0.8);
        var b = new CostInfo("r1", 0.01, 0.005, 0.8);
        a.Should().Be(b);
    }
}

[Trait("Category", "Unit")]
public class CostBenefitAnalysisTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var analysis = new CostBenefitAnalysis(
            RecommendedRoute: "gpt-4-turbo",
            EstimatedCost: 0.05,
            EstimatedQuality: 0.92,
            ValueScore: 18.4,
            Rationale: "Best quality per dollar");

        analysis.RecommendedRoute.Should().Be("gpt-4-turbo");
        analysis.EstimatedCost.Should().Be(0.05);
        analysis.EstimatedQuality.Should().Be(0.92);
        analysis.ValueScore.Should().Be(18.4);
        analysis.Rationale.Should().Be("Best quality per dollar");
    }
}

[Trait("Category", "Unit")]
public class CacheStatisticsTests
{
    [Fact]
    public void UtilizationPercent_ShouldCalculateCorrectly()
    {
        var stats = new CacheStatistics(50, 100, 200, 50, 0.8, 1024);
        stats.UtilizationPercent.Should().Be(50.0);
    }

    [Fact]
    public void UtilizationPercent_WhenMaxIsZero_ShouldReturnZero()
    {
        var stats = new CacheStatistics(0, 0, 0, 0, 0.0, 0);
        stats.UtilizationPercent.Should().Be(0.0);
    }

    [Fact]
    public void IsHealthy_WhenHitRateAboveHalf_ShouldBeTrue()
    {
        var stats = new CacheStatistics(50, 100, 200, 50, 0.8, 1024);
        stats.IsHealthy.Should().BeTrue();
    }

    [Fact]
    public void IsHealthy_WhenWarmingUp_ShouldBeTrue()
    {
        var stats = new CacheStatistics(10, 100, 30, 20, 0.3, 512);
        stats.IsHealthy.Should().BeTrue();
    }

    [Fact]
    public void IsHealthy_WhenLowHitRateAndWarmedUp_ShouldBeFalse()
    {
        var stats = new CacheStatistics(50, 100, 10, 200, 0.047, 1024);
        stats.IsHealthy.Should().BeFalse();
    }

    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var stats = new CacheStatistics(25, 50, 100, 25, 0.8, 2048);

        stats.TotalEntries.Should().Be(25);
        stats.MaxEntries.Should().Be(50);
        stats.HitCount.Should().Be(100);
        stats.MissCount.Should().Be(25);
        stats.HitRate.Should().Be(0.8);
        stats.MemoryEstimateBytes.Should().Be(2048);
    }
}

[Trait("Category", "Unit")]
public class ReviewCommentTests
{
    [Fact]
    public void Create_ShouldSetRequiredProperties()
    {
        var now = DateTime.UtcNow;
        var comment = new ReviewComment("c-1", "reviewer-1", "Fix typo", ReviewCommentStatus.Open, now);

        comment.CommentId.Should().Be("c-1");
        comment.ReviewerId.Should().Be("reviewer-1");
        comment.Content.Should().Be("Fix typo");
        comment.Status.Should().Be(ReviewCommentStatus.Open);
        comment.CreatedAt.Should().Be(now);
        comment.ResolvedAt.Should().BeNull();
    }

    [Fact]
    public void Create_WithResolvedAt_ShouldSetIt()
    {
        var now = DateTime.UtcNow;
        var resolved = now.AddHours(1);
        var comment = new ReviewComment("c-2", "r-1", "Done", ReviewCommentStatus.Resolved, now, resolved);

        comment.ResolvedAt.Should().Be(resolved);
    }
}

[Trait("Category", "Unit")]
public class ReviewDecisionTests
{
    [Fact]
    public void Create_ShouldSetRequiredProperties()
    {
        var now = DateTime.UtcNow;
        var decision = new ReviewDecision("reviewer-1", true, "LGTM", null, now);

        decision.ReviewerId.Should().Be("reviewer-1");
        decision.Approved.Should().BeTrue();
        decision.Feedback.Should().Be("LGTM");
        decision.Comments.Should().BeNull();
        decision.ReviewedAt.Should().Be(now);
    }

    [Fact]
    public void Create_WithComments_ShouldSetThem()
    {
        var comments = new List<ReviewComment>
        {
            new("c-1", "r-1", "Fix this", ReviewCommentStatus.Open, DateTime.UtcNow)
        };
        var decision = new ReviewDecision("r-1", false, "Needs changes", comments, DateTime.UtcNow);

        decision.Approved.Should().BeFalse();
        decision.Comments.Should().HaveCount(1);
    }
}

[Trait("Category", "Unit")]
public class NextNodeCandidateTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var candidate = new NextNodeCandidate("node-5", "analyze", 0.85);

        candidate.NodeId.Should().Be("node-5");
        candidate.Action.Should().Be("analyze");
        candidate.Confidence.Should().Be(0.85);
    }

    [Fact]
    public void Record_Equality_ShouldWorkByValue()
    {
        var a = new NextNodeCandidate("n1", "act", 0.5);
        var b = new NextNodeCandidate("n1", "act", 0.5);
        a.Should().Be(b);
    }
}

[Trait("Category", "Unit")]
public class ScheduledTaskTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var start = DateTime.UtcNow;
        var end = start.AddHours(1);
        var deps = new List<string> { "task-1", "task-2" };
        var task = new ScheduledTask("Build", start, end, deps);

        task.Name.Should().Be("Build");
        task.StartTime.Should().Be(start);
        task.EndTime.Should().Be(end);
        task.Dependencies.Should().HaveCount(2);
    }

    [Fact]
    public void Create_WithEmptyDeps_ShouldWork()
    {
        var task = new ScheduledTask("Deploy", DateTime.UtcNow, DateTime.UtcNow.AddMinutes(5), new List<string>());
        task.Dependencies.Should().BeEmpty();
    }
}
