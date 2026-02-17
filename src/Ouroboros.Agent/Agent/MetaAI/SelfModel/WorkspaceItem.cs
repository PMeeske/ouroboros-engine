namespace Ouroboros.Agent.MetaAI.SelfModel;

/// <summary>
/// Represents an item in the global workspace.
/// </summary>
public sealed record WorkspaceItem(
    Guid Id,
    string Content,
    WorkspacePriority Priority,
    string Source,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    List<string> Tags,
    Dictionary<string, object> Metadata)
{
    /// <summary>
    /// Gets the attention weight based on priority, recency, and expiration.
    /// </summary>
    public double GetAttentionWeight()
    {
        double priorityWeight = (int)Priority / 3.0; // 0.0 to 1.0
        double recencyWeight = 1.0 - Math.Min(1.0, (DateTime.UtcNow - CreatedAt).TotalHours / 24.0);
        double urgencyWeight = ExpiresAt < DateTime.UtcNow.AddHours(1) ? 1.0 : 0.0;
        
        return (priorityWeight * 0.5) + (recencyWeight * 0.3) + (urgencyWeight * 0.2);
    }
}