using FluentAssertions;
using Ouroboros.Agent.MetaAI;
using Xunit;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class OuroborosCapabilityTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var capability = new OuroborosCapability("reasoning", "Symbolic reasoning over MeTTa atoms", 0.85);

        capability.Name.Should().Be("reasoning");
        capability.Description.Should().Be("Symbolic reasoning over MeTTa atoms");
        capability.ConfidenceLevel.Should().Be(0.85);
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        var a = new OuroborosCapability("cap", "desc", 0.5);
        var b = new OuroborosCapability("cap", "desc", 0.5);
        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentConfidence_ShouldNotBeEqual()
    {
        var a = new OuroborosCapability("cap", "desc", 0.5);
        var b = new OuroborosCapability("cap", "desc", 0.9);
        a.Should().NotBe(b);
    }
}

[Trait("Category", "Unit")]
public class OuroborosExperienceTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var insights = new List<string> { "Retry helps", "Caching improves performance" }.AsReadOnly();
        var duration = TimeSpan.FromMinutes(3);

        var experience = new OuroborosExperience(id, "Build pipeline", true, 0.92, insights, now, duration);

        experience.Id.Should().Be(id);
        experience.Goal.Should().Be("Build pipeline");
        experience.Success.Should().BeTrue();
        experience.QualityScore.Should().Be(0.92);
        experience.Insights.Should().HaveCount(2);
        experience.Timestamp.Should().Be(now);
        experience.Duration.Should().Be(duration);
    }

    [Fact]
    public void Create_WithDefaultDuration_ShouldBeZero()
    {
        var experience = new OuroborosExperience(
            Guid.NewGuid(), "goal", false, 0.3, new List<string>().AsReadOnly(), DateTime.UtcNow);

        experience.Duration.Should().Be(TimeSpan.Zero);
    }
}

[Trait("Category", "Unit")]
public class OuroborosLimitationTests
{
    [Fact]
    public void Create_WithMitigation_ShouldSetAllProperties()
    {
        var limitation = new OuroborosLimitation(
            "memory-bound",
            "Limited working memory capacity",
            "Use external memory store");

        limitation.Name.Should().Be("memory-bound");
        limitation.Description.Should().Be("Limited working memory capacity");
        limitation.Mitigation.Should().Be("Use external memory store");
    }

    [Fact]
    public void Create_WithoutMitigation_ShouldDefaultToNull()
    {
        var limitation = new OuroborosLimitation("slow-inference", "High latency for complex queries");

        limitation.Name.Should().Be("slow-inference");
        limitation.Mitigation.Should().BeNull();
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        var a = new OuroborosLimitation("l", "d", "m");
        var b = new OuroborosLimitation("l", "d", "m");
        a.Should().Be(b);
    }
}

[Trait("Category", "Unit")]
public class OuroborosResultTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var phaseResults = new List<PhaseResult>
        {
            new(ImprovementPhase.Plan, true, "Plan created", null, TimeSpan.FromSeconds(1)),
            new(ImprovementPhase.Execute, true, "Executed", null, TimeSpan.FromSeconds(2))
        }.AsReadOnly();
        var metadata = new Dictionary<string, object> { ["model"] = "gpt-4" };
        var duration = TimeSpan.FromSeconds(5);

        var result = new OuroborosResult(
            "Summarize document",
            true,
            "Summary output",
            phaseResults,
            3,
            ImprovementPhase.Learn,
            "Performance improved across iterations",
            duration,
            metadata);

        result.Goal.Should().Be("Summarize document");
        result.Success.Should().BeTrue();
        result.Output.Should().Be("Summary output");
        result.PhaseResults.Should().HaveCount(2);
        result.CycleCount.Should().Be(3);
        result.CurrentPhase.Should().Be(ImprovementPhase.Learn);
        result.SelfReflection.Should().Contain("improved");
        result.Duration.Should().Be(duration);
        result.Metadata.Should().ContainKey("model");
    }
}

[Trait("Category", "Unit")]
public class PhaseResultTests
{
    [Fact]
    public void Create_Success_ShouldSetAllProperties()
    {
        var duration = TimeSpan.FromSeconds(1.5);
        var result = new PhaseResult(
            ImprovementPhase.Plan, true, "Plan generated", null, duration);

        result.Phase.Should().Be(ImprovementPhase.Plan);
        result.Success.Should().BeTrue();
        result.Output.Should().Be("Plan generated");
        result.Error.Should().BeNull();
        result.Duration.Should().Be(duration);
    }

    [Fact]
    public void Create_Failure_WithError_ShouldSetIt()
    {
        var result = new PhaseResult(
            ImprovementPhase.Execute, false, "", "Timeout occurred", TimeSpan.FromSeconds(30));

        result.Success.Should().BeFalse();
        result.Error.Should().Be("Timeout occurred");
    }

