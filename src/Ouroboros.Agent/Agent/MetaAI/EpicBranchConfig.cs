namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Configuration for epic branch orchestration.
/// </summary>
public sealed record EpicBranchConfig(
    string BranchPrefix = "epic",
    string AgentPoolPrefix = "sub-issue-agent",
    bool AutoCreateBranches = true,
    bool AutoAssignAgents = true,
    int MaxConcurrentSubIssues = 5);