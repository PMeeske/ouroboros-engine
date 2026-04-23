using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Agent.Tests;

[Trait("Category", "Unit")]
public class StakeholderReviewLoopTests
{
    private readonly Mock<IReviewSystemProvider> _reviewSystemMock;
    private readonly StakeholderReviewLoop _reviewLoop;

    public StakeholderReviewLoopTests()
    {
        _reviewSystemMock = new Mock<IReviewSystemProvider>();
        _reviewLoop = new StakeholderReviewLoop(_reviewSystemMock.Object);
    }

    #region Constructor

    [Fact]
    public void Constructor_WithNullReviewSystem_ShouldThrowArgumentNullException()
    {
        // Act
        Action act = () => new StakeholderReviewLoop(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("reviewSystem");
    }

    #endregion

    #region ExecuteReviewLoopAsync

    [Fact]
    public async Task ExecuteReviewLoopAsync_WithNullRequiredReviewers_ShouldNotThrow()
    {
        // Arrange
        var reviewers = new List<string> { "alice", "bob" };
        var pr = new PullRequest("pr-1", "Title", "Desc", "spec", reviewers, DateTime.UtcNow);
        _reviewSystemMock.Setup(r => r.OpenPullRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PullRequest, string>.Success(pr));
        _reviewSystemMock.Setup(r => r.RequestReviewersAsync(It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool, string>.Success(true));
        _reviewSystemMock.Setup(r => r.GetReviewDecisionsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<ReviewDecision>, string>.Success(new List<ReviewDecision> { new("alice", true, null, null, DateTime.UtcNow), new("bob", true, null, null, DateTime.UtcNow) }));
        _reviewSystemMock.Setup(r => r.GetCommentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<ReviewComment>, string>.Success(new List<ReviewComment>()));
        _reviewSystemMock.Setup(r => r.MergePullRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool, string>.Success(true));

        var config = new StakeholderReviewConfig { PollingInterval = TimeSpan.FromMilliseconds(10), ReviewTimeout = TimeSpan.FromSeconds(1) };

        // Act
        var result = await _reviewLoop.ExecuteReviewLoopAsync("Title", "Desc", "spec", reviewers, config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AllApproved.Should().BeTrue();
        result.Value.ApprovedCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteReviewLoopAsync_PRFailure_ShouldReturnFailure()
    {
        // Arrange
        _reviewSystemMock.Setup(r => r.OpenPullRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PullRequest, string>.Failure("PR creation failed"));

        var reviewers = new List<string> { "alice" };
        var config = new StakeholderReviewConfig { PollingInterval = TimeSpan.FromMilliseconds(10) };

        // Act
        var result = await _reviewLoop.ExecuteReviewLoopAsync("Title", "Desc", "spec", reviewers, config);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("PR");
    }

    [Fact]
    public async Task ExecuteReviewLoopAsync_RequestReviewersFailure_ShouldReturnFailure()
    {
        // Arrange
        var pr = new PullRequest("pr-1", "Title", "Desc", "spec", new List<string>(), DateTime.UtcNow);
        _reviewSystemMock.Setup(r => r.OpenPullRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PullRequest, string>.Success(pr));
        _reviewSystemMock.Setup(r => r.RequestReviewersAsync(It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool, string>.Failure("Request failed"));

        var reviewers = new List<string> { "alice" };
        var config = new StakeholderReviewConfig { PollingInterval = TimeSpan.FromMilliseconds(10) };

        // Act
        var result = await _reviewLoop.ExecuteReviewLoopAsync("Title", "Desc", "spec", reviewers, config);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("reviewers");
    }

    [Fact]
    public async Task ExecuteReviewLoopAsync_NotAllApproved_ShouldReturnFailure()
    {
        // Arrange
        var pr = new PullRequest("pr-1", "Title", "Desc", "spec", new List<string>(), DateTime.UtcNow);
        _reviewSystemMock.Setup(r => r.OpenPullRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PullRequest, string>.Success(pr));
        _reviewSystemMock.Setup(r => r.RequestReviewersAsync(It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool, string>.Success(true));
        _reviewSystemMock.Setup(r => r.GetReviewDecisionsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<ReviewDecision>, string>.Success(new List<ReviewDecision> { new("alice", false, "needs work", null, DateTime.UtcNow) }));
        _reviewSystemMock.Setup(r => r.GetCommentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<ReviewComment>, string>.Success(new List<ReviewComment>()));

        var reviewers = new List<string> { "alice" };
        var config = new StakeholderReviewConfig { PollingInterval = TimeSpan.FromMilliseconds(10), ReviewTimeout = TimeSpan.FromSeconds(1) };

        // Act
        var result = await _reviewLoop.ExecuteReviewLoopAsync("Title", "Desc", "spec", reviewers, config);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Not all required approvals");
    }

    #endregion

    #region MonitorReviewProgressAsync

    [Fact]
    public async Task MonitorReviewProgressAsync_Timeout_ShouldReturnFailure()
    {
        // Arrange
        _reviewSystemMock.Setup(r => r.GetReviewDecisionsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<ReviewDecision>, string>.Success(new List<ReviewDecision>()));
        _reviewSystemMock.Setup(r => r.GetCommentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<ReviewComment>, string>.Success(new List<ReviewComment>()));

        var config = new StakeholderReviewConfig
        {
            ReviewTimeout = TimeSpan.FromMilliseconds(50),
            PollingInterval = TimeSpan.FromMilliseconds(10)
        };

        // Act
        var result = await _reviewLoop.MonitorReviewProgressAsync("pr-1", config);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("timeout");
    }

    [Fact]
    public async Task MonitorReviewProgressAsync_GetReviewsFailure_ShouldReturnFailure()
    {
        // Arrange
        _reviewSystemMock.Setup(r => r.GetReviewDecisionsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<ReviewDecision>, string>.Failure("Failed to get reviews"));

        var config = new StakeholderReviewConfig { PollingInterval = TimeSpan.FromMilliseconds(10), ReviewTimeout = TimeSpan.FromSeconds(1) };

        // Act
        var result = await _reviewLoop.MonitorReviewProgressAsync("pr-1", config);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("reviews");
    }

    [Fact]
    public async Task MonitorReviewProgressAsync_Cancellation_ShouldReturnFailure()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        _reviewSystemMock.Setup(r => r.GetReviewDecisionsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<ReviewDecision>, string>.Success(new List<ReviewDecision>()));
        _reviewSystemMock.Setup(r => r.GetCommentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<ReviewComment>, string>.Success(new List<ReviewComment>()));

        var config = new StakeholderReviewConfig { PollingInterval = TimeSpan.FromMilliseconds(100), ReviewTimeout = TimeSpan.FromSeconds(10) };

        // Act
        cts.CancelAfter(50);
        var result = await _reviewLoop.MonitorReviewProgressAsync("pr-1", config, cts.Token);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    #endregion

    #region ResolveCommentsAsync

    [Fact]
    public async Task ResolveCommentsAsync_NoOpenComments_ShouldReturnZero()
    {
        // Arrange
        var comments = new List<ReviewComment>
        {
            new ReviewComment("c1", "r1", "Resolved", ReviewCommentStatus.Resolved, DateTime.UtcNow, DateTime.UtcNow)
        };
        _reviewSystemMock.Setup(r => r.ResolveCommentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool, string>.Success(true));

        // Act
        var result = await _reviewLoop.ResolveCommentsAsync("pr-1", comments);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
    }

    [Fact]
    public async Task ResolveCommentsAsync_WithOpenComments_ShouldResolve()
    {
        // Arrange
        var comments = new List<ReviewComment>
        {
            new ReviewComment("c1", "r1", "Fix this", ReviewCommentStatus.Open, DateTime.UtcNow)
        };
        _reviewSystemMock.Setup(r => r.ResolveCommentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool, string>.Success(true));

        // Act
        var result = await _reviewLoop.ResolveCommentsAsync("pr-1", comments);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1);
    }

    #endregion
}
