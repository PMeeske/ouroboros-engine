namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents the confidence level of the Ouroboros system for a given action.
/// </summary>
public enum OuroborosConfidence
{
    /// <summary>High confidence - action can proceed without validation.</summary>
    High,

    /// <summary>Medium confidence - action requires validation before proceeding.</summary>
    Medium,

    /// <summary>Low confidence - action requires validation and possibly human review.</summary>
    Low,
}