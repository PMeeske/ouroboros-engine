namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents an epic with its sub-issues.
/// </summary>
public sealed record Epic(
    int EpicNumber,
    string Title,
    string Description,
    List<int> SubIssueNumbers,
    DateTime CreatedAt);