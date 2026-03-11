using FluentAssertions;
using Ouroboros.Agent.MetaAI;
using Xunit;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class TemporalConstraintTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var constraint = new TemporalConstraint("taskA", "taskB", TemporalRelation.Before, TimeSpan.FromMinutes(5));

        constraint.TaskA.Should().Be("taskA");
        constraint.TaskB.Should().Be("taskB");
        constraint.Relation.Should().Be(TemporalRelation.Before);
        constraint.Duration.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void Create_WithoutDuration_ShouldDefaultToNull()
    {
        var constraint = new TemporalConstraint("a", "b", TemporalRelation.Simultaneous);

        constraint.Duration.Should().BeNull();
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        var a = new TemporalConstraint("x", "y", TemporalRelation.During);
        var b = new TemporalConstraint("x", "y", TemporalRelation.During);

        a.Should().Be(b);
    }
}

[Trait("Category", "Unit")]
public class PhaseResultTests
{
    [Fact]
    public void Create_WithSuccess_ShouldSetProperties()
    {
        var result = new PhaseResult(ImprovementPhase.Plan, true, "planned", null, TimeSpan.FromSeconds(1));

        result.Phase.Should().Be(ImprovementPhase.Plan);
        result.Success.Should().BeTrue();
        result.Output.Should().Be("planned");
        result.Error.Should().BeNull();
        result.Duration.Should().Be(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Create_WithNullMetadata_ShouldInitializeEmptyDictionary()
    {
        var result = new PhaseResult(ImprovementPhase.Execute, true, "done", null, TimeSpan.Zero);

        result.Metadata.Should().NotBeNull();
        result.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithMetadata_ShouldPreserveIt()
    {
        var meta = new Dictionary<string, object> { ["key"] = "value" };
        var result = new PhaseResult(ImprovementPhase.Verify, false, "", "error", TimeSpan.Zero, meta);

        result.Metadata.Should().ContainKey("key");
    }

    [Fact]
    public void Create_WithError_ShouldSetErrorProperty()
    {
        var result = new PhaseResult(ImprovementPhase.Learn, false, "", "something failed", TimeSpan.FromMilliseconds(100));

        result.Success.Should().BeFalse();
        result.Error.Should().Be("something failed");
    }
}

[Trait("Category", "Unit")]
public class HumanFeedbackRequestTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var now = DateTime.UtcNow;
        var options = new List<string> { "Yes", "No" };
        var request = new HumanFeedbackRequest("req-1", "context", "question?", options, now, TimeSpan.FromMinutes(5));

        request.RequestId.Should().Be("req-1");
        request.Context.Should().Be("context");
        request.Question.Should().Be("question?");
        request.Options.Should().HaveCount(2);
        request.RequestedAt.Should().Be(now);
        request.Timeout.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void Create_WithNullOptions_ShouldAllowNull()
    {
        var request = new HumanFeedbackRequest("r1", "ctx", "q?", null, DateTime.UtcNow, TimeSpan.FromSeconds(30));

        request.Options.Should().BeNull();
    }
}

[Trait("Category", "Unit")]
public class EpicTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var now = DateTime.UtcNow;
        var subIssues = new List<int> { 1, 2, 3 };
        var epic = new Epic(42, "Big Feature", "Description", subIssues, now);

        epic.EpicNumber.Should().Be(42);
        epic.Title.Should().Be("Big Feature");
        epic.Description.Should().Be("Description");
        epic.SubIssueNumbers.Should().HaveCount(3);
        epic.CreatedAt.Should().Be(now);
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        var subs = new List<int> { 1 };
        var now = DateTime.UtcNow;
        var a = new Epic(1, "t", "d", subs, now);
        var b = new Epic(1, "t", "d", subs, now);

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
    public void Create_WithCustomValues_ShouldOverrideDefaults()
    {
        var config = new EpicBranchConfig("feature", "agent", false, false, 10);

        config.BranchPrefix.Should().Be("feature");
        config.AgentPoolPrefix.Should().Be("agent");
        config.AutoCreateBranches.Should().BeFalse();
        config.AutoAssignAgents.Should().BeFalse();
        config.MaxConcurrentSubIssues.Should().Be(10);
    }
}

[Trait("Category", "Unit")]
public class PullRequestTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var now = DateTime.UtcNow;
        var reviewers = new List<string> { "alice", "bob" };
        var pr = new PullRequest("pr-1", "Add feature", "Description", "Draft spec", reviewers, now);

        pr.Id.Should().Be("pr-1");
        pr.Title.Should().Be("Add feature");
        pr.Description.Should().Be("Description");
        pr.DraftSpec.Should().Be("Draft spec");
        pr.RequiredReviewers.Should().HaveCount(2);
        pr.CreatedAt.Should().Be(now);
    }
}

