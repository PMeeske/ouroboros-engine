namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Interface for epic branch orchestration.
/// </summary>
public interface IEpicBranchOrchestrator
{
    /// <summary>
    /// Registers an epic and creates assignments for all sub-issues.
    /// </summary>
    Task<Result<Epic, string>> RegisterEpicAsync(
        int epicNumber,
        string epicTitle,
        string epicDescription,
        List<int> subIssueNumbers,
        CancellationToken ct = default);

    /// <summary>
    /// Assigns an agent to a sub-issue and creates a dedicated branch.
    /// </summary>
    Task<Result<SubIssueAssignment, string>> AssignSubIssueAsync(
        int epicNumber,
        int subIssueNumber,
        string? preferredAgentId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets all sub-issue assignments for an epic.
    /// </summary>
    IReadOnlyList<SubIssueAssignment> GetSubIssueAssignments(int epicNumber);

    /// <summary>
    /// Gets a specific sub-issue assignment.
    /// </summary>
    SubIssueAssignment? GetSubIssueAssignment(int epicNumber, int subIssueNumber);

    /// <summary>
    /// Updates the status of a sub-issue.
    /// </summary>
    Result<SubIssueAssignment, string> UpdateSubIssueStatus(
        int epicNumber,
        int subIssueNumber,
        SubIssueStatus status,
        string? errorMessage = null);

    /// <summary>
    /// Executes work on a sub-issue using its assigned agent and branch.
    /// </summary>
    Task<Result<SubIssueAssignment, string>> ExecuteSubIssueAsync(
        int epicNumber,
        int subIssueNumber,
        Func<SubIssueAssignment, Task<Result<SubIssueAssignment, string>>> workFunc,
        CancellationToken ct = default);
}