namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Types of goals in the hierarchy.
/// </summary>
public enum GoalType
{
    /// <summary>Primary goal - the main objective</summary>
    Primary,

    /// <summary>Secondary goal - supporting objective</summary>
    Secondary,

    /// <summary>Instrumental goal - means to achieve other goals</summary>
    Instrumental,

    /// <summary>Safety goal - constraint or boundary condition</summary>
    Safety
}