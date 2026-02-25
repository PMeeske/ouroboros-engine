namespace Ouroboros.Pipeline.Verification;

/// <summary>
/// Represents an action type in the plan.
/// </summary>
public abstract record PlanAction
{
    /// <summary>
    /// Converts the action to a MeTTa atom representation.
    /// </summary>
    /// <returns>The MeTTa atom string.</returns>
    public abstract string ToMeTTaAtom();
}