    [Fact]
    public void Metadata_WhenNull_ShouldDefaultToEmptyDictionary()
    {
        var result = new PhaseResult(
            ImprovementPhase.Verify, true, "OK", null, TimeSpan.Zero, null);

        result.Metadata.Should().NotBeNull();
        result.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void Metadata_WhenProvided_ShouldRetainValues()
    {
        var meta = new Dictionary<string, object> { ["key"] = "value" };
        var result = new PhaseResult(
            ImprovementPhase.Learn, true, "Learned", null, TimeSpan.Zero, meta);

        result.Metadata.Should().HaveCount(1);
        result.Metadata["key"].Should().Be("value");
    }
}

[Trait("Category", "Unit")]
public class PromptResultTests
{
    [Fact]
    public void Create_Success_ShouldSetAllProperties()
    {
        var result = new PromptResult(
            "What is 2+2?", true, 150.5, 0.95, "gpt-4", null);

        result.Prompt.Should().Be("What is 2+2?");
        result.Success.Should().BeTrue();
        result.LatencyMs.Should().Be(150.5);
        result.ConfidenceScore.Should().Be(0.95);
        result.SelectedModel.Should().Be("gpt-4");
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Create_Failure_ShouldSetError()
    {
        var result = new PromptResult(
            "complex query", false, 5000, 0.0, null, "Timeout");

        result.Success.Should().BeFalse();
        result.SelectedModel.Should().BeNull();
        result.Error.Should().Be("Timeout");
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        var a = new PromptResult("p", true, 100, 0.9, "m", null);
        var b = new PromptResult("p", true, 100, 0.9, "m", null);
        a.Should().Be(b);
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

        var pr = new PullRequest("pr-1", "Add auth", "Authentication feature", "Draft spec content", reviewers, now);

        pr.Id.Should().Be("pr-1");
        pr.Title.Should().Be("Add auth");
        pr.Description.Should().Be("Authentication feature");
        pr.DraftSpec.Should().Be("Draft spec content");
        pr.RequiredReviewers.Should().HaveCount(2);
        pr.RequiredReviewers.Should().Contain("alice");
        pr.CreatedAt.Should().Be(now);
    }
}

[Trait("Category", "Unit")]
public class ReviewStateTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var now = DateTime.UtcNow;
        var pr = new PullRequest("pr-1", "Title", "Desc", "Spec", new List<string> { "r1" }, now);
        var reviews = new List<ReviewDecision>
        {
            new("r1", true, "Looks good", null, now)
        };
        var comments = new List<ReviewComment>
        {
            new("c1", "r1", "Nice work", ReviewCommentStatus.Resolved, now)
        };

        var state = new ReviewState(pr, reviews, comments, ReviewStatus.Approved, now);

        state.PR.Should().Be(pr);
        state.Reviews.Should().HaveCount(1);
        state.AllComments.Should().HaveCount(1);
        state.Status.Should().Be(ReviewStatus.Approved);
        state.LastUpdatedAt.Should().Be(now);
    }
}

[Trait("Category", "Unit")]
public class HumanFeedbackRequestTests
{
    [Fact]
    public void Create_WithOptions_ShouldSetAllProperties()
    {
        var now = DateTime.UtcNow;
        var timeout = TimeSpan.FromMinutes(10);
        var options = new List<string> { "Approve", "Reject", "Defer" };

        var request = new HumanFeedbackRequest(
            "fb-1", "Deployment context", "Should we proceed?", options, now, timeout);

        request.RequestId.Should().Be("fb-1");
        request.Context.Should().Be("Deployment context");
        request.Question.Should().Be("Should we proceed?");
        request.Options.Should().HaveCount(3);
        request.RequestedAt.Should().Be(now);
        request.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void Create_WithoutOptions_ShouldAllowNull()
    {
        var request = new HumanFeedbackRequest(
            "fb-2", "ctx", "What next?", null, DateTime.UtcNow, TimeSpan.FromMinutes(5));

        request.Options.Should().BeNull();
    }
}

[Trait("Category", "Unit")]
public class HumanFeedbackResponseTests
{
    [Fact]
    public void Create_WithMetadata_ShouldSetAllProperties()
    {
        var now = DateTime.UtcNow;
        var metadata = new Dictionary<string, object> { ["confidence"] = 0.9 };

        var response = new HumanFeedbackResponse("fb-1", "Approve", metadata, now);

        response.RequestId.Should().Be("fb-1");
        response.Response.Should().Be("Approve");
        response.Metadata.Should().HaveCount(1);
        response.RespondedAt.Should().Be(now);
    }

    [Fact]
    public void Create_WithoutMetadata_ShouldAllowNull()
    {
        var response = new HumanFeedbackResponse("fb-2", "Reject", null, DateTime.UtcNow);

        response.Metadata.Should().BeNull();
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        var now = DateTime.UtcNow;
        var a = new HumanFeedbackResponse("r", "ok", null, now);
        var b = new HumanFeedbackResponse("r", "ok", null, now);
        a.Should().Be(b);
    }
}
