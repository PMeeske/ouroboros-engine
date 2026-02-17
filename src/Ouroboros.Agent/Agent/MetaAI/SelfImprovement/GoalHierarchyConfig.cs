namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Configuration for goal hierarchy behavior.
/// </summary>
public sealed record GoalHierarchyConfig(
    int MaxDepth = 3,
    int MaxSubgoalsPerGoal = 5,
    List<string> SafetyConstraints = null!,
    List<string> CoreValues = null!)
{
    public GoalHierarchyConfig() : this(
        3,
        5,
        new List<string>
        {
            "Do not harm users",
            "Respect user privacy",
            "Operate within permissions",
            "Be transparent about limitations"
        },
        new List<string>
        {
            "Helpfulness",
            "Harmlessness",
            "Honesty",
            "Accuracy"
        })
    {
    }
}