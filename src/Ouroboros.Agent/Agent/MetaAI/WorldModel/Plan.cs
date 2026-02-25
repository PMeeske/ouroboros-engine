namespace Ouroboros.Agent.MetaAI.WorldModel;

/// <summary>
/// Represents a plan generated through imagination-based planning.
/// Integrates with existing Plan type in Ouroboros.
/// </summary>
/// <param name="Description">Description of the plan.</param>
/// <param name="Actions">Sequence of actions to execute.</param>
/// <param name="ExpectedReward">Expected cumulative reward.</param>
/// <param name="Confidence">Confidence in the plan (0-1).</param>
public sealed record Plan(
    string Description,
    List<Action> Actions,
    double ExpectedReward,
    double Confidence);