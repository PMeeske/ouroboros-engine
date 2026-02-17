namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Mock implementation of review system provider for testing.
/// </summary>
public sealed class MockReviewSystemProvider : IReviewSystemProvider
{
    private readonly Dictionary<string, PullRequest> _prs = new();
    private readonly Dictionary<string, List<ReviewDecision>> _reviews = new();
    private readonly Dictionary<string, List<ReviewComment>> _comments = new();
    public string? LastCreatedPrId { get; private set; }

    public Task<Result<PullRequest, string>> OpenPullRequestAsync(
        string title,
        string description,
        string draftSpec,
        List<string> requiredReviewers,
        CancellationToken ct = default)
    {
        PullRequest pr = new PullRequest(
            Guid.NewGuid().ToString(),
            title,
            description,
            draftSpec,
            requiredReviewers,
            DateTime.UtcNow);

        _prs[pr.Id] = pr;
        _reviews[pr.Id] = new List<ReviewDecision>();
        _comments[pr.Id] = new List<ReviewComment>();
        LastCreatedPrId = pr.Id;

        return Task.FromResult(Result<PullRequest, string>.Success(pr));
    }

    public Task<Result<bool, string>> RequestReviewersAsync(
        string prId,
        List<string> reviewers,
        CancellationToken ct = default)
    {
        if (!_prs.ContainsKey(prId))
            return Task.FromResult(Result<bool, string>.Failure("PR not found"));

        // Simulate reviewer requests
        return Task.FromResult(Result<bool, string>.Success(true));
    }

    public Task<Result<List<ReviewDecision>, string>> GetReviewDecisionsAsync(
        string prId,
        CancellationToken ct = default)
    {
        if (!_reviews.ContainsKey(prId))
            return Task.FromResult(Result<List<ReviewDecision>, string>.Failure("PR not found"));

        return Task.FromResult(Result<List<ReviewDecision>, string>.Success(_reviews[prId]));
    }

    public Task<Result<List<ReviewComment>, string>> GetCommentsAsync(
        string prId,
        CancellationToken ct = default)
    {
        if (!_comments.ContainsKey(prId))
            return Task.FromResult(Result<List<ReviewComment>, string>.Failure("PR not found"));

        return Task.FromResult(Result<List<ReviewComment>, string>.Success(_comments[prId]));
    }

    public Task<Result<bool, string>> ResolveCommentAsync(
        string prId,
        string commentId,
        string resolution,
        CancellationToken ct = default)
    {
        if (!_comments.ContainsKey(prId))
            return Task.FromResult(Result<bool, string>.Failure("PR not found"));

        ReviewComment? comment = _comments[prId].FirstOrDefault(c => c.CommentId == commentId);
        if (comment == null)
            return Task.FromResult(Result<bool, string>.Failure("Comment not found"));

        // Remove old comment and add resolved version
        ReviewComment updatedComment = comment with
        {
            Status = ReviewCommentStatus.Resolved,
            ResolvedAt = DateTime.UtcNow
        };

        List<ReviewComment> comments = _comments[prId];
        int index = comments.IndexOf(comment);
        if (index >= 0)
        {
            comments[index] = updatedComment;
        }

        return Task.FromResult(Result<bool, string>.Success(true));
    }

    public Task<Result<bool, string>> MergePullRequestAsync(
        string prId,
        string mergeCommitMessage,
        CancellationToken ct = default)
    {
        if (!_prs.ContainsKey(prId))
            return Task.FromResult(Result<bool, string>.Failure("PR not found"));

        // Simulate merge
        return Task.FromResult(Result<bool, string>.Success(true));
    }

    // Test helper methods
    public void SimulateReview(string prId, string reviewerId, bool approved, string? feedback = null)
    {
        if (!_reviews.ContainsKey(prId)) return;

        // Remove any existing review from this reviewer (they can update their review)
        ReviewDecision? existingReview = _reviews[prId].FirstOrDefault(r => r.ReviewerId == reviewerId);
        if (existingReview != null)
        {
            _reviews[prId].Remove(existingReview);
        }

        ReviewDecision review = new ReviewDecision(
            reviewerId,
            approved,
            feedback,
            null,
            DateTime.UtcNow);

        _reviews[prId].Add(review);
    }

    public void SimulateComment(string prId, string reviewerId, string content)
    {
        if (!_comments.ContainsKey(prId)) return;

        ReviewComment comment = new ReviewComment(
            Guid.NewGuid().ToString(),
            reviewerId,
            content,
            ReviewCommentStatus.Open,
            DateTime.UtcNow);

        _comments[prId].Add(comment);
    }
}