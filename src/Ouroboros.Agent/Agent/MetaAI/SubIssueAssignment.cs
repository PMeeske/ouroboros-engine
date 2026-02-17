namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents a sub-issue in an epic with its assigned agent and branch.
/// </summary>
public sealed record SubIssueAssignment(
    int IssueNumber,
    string Title,
    string Description,
    string AssignedAgentId,
    string BranchName,
    PipelineBranch? Branch,
    SubIssueStatus Status,
    DateTime CreatedAt,
    DateTime? CompletedAt = null,
    string? ErrorMessage = null);