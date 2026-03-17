using FluentAssertions;
using System.Diagnostics;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class StakeholderReviewLoopTests
{
    private readonly Mock<IReviewSystemProvider> _mockReviewSystem = new();

    private StakeholderReviewLoop CreateSut()
    {
        return new StakeholderReviewLoop(_mockReviewSystem.Object);
    }

    // === Constructor Tests ===

    [Fact]
    public void Constructor_NullReviewSystem_ThrowsArgumentNullException()
    {
        var act = () => new StakeholderReviewLoop(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("reviewSystem");
    }

    [Fact]
    public void Constructor_ValidArgs_DoesNotThrow()
    {
        var act = () => CreateSut();
        act.Should().NotThrow();
    }

    // === ExecuteReviewLoopAsync Tests ===

    [Fact]
    public async Task ExecuteReviewLoopAsync_PrOpenFails_ReturnsFailure()
    {
        var sut = CreateSut();
        _mockReviewSystem.Setup(r => r.OpenPullRequestAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PullRequest, string>.Failure("PR creation failed"));

        var result = await sut.ExecuteReviewLoopAsync("title", "desc", "spec", new List<string> { "reviewer1" });

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Failed to open PR");
    }

    [Fact]
    public async Task ExecuteReviewLoopAsync_RequestReviewersFails_ReturnsFailure()
    {
        var sut = CreateSut();
        var pr = new PullRequest("pr-1", "title", "desc", "spec", new List<string>(), DateTime.UtcNow);

        _mockReviewSystem.Setup(r => r.OpenPullRequestAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PullRequest, string>.Success(pr));

        _mockReviewSystem.Setup(r => r.RequestReviewersAsync(
            It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool, string>.Failure("reviewer request failed"));

        var result = await sut.ExecuteReviewLoopAsync("title", "desc", "spec", new List<string> { "reviewer1" });

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Failed to request reviewers");
    }

    // === MonitorReviewProgressAsync Tests ===

    [Fact]
    public async Task MonitorReviewProgressAsync_AllApproved_ReturnsApproved()
    {
        var sut = CreateSut();
        var config = new StakeholderReviewConfig(
            MinimumRequiredApprovals: 1,
            RequireAllReviewersApprove: true);

        var reviews = new List<ReviewDecision>
        {
            new ReviewDecision("reviewer1", true, "Looks good", DateTime.UtcNow)
        };
        var comments = new List<ReviewComment>();

        _mockReviewSystem.Setup(r => r.GetReviewDecisionsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<ReviewDecision>, string>.Success(reviews));

        _mockReviewSystem.Setup(r => r.GetCommentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<ReviewComment>, string>.Success(comments));

        var result = await sut.MonitorReviewProgressAsync("pr-1", config);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(ReviewStatus.Approved);
    }

    [Fact]
    public async Task MonitorReviewProgressAsync_GetReviewsFails_ReturnsFailure()
    {
        var sut = CreateSut();

        _mockReviewSystem.Setup(r => r.GetReviewDecisionsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<ReviewDecision>, string>.Failure("API error"));

        var result = await sut.MonitorReviewProgressAsync("pr-1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Failed to get reviews");
    }

    [Fact]
    public async Task MonitorReviewProgressAsync_GetCommentsFails_ReturnsFailure()
    {
        var sut = CreateSut();
        var reviews = new List<ReviewDecision>();

        _mockReviewSystem.Setup(r => r.GetReviewDecisionsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<ReviewDecision>, string>.Success(reviews));

        _mockReviewSystem.Setup(r => r.GetCommentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<ReviewComment>, string>.Failure("comments API error"));

        var result = await sut.MonitorReviewProgressAsync("pr-1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Failed to get comments");
    }

    [Fact]
    public async Task MonitorReviewProgressAsync_Cancelled_ReturnsFailure()
    {
        var sut = CreateSut();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => sut.MonitorReviewProgressAsync("pr-1", null, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // === ResolveCommentsAsync Tests ===

    [Fact]
    public async Task ResolveCommentsAsync_NoOpenComments_ReturnsZero()
    {
        var sut = CreateSut();
        var comments = new List<ReviewComment>
        {
            new ReviewComment("c1", "reviewer1", "comment", ReviewCommentStatus.Resolved, DateTime.UtcNow)
        };

        var result = await sut.ResolveCommentsAsync("pr-1", comments);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
    }

    [Fact]
    public async Task ResolveCommentsAsync_WithOpenComments_ResolvesAndReturnsCount()
    {
        var sut = CreateSut();
        var comments = new List<ReviewComment>
        {
            new ReviewComment("c1", "reviewer1", "fix typo", ReviewCommentStatus.Open, DateTime.UtcNow),
            new ReviewComment("c2", "reviewer2", "add tests", ReviewCommentStatus.Open, DateTime.UtcNow)
        };

        _mockReviewSystem.Setup(r => r.ResolveCommentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool, string>.Success(true));

        var result = await sut.ResolveCommentsAsync("pr-1", comments);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(2);
    }

    [Fact]
    public async Task ResolveCommentsAsync_ResolveFailsForSome_ReturnsPartialCount()
    {
        var sut = CreateSut();
        var comments = new List<ReviewComment>
        {
            new ReviewComment("c1", "reviewer1", "fix typo", ReviewCommentStatus.Open, DateTime.UtcNow),
            new ReviewComment("c2", "reviewer2", "add tests", ReviewCommentStatus.Open, DateTime.UtcNow)
        };

        var callCount = 0;
        _mockReviewSystem.Setup(r => r.ResolveCommentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1
                    ? Result<bool, string>.Success(true)
                    : Result<bool, string>.Failure("failed");
            });

        var result = await sut.ResolveCommentsAsync("pr-1", comments);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1);
    }
}
