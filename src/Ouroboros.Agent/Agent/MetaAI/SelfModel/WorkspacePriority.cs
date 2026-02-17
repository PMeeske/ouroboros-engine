namespace Ouroboros.Agent.MetaAI.SelfModel;

/// <summary>
/// Priority level for workspace items.
/// </summary>
public enum WorkspacePriority
{
    /// <summary>Low priority - background information</summary>
    Low = 0,
    
    /// <summary>Normal priority - standard working memory</summary>
    Normal = 1,
    
    /// <summary>High priority - important information requiring attention</summary>
    High = 2,
    
    /// <summary>Critical priority - urgent information</summary>
    Critical = 3
}