namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Defines the temporal relationship between two tasks.
/// </summary>
public enum TemporalRelation
{
    /// <summary>Task A must complete before Task B starts.</summary>
    Before,
    
    /// <summary>Task A must start after Task B completes.</summary>
    After,
    
    /// <summary>Task A must execute during Task B's execution.</summary>
    During,
    
    /// <summary>Task A and Task B must have overlapping execution.</summary>
    Overlaps,
    
    /// <summary>Task A must finish before Task B starts.</summary>
    MustFinishBefore,
    
    /// <summary>Task A and Task B must execute simultaneously.</summary>
    Simultaneous
}