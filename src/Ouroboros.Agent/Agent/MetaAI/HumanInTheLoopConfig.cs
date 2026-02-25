namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Configuration for human-in-the-loop.
/// </summary>
public sealed record HumanInTheLoopConfig(
    bool RequireApprovalForCriticalSteps = true,
    bool EnableInteractiveRefinement = true,
    TimeSpan DefaultTimeout = default,
    List<string> CriticalActionPatterns = null!);