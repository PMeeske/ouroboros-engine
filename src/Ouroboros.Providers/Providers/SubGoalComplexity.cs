namespace Ouroboros.Providers;

/// <summary>
/// Complexity level of a sub-goal.
/// </summary>
public enum SubGoalComplexity
{
    /// <summary>Simple lookup or transformation.</summary>
    Trivial,
    /// <summary>Straightforward task, single-step reasoning.</summary>
    Simple,
    /// <summary>Multi-step reasoning required.</summary>
    Moderate,
    /// <summary>Complex analysis or creative generation.</summary>
    Complex,
    /// <summary>Requires deep expertise or multi-factor analysis.</summary>
    Expert
}