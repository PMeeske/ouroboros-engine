using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Agent.Tests;

[Trait("Category", "Unit")]
public class MockReviewSystemProviderTests
{
    private readonly MockReviewSystemProvider _provider;

    public MockReviewSystemProviderTests()
    {
        _provider = new MockReviewSystemProvider();
    }

    #region OpenPullRequestAsync

    [Fact]
    public async Task OpenPullRequestAsync_ValidArgs_ShouldCreatePR()
    {
        var result = await _provider.OpenPullRequestAsync("Title", "Desc", "spec", new List<string> { "alice" });

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Title");
        result.Value.Description.Should().Be("Desc");
        result.Value.DraftSpec.Should().Be("spec");
        result.Value.RequiredReviewers.Should().ContainSingle().Which.Should().Be("alice");
    }

    [Fact]
    public async Task OpenPullRequestAsync_ShouldSetLastCreatedPrId()
    {
        var result = await _provider.OpenPullRequestAsync("Title", "Desc", "spec", new List<string>());

        _provider.LastCreatedPrId.Should().Be(result.Value.Id);
    }

    #endregion

    #region RequestReviewersAsync

    [Fact]
    public async Task RequestReviewersAsync_ExistingPR_ShouldSucceed()
    {
        var pr = await _provider.OpenPullRequestAsync("Title", "Desc", "spec", new List<string>());
        var result = await _provider.RequestReviewersAsync(pr.Value.Id, new List<string> { "bob" });

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task RequestReviewersAsync_NonExistingPR_ShouldFail()
    {
        var result = await _provider.RequestReviewersAsync("nonexistent", new List<string> { "bob" });

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("PR not found");
    }

    #endregion

    #region GetReviewDecisionsAsync

    [Fact]
    public async Task GetReviewDecisionsAsync_ExistingPR_ShouldReturnEmptyList()
    {
        var pr = await _provider.OpenPullRequestAsync("Title", "Desc", "spec", new List<string>());
        var result = await _provider.GetReviewDecisionsAsync(pr.Value.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task GetReviewDecisionsAsync_NonExistingPR_ShouldFail()
    {
        var result = await _provider.GetReviewDecisionsAsync("nonexistent");

        result.IsSuccess.Should().BeFalse();
    }

    #endregion

    #region GetCommentsAsync

    [Fact]
    public async Task GetCommentsAsync_ExistingPR_ShouldReturnEmptyList()
    {
        var pr = await _provider.OpenPullRequestAsync("Title", "Desc", "spec", new List<string>());
        var result = await _provider.GetCommentsAsync(pr.Value.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCommentsAsync_NonExistingPR_ShouldFail()
    {
        var result = await _provider.GetCommentsAsync("nonexistent");

        result.IsSuccess.Should().BeFalse();
    }

    #endregion

    #region ResolveCommentAsync

    [Fact]
    public async Task ResolveCommentAsync_ExistingComment_ShouldSucceed()
    {
        var pr = await _provider.OpenPullRequestAsync("Title", "Desc", "spec", new List<string>());
        _provider.SimulateComment(pr.Value.Id, "reviewer", "fix this");
        var comments = (await _provider.GetCommentsAsync(pr.Value.Id)).Value;
        var commentId = comments[0].CommentId;

        var result = await _provider.ResolveCommentAsync(pr.Value.Id, commentId, "fixed");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();

        var updated = (await _provider.GetCommentsAsync(pr.Value.Id)).Value;
        updated[0].Status.Should().Be(ReviewCommentStatus.Resolved);
    }

    [Fact]
    public async Task ResolveCommentAsync_NonExistingComment_ShouldFail()
    {
        var pr = await _provider.OpenPullRequestAsync("Title", "Desc", "spec", new List<string>());
        var result = await _provider.ResolveCommentAsync(pr.Value.Id, "nonexistent", "fixed");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Comment not found");
    }

    [Fact]
    public async Task ResolveCommentAsync_NonExistingPR_ShouldFail()
    {
        var result = await _provider.ResolveCommentAsync("nonexistent", "c1", "fixed");

        result.IsSuccess.Should().BeFalse();
    }

    #endregion

    #region MergePullRequestAsync

    [Fact]
    public async Task MergePullRequestAsync_ExistingPR_ShouldSucceed()
    {
        var pr = await _provider.OpenPullRequestAsync("Title", "Desc", "spec", new List<string>());
        var result = await _provider.MergePullRequestAsync(pr.Value.Id, "merge msg");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task MergePullRequestAsync_NonExistingPR_ShouldFail()
    {
        var result = await _provider.MergePullRequestAsync("nonexistent", "merge msg");

        result.IsSuccess.Should().BeFalse();
    }

    #endregion

    #region SimulateReview

    [Fact]
    public async Task SimulateReview_ShouldAddReview()
    {
        var pr = await _provider.OpenPullRequestAsync("Title", "Desc", "spec", new List<string>());
        _provider.SimulateReview(pr.Value.Id, "alice", true, "LGTM");

        var decisions = (await _provider.GetReviewDecisionsAsync(pr.Value.Id)).Value;
        decisions.Should().ContainSingle();
        decisions[0].ReviewerId.Should().Be("alice");
        decisions[0].Approved.Should().BeTrue();
        decisions[0].Feedback.Should().Be("LGTM");
    }

    [Fact]
    public async Task SimulateReview_SameReviewerTwice_ShouldUpdate()
    {
        var pr = await _provider.OpenPullRequestAsync("Title", "Desc", "spec", new List<string>());
        _provider.SimulateReview(pr.Value.Id, "alice", true, "LGTM");
        _provider.SimulateReview(pr.Value.Id, "alice", false, "Found bug");

        var decisions = (await _provider.GetReviewDecisionsAsync(pr.Value.Id)).Value;
        decisions.Should().ContainSingle();
        decisions[0].Approved.Should().BeFalse();
        decisions[0].Feedback.Should().Be("Found bug");
    }

    [Fact]
    public void SimulateReview_NonExistingPR_ShouldNotThrow()
    {
        _provider.SimulateReview("nonexistent", "alice", true);
    }

    #endregion

    #region SimulateComment

    [Fact]
    public async Task SimulateComment_ShouldAddComment()
    {
        var pr = await _provider.OpenPullRequestAsync("Title", "Desc", "spec", new List<string>());
        _provider.SimulateComment(pr.Value.Id, "alice", "fix this");

        var comments = (await _provider.GetCommentsAsync(pr.Value.Id)).Value;
        comments.Should().ContainSingle();
        comments[0].ReviewerId.Should().Be("alice");
        comments[0].Content.Should().Be("fix this");
        comments[0].Status.Should().Be(ReviewCommentStatus.Open);
    }

    [Fact]
    public void SimulateComment_NonExistingPR_ShouldNotThrow()
    {
        _provider.SimulateComment("nonexistent", "alice", "fix this");
    }

    #endregion
}
