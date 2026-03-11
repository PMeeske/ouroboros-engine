using FluentAssertions;
using Ouroboros.Agent.MetaAI;
using Xunit;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class MockReviewSystemProviderTests
{
    [Fact]
    public async Task OpenPullRequestAsync_CreatesAndReturnsPr()
    {
        var provider = new MockReviewSystemProvider();

        var result = await provider.OpenPullRequestAsync(
            "Test PR", "Description", "draft spec", new List<string> { "reviewer1" });

        result.IsSuccess.Should().BeTrue();
        var pr = result.Value;
        pr.Title.Should().Be("Test PR");
        pr.Description.Should().Be("Description");
        provider.LastCreatedPrId.Should().Be(pr.Id);
    }

    [Fact]
    public async Task RequestReviewersAsync_WithValidPr_ReturnsSuccess()
    {
        var provider = new MockReviewSystemProvider();
        var prResult = await provider.OpenPullRequestAsync("PR", "desc", "spec", new List<string>());
        var prId = prResult.Value.Id;

        var result = await provider.RequestReviewersAsync(prId, new List<string> { "reviewer1" });

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task RequestReviewersAsync_WithInvalidPr_ReturnsFailure()
    {
        var provider = new MockReviewSystemProvider();

        var result = await provider.RequestReviewersAsync("non-existent", new List<string>());

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task SimulateReview_AddsReviewDecision()
    {
        var provider = new MockReviewSystemProvider();
        var prResult = await provider.OpenPullRequestAsync("PR", "desc", "spec", new List<string>());
        var prId = prResult.Value.Id;

        provider.SimulateReview(prId, "reviewer1", true, "Looks good");

        var decisions = await provider.GetReviewDecisionsAsync(prId);
        decisions.IsSuccess.Should().BeTrue();
        decisions.Value.Should().HaveCount(1);
        decisions.Value[0].ReviewerId.Should().Be("reviewer1");
        decisions.Value[0].Approved.Should().BeTrue();
    }

    [Fact]
    public async Task SimulateReview_UpdatesExistingReview()
    {
        var provider = new MockReviewSystemProvider();
        var prResult = await provider.OpenPullRequestAsync("PR", "desc", "spec", new List<string>());
        var prId = prResult.Value.Id;

        provider.SimulateReview(prId, "reviewer1", false, "Needs work");
        provider.SimulateReview(prId, "reviewer1", true, "Fixed");

        var decisions = await provider.GetReviewDecisionsAsync(prId);
        decisions.Value.Should().HaveCount(1);
        decisions.Value[0].Approved.Should().BeTrue();
    }

    [Fact]
    public async Task SimulateComment_AddsComment()
    {
        var provider = new MockReviewSystemProvider();
        var prResult = await provider.OpenPullRequestAsync("PR", "desc", "spec", new List<string>());
        var prId = prResult.Value.Id;

        provider.SimulateComment(prId, "reviewer1", "Please fix this");

        var comments = await provider.GetCommentsAsync(prId);
        comments.IsSuccess.Should().BeTrue();
        comments.Value.Should().HaveCount(1);
        comments.Value[0].Content.Should().Be("Please fix this");
    }

    [Fact]
    public async Task ResolveCommentAsync_ResolvesComment()
    {
        var provider = new MockReviewSystemProvider();
        var prResult = await provider.OpenPullRequestAsync("PR", "desc", "spec", new List<string>());
        var prId = prResult.Value.Id;
        provider.SimulateComment(prId, "reviewer1", "Fix this");

        var comments = await provider.GetCommentsAsync(prId);
        var commentId = comments.Value[0].CommentId;

        var resolveResult = await provider.ResolveCommentAsync(prId, commentId, "Fixed");

        resolveResult.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ResolveCommentAsync_WithInvalidPr_ReturnsFailure()
    {
        var provider = new MockReviewSystemProvider();

        var result = await provider.ResolveCommentAsync("bad-pr", "bad-comment", "res");

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task MergePullRequestAsync_WithValidPr_ReturnsSuccess()
    {
        var provider = new MockReviewSystemProvider();
        var prResult = await provider.OpenPullRequestAsync("PR", "desc", "spec", new List<string>());
        var prId = prResult.Value.Id;

        var result = await provider.MergePullRequestAsync(prId, "merge commit");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task MergePullRequestAsync_WithInvalidPr_ReturnsFailure()
    {
        var provider = new MockReviewSystemProvider();

        var result = await provider.MergePullRequestAsync("bad-pr", "merge");

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task GetReviewDecisionsAsync_WithInvalidPr_ReturnsFailure()
    {
        var provider = new MockReviewSystemProvider();

        var result = await provider.GetReviewDecisionsAsync("bad-pr");

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task GetCommentsAsync_WithInvalidPr_ReturnsFailure()
    {
        var provider = new MockReviewSystemProvider();

        var result = await provider.GetCommentsAsync("bad-pr");

        result.IsSuccess.Should().BeFalse();
    }
}
