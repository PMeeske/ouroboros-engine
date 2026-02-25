namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Defines the level of detail for plan explanations.
/// </summary>
public enum ExplanationLevel
{
    /// <summary>One-line summary of the plan.</summary>
    Brief,
    
    /// <summary>Step-by-step explanation of each action.</summary>
    Detailed,
    
    /// <summary>Explanation of why each step is necessary.</summary>
    Causal,
    
    /// <summary>Explanation of what would happen without each step.</summary>
    Counterfactual
}