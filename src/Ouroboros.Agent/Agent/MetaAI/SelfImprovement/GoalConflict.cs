namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Result of goal conflict detection.
/// </summary>
public sealed record GoalConflict(
    Goal Goal1,
    Goal Goal2,
    string ConflictType,
    string Description,
    List<string> SuggestedResolutions);