namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents a review comment on a PR.
/// </summary>
public sealed record ReviewComment(
    string CommentId,
    string ReviewerId,
    string Content,
    ReviewCommentStatus Status,
    DateTime CreatedAt,
    DateTime? ResolvedAt = null);