namespace Ouroboros.Agent.MetaAI.Affect;

/// <summary>
/// Task status.
/// </summary>
public enum TaskStatus
{
    /// <summary>Task is pending execution</summary>
    Pending,
    
    /// <summary>Task is currently executing</summary>
    InProgress,
    
    /// <summary>Task completed successfully</summary>
    Completed,
    
    /// <summary>Task failed</summary>
    Failed,
    
    /// <summary>Task was cancelled</summary>
    Cancelled,
    
    /// <summary>Task is blocked by another task</summary>
    Blocked
}