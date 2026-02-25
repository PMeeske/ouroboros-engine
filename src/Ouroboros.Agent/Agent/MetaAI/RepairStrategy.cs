namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Defines the strategy for repairing a broken plan.
/// </summary>
public enum RepairStrategy
{
    /// <summary>Full replanning from current state.</summary>
    Replan,
    
    /// <summary>Minimal local fixes to the plan.</summary>
    Patch,
    
    /// <summary>Use similar past repairs as templates.</summary>
    CaseBased,
    
    /// <summary>Undo and retry with alternative decompositions.</summary>
    Backtrack
}