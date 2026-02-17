namespace Ouroboros.Agent.MetaAI.SelfModel;

/// <summary>
/// Status of an agent commitment.
/// </summary>
public enum CommitmentStatus
{
    /// <summary>Commitment is planned but not started</summary>
    Planned,
    
    /// <summary>Commitment is in progress</summary>
    InProgress,
    
    /// <summary>Commitment is completed successfully</summary>
    Completed,
    
    /// <summary>Commitment failed</summary>
    Failed,
    
    /// <summary>Commitment was cancelled</summary>
    Cancelled,
    
    /// <summary>Commitment is at risk of missing deadline</summary>
    AtRisk
}