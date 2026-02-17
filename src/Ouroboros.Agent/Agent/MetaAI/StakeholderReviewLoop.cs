#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Stakeholder Review Loop - PR-based approval workflow
// ==========================================================

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Implementation of stakeholder review loop orchestration.
/// </summary>
public sealed class StakeholderReviewLoop : IStakeholderReviewLoop
{
    private readonly IReviewSystemProvider _reviewSystem;

    public StakeholderReviewLoop(IReviewSystemProvider reviewSystem)
    {
        _reviewSystem = reviewSystem ?? throw new ArgumentNullException(nameof(reviewSystem));
    }

    /// <summary>
    /// Executes the complete stakeholder review workflow.
    /// Opens PR, requests reviews, resolves comments, and merges when approved.
    /// </summary>
    public async Task<Result<StakeholderReviewResult, string>> ExecuteReviewLoopAsync(
        string title,
        string description,
        string draftSpec,
        List<string> requiredReviewers,
        StakeholderReviewConfig? config = null,
        CancellationToken ct = default)
    {
        config ??= new StakeholderReviewConfig(
            ReviewTimeout: TimeSpan.FromHours(24),
            PollingInterval: TimeSpan.FromMinutes(5));

        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Step 1: Open PR
            Result<PullRequest, string> prResult = await _reviewSystem.OpenPullRequestAsync(
                title, description, draftSpec, requiredReviewers, ct);

            if (!prResult.IsSuccess)
                return Result<StakeholderReviewResult, string>.Failure(
                    $"Failed to open PR: {prResult.Error}");

            PullRequest pr = prResult.Value;

            // Step 2: Request reviewers
            Result<bool, string> requestResult = await _reviewSystem.RequestReviewersAsync(
                pr.Id, requiredReviewers, ct);

            if (!requestResult.IsSuccess)
                return Result<StakeholderReviewResult, string>.Failure(
                    $"Failed to request reviewers: {requestResult.Error}");

            // Step 3: Monitor review progress
            Result<ReviewState, string> monitorResult = await MonitorReviewProgressAsync(pr.Id, config, ct);

            if (!monitorResult.IsSuccess)
                return Result<StakeholderReviewResult, string>.Failure(
                    $"Review monitoring failed: {monitorResult.Error}");

            ReviewState finalState = monitorResult.Value;

            // Step 4: Check if all required approvals are obtained
            bool allApproved = CheckAllApproved(finalState, config);

            if (!allApproved)
                return Result<StakeholderReviewResult, string>.Failure(
                    "Not all required approvals obtained");

            // Step 5: Resolve remaining comments
            List<ReviewComment> openComments = finalState.AllComments
                .Where(c => c.Status == ReviewCommentStatus.Open)
                .ToList();

            if (openComments.Any())
            {
                Result<int, string> resolveResult = await ResolveCommentsAsync(pr.Id, openComments, ct);
                if (!resolveResult.IsSuccess && !config.AutoResolveNonBlockingComments)
                    return Result<StakeholderReviewResult, string>.Failure(
                        $"Comment resolution failed: {resolveResult.Error}");
            }

            // Step 6: Merge PR
            Result<bool, string> mergeResult = await _reviewSystem.MergePullRequestAsync(
                pr.Id,
                $"Merge approved by all reviewers: {string.Join(", ", requiredReviewers)}",
                ct);

            if (!mergeResult.IsSuccess)
                return Result<StakeholderReviewResult, string>.Failure(
                    $"Failed to merge PR: {mergeResult.Error}");

            sw.Stop();

            StakeholderReviewResult result = new StakeholderReviewResult(
                finalState with { Status = ReviewStatus.Merged },
                true,
                requiredReviewers.Count,
                finalState.Reviews.Count(r => r.Approved),
                finalState.AllComments.Count(c => c.Status == ReviewCommentStatus.Resolved),
                finalState.AllComments.Count(c => c.Status == ReviewCommentStatus.Open),
                sw.Elapsed,
                $"PR '{title}' successfully merged after {finalState.Reviews.Count} approvals");

            return Result<StakeholderReviewResult, string>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<StakeholderReviewResult, string>.Failure(
                $"Review loop execution failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Monitors an existing PR until all approvals are collected.
    /// </summary>
    public async Task<Result<ReviewState, string>> MonitorReviewProgressAsync(
        string prId,
        StakeholderReviewConfig? config = null,
        CancellationToken ct = default)
    {
        config ??= new StakeholderReviewConfig();

        DateTime startTime = DateTime.UtcNow;
        TimeSpan timeout = config.ReviewTimeout == default
            ? TimeSpan.FromHours(24)
            : config.ReviewTimeout;

        TimeSpan pollingInterval = config.PollingInterval == default
            ? TimeSpan.FromMinutes(5)
            : config.PollingInterval;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                // Check timeout
                if (DateTime.UtcNow - startTime > timeout)
                    return Result<ReviewState, string>.Failure("Review timeout exceeded");

                // Get current review decisions
                Result<List<ReviewDecision>, string> reviewsResult = await _reviewSystem.GetReviewDecisionsAsync(prId, ct);
                if (!reviewsResult.IsSuccess)
                    return Result<ReviewState, string>.Failure(
                        $"Failed to get reviews: {reviewsResult.Error}");

                List<ReviewDecision> reviews = reviewsResult.Value;

                // Get all comments
                Result<List<ReviewComment>, string> commentsResult = await _reviewSystem.GetCommentsAsync(prId, ct);
                if (!commentsResult.IsSuccess)
                    return Result<ReviewState, string>.Failure(
                        $"Failed to get comments: {commentsResult.Error}");

                List<ReviewComment> comments = commentsResult.Value;

                // Build current state
                ReviewStatus status = DetermineReviewStatus(reviews, comments, config);
                ReviewState reviewState = new ReviewState(
                    new PullRequest(prId, "", "", "", new List<string>(), DateTime.UtcNow),
                    reviews,
                    comments,
                    status,
                    DateTime.UtcNow);

                // Check if review is complete
                if (status == ReviewStatus.Approved)
                    return Result<ReviewState, string>.Success(reviewState);

                // Wait before next poll
                await Task.Delay(pollingInterval, ct);
            }

            return Result<ReviewState, string>.Failure("Monitoring cancelled");
        }
        catch (Exception ex)
        {
            return Result<ReviewState, string>.Failure(
                $"Review monitoring failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Attempts to automatically resolve comments based on feedback.
    /// </summary>
    public async Task<Result<int, string>> ResolveCommentsAsync(
        string prId,
        List<ReviewComment> comments,
        CancellationToken ct = default)
    {
        try
        {
            int resolvedCount = 0;

            // Create a copy to avoid modification during enumeration
            List<ReviewComment> openComments = comments.Where(c => c.Status == ReviewCommentStatus.Open).ToList();

            foreach (ReviewComment? comment in openComments)
            {
                // Generate resolution based on comment content
                string resolution = GenerateResolution(comment.Content);

                Result<bool, string> resolveResult = await _reviewSystem.ResolveCommentAsync(
                    prId, comment.CommentId, resolution, ct);

                if (resolveResult.IsSuccess)
                    resolvedCount++;
            }

            return Result<int, string>.Success(resolvedCount);
        }
        catch (Exception ex)
        {
            return Result<int, string>.Failure(
                $"Comment resolution failed: {ex.Message}");
        }
    }

    private bool CheckAllApproved(ReviewState state, StakeholderReviewConfig config)
    {
        int approvedCount = state.Reviews.Count(r => r.Approved);

        if (config.RequireAllReviewersApprove)
        {
            // All reviewers must approve
            return state.Reviews.Count > 0 && state.Reviews.All(r => r.Approved);
        }
        else
        {
            // Minimum number of approvals
            return approvedCount >= config.MinimumRequiredApprovals;
        }
    }

    private ReviewStatus DetermineReviewStatus(
        List<ReviewDecision> reviews,
        List<ReviewComment> comments,
        StakeholderReviewConfig config)
    {
        if (!reviews.Any())
            return ReviewStatus.AwaitingReview;

        bool hasOpenComments = comments.Any(c => c.Status == ReviewCommentStatus.Open);
        bool allApproved = reviews.All(r => r.Approved);
        bool hasRejections = reviews.Any(r => !r.Approved);

        if (hasRejections || hasOpenComments)
            return ReviewStatus.ChangesRequested;

        if (config.RequireAllReviewersApprove)
        {
            if (allApproved && reviews.Count > 0)
                return ReviewStatus.Approved;
        }
        else
        {
            if (reviews.Count(r => r.Approved) >= config.MinimumRequiredApprovals)
                return ReviewStatus.Approved;
        }

        return ReviewStatus.AwaitingReview;
    }

    private string GenerateResolution(string commentContent)
    {
        // Simple resolution generation
        // In production, this could use an LLM to generate proper responses
        return $"Addressed: {commentContent}";
    }
}
