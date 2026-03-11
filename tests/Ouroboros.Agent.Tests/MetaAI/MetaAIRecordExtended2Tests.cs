using FluentAssertions;
using Ouroboros.Agent.MetaAI;
using Xunit;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class ToolSelectionContextTests
{
    [Fact]
    public void Create_Default_ShouldHaveNullOptionalProperties()
    {
        var context = new ToolSelectionContext();

        context.MaxTools.Should().BeNull();
        context.RequiredCategories.Should().BeNull();
        context.ExcludedCategories.Should().BeNull();
        context.RequiredToolNames.Should().BeNull();
        context.PreferFastTools.Should().BeFalse();
        context.PreferReliableTools.Should().BeFalse();
    }

    [Fact]
    public void Create_WithInitProperties_ShouldSetThem()
    {
        var context = new ToolSelectionContext
        {
            MaxTools = 5,
            RequiredCategories = new List<ToolCategory> { ToolCategory.Code, ToolCategory.Analysis },
            ExcludedCategories = new List<ToolCategory> { ToolCategory.Creative },
            RequiredToolNames = new List<string> { "compiler" },
            PreferFastTools = true,
            PreferReliableTools = true
        };

        context.MaxTools.Should().Be(5);
        context.RequiredCategories.Should().HaveCount(2);
        context.ExcludedCategories.Should().HaveCount(1);
        context.RequiredToolNames.Should().HaveCount(1);
        context.PreferFastTools.Should().BeTrue();
        context.PreferReliableTools.Should().BeTrue();
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        var a = new ToolSelectionContext { MaxTools = 3, PreferFastTools = true };
        var b = new ToolSelectionContext { MaxTools = 3, PreferFastTools = true };

        a.Should().Be(b);
    }
}

[Trait("Category", "Unit")]
public class TestCaseTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var context = new Dictionary<string, object> { ["key"] = "value" };
        var testCase = new TestCase("test1", "achieve goal", context, null);

        testCase.Name.Should().Be("test1");
        testCase.Goal.Should().Be("achieve goal");
        testCase.Context.Should().ContainKey("key");
        testCase.CustomValidator.Should().BeNull();
    }

    [Fact]
    public void Create_WithNullContext_ShouldAllowNull()
    {
        var testCase = new TestCase("test2", "goal", null, null);

        testCase.Context.Should().BeNull();
    }

    [Fact]
    public void Create_WithCustomValidator_ShouldSetIt()
    {
        Func<PlanVerificationResult, bool> validator = r => true;
        var testCase = new TestCase("test3", "goal", null, validator);

        testCase.CustomValidator.Should().NotBeNull();
    }
}

[Trait("Category", "Unit")]
public class ReviewStateTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var now = DateTime.UtcNow;
        var reviewers = new List<string> { "alice" };
        var pr = new PullRequest("pr-1", "Fix", "Desc", "Spec", reviewers, now);
        var reviews = new List<ReviewDecision>();
        var comments = new List<ReviewComment>();

        var state = new ReviewState(pr, reviews, comments, ReviewStatus.Draft, now);

        state.PR.Should().Be(pr);
        state.Reviews.Should().BeEmpty();
        state.AllComments.Should().BeEmpty();
        state.Status.Should().Be(ReviewStatus.Draft);
        state.LastUpdatedAt.Should().Be(now);
    }
}

[Trait("Category", "Unit")]
public class StakeholderReviewResultTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var now = DateTime.UtcNow;
        var reviewers = new List<string> { "alice" };
        var pr = new PullRequest("pr-1", "Fix", "Desc", "Spec", reviewers, now);
        var state = new ReviewState(pr, new List<ReviewDecision>(), new List<ReviewComment>(), ReviewStatus.Approved, now);

        var result = new StakeholderReviewResult(state, true, 3, 3, 5, 0, TimeSpan.FromMinutes(30), "All approved");

        result.FinalState.Should().Be(state);
        result.AllApproved.Should().BeTrue();
        result.TotalReviewers.Should().Be(3);
        result.ApprovedCount.Should().Be(3);
        result.CommentsResolved.Should().Be(5);
        result.CommentsRemaining.Should().Be(0);
        result.Duration.Should().Be(TimeSpan.FromMinutes(30));
        result.Summary.Should().Be("All approved");
    }
}

[Trait("Category", "Unit")]
public class SkillSuggestionTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var skill = new Skill("search", "Search the web", new List<string> { "query" });
        var suggestion = new SkillSuggestion("search", skill, 0.95, "search('query')");

        suggestion.TokenName.Should().Be("search");
        suggestion.Skill.Should().Be(skill);
        suggestion.RelevanceScore.Should().Be(0.95);
        suggestion.UsageExample.Should().Be("search('query')");
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        var skill = new Skill("s", "d", new List<string>());
        var a = new SkillSuggestion("t", skill, 0.5, "ex");
        var b = new SkillSuggestion("t", skill, 0.5, "ex");

        a.Should().Be(b);
    }
}