[Trait("Category", "Unit")]
public class SubIssueAssignmentTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var now = DateTime.UtcNow;
        var assignment = new SubIssueAssignment(1, "Title", "Desc", "agent-1", "epic/1", null, SubIssueStatus.Pending, now);

        assignment.IssueNumber.Should().Be(1);
        assignment.Title.Should().Be("Title");
        assignment.AssignedAgentId.Should().Be("agent-1");
        assignment.BranchName.Should().Be("epic/1");
        assignment.Branch.Should().BeNull();
        assignment.Status.Should().Be(SubIssueStatus.Pending);
        assignment.CompletedAt.Should().BeNull();
        assignment.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Create_WithOptionalFields_ShouldSetThem()
    {
        var now = DateTime.UtcNow;
        var completed = now.AddHours(1);
        var assignment = new SubIssueAssignment(2, "T", "D", "a", "b", null, SubIssueStatus.Failed, now, completed, "error");

        assignment.CompletedAt.Should().Be(completed);
        assignment.ErrorMessage.Should().Be("error");
    }
}

[Trait("Category", "Unit")]
public class OuroborosLimitationTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var limitation = new OuroborosLimitation("Token limit", "Limited context window", "Use chunking");

        limitation.Name.Should().Be("Token limit");
        limitation.Description.Should().Be("Limited context window");
        limitation.Mitigation.Should().Be("Use chunking");
    }

    [Fact]
    public void Create_WithoutMitigation_ShouldDefaultToNull()
    {
        var limitation = new OuroborosLimitation("Hallucination", "May generate incorrect facts");

        limitation.Mitigation.Should().BeNull();
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        var a = new OuroborosLimitation("x", "y");
        var b = new OuroborosLimitation("x", "y");

        a.Should().Be(b);
    }
}

[Trait("Category", "Unit")]
public class TrainingResultTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var metrics = new Dictionary<string, double> { ["accuracy"] = 0.95 };
        var patterns = new List<string> { "pattern1" };
        var result = new TrainingResult(100, metrics, patterns, true);

        result.ExperiencesProcessed.Should().Be(100);
        result.ImprovedMetrics.Should().ContainKey("accuracy");
        result.LearnedPatterns.Should().HaveCount(1);
        result.Success.Should().BeTrue();
    }
}

[Trait("Category", "Unit")]
public class QdrantSkillRegistryStatsTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var stats = new QdrantSkillRegistryStats(50, 0.85, 1000, "search", "analyze", "localhost:6334", "skills", true);

        stats.TotalSkills.Should().Be(50);
        stats.AverageSuccessRate.Should().Be(0.85);
        stats.TotalExecutions.Should().Be(1000);
        stats.MostUsedSkill.Should().Be("search");
        stats.MostSuccessfulSkill.Should().Be("analyze");
        stats.ConnectionString.Should().Be("localhost:6334");
        stats.CollectionName.Should().Be("skills");
        stats.IsConnected.Should().BeTrue();
    }

    [Fact]
    public void Create_WithNullSkills_ShouldAllowNull()
    {
        var stats = new QdrantSkillRegistryStats(0, 0, 0, null, null, "conn", "col", false);

        stats.MostUsedSkill.Should().BeNull();
        stats.MostSuccessfulSkill.Should().BeNull();
    }
}

[Trait("Category", "Unit")]
public class ExperimentResultTests
{
    [Fact]
    public void Duration_ShouldCalculateCorrectly()
    {
        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddHours(2);
        var result = new ExperimentResult("exp-1", start, end, new List<VariantResult>(), null, null, ExperimentStatus.Completed);

        result.Duration.Should().Be(TimeSpan.FromHours(2));
    }

    [Fact]
    public void IsCompleted_WhenCompleted_ShouldBeTrue()
    {
        var now = DateTime.UtcNow;
        var result = new ExperimentResult("exp-1", now, now, new List<VariantResult>(), null, "variantA", ExperimentStatus.Completed);

        result.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public void IsCompleted_WhenRunning_ShouldBeFalse()
    {
        var now = DateTime.UtcNow;
        var result = new ExperimentResult("exp-1", now, now, new List<VariantResult>(), null, null, ExperimentStatus.Running);

        result.IsCompleted.Should().BeFalse();
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
        config.MaxMetricsAge.Should().Be(90);
    }

    [Fact]
    public void Create_WithCustomValues_ShouldOverrideDefaults()
    {
        var config = new PersistentMetricsConfig("custom/path", "data.json", false, TimeSpan.FromMinutes(10), 30);

        config.StoragePath.Should().Be("custom/path");
        config.FileName.Should().Be("data.json");
        config.AutoSave.Should().BeFalse();
        config.AutoSaveInterval.Should().Be(TimeSpan.FromMinutes(10));
        config.MaxMetricsAge.Should().Be(30);
    }
}
