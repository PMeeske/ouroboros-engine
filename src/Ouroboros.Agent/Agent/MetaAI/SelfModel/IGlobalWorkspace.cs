#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Global Workspace Interface
// Phase 2: Shared working memory with attention policies
// ==========================================================

namespace Ouroboros.Agent.MetaAI.SelfModel;

/// <summary>
/// Interface for global workspace management.
/// Implements shared working memory with attention-based policies.
/// </summary>
public interface IGlobalWorkspace
{
    /// <summary>
    /// Adds an item to the workspace.
    /// </summary>
    /// <param name="content">Item content</param>
    /// <param name="priority">Priority level</param>
    /// <param name="source">Source of the item</param>
    /// <param name="tags">Tags for categorization</param>
    /// <param name="lifetime">Optional custom lifetime</param>
    /// <returns>The created workspace item</returns>
    WorkspaceItem AddItem(
        string content,
        WorkspacePriority priority,
        string source,
        List<string>? tags = null,
        TimeSpan? lifetime = null);

    /// <summary>
    /// Gets items currently in the workspace, ordered by attention weight.
    /// </summary>
    /// <param name="minPriority">Minimum priority filter</param>
    /// <returns>List of workspace items ordered by attention weight</returns>
    List<WorkspaceItem> GetItems(WorkspacePriority minPriority = WorkspacePriority.Low);

    /// <summary>
    /// Gets high-priority items requiring immediate attention.
    /// </summary>
    /// <returns>List of high-priority items</returns>
    List<WorkspaceItem> GetHighPriorityItems();

    /// <summary>
    /// Removes an item from the workspace.
    /// </summary>
    /// <param name="itemId">Item ID to remove</param>
    /// <returns>True if removed, false if not found</returns>
    bool RemoveItem(Guid itemId);

    /// <summary>
    /// Broadcasts a high-priority item to all listeners.
    /// </summary>
    /// <param name="item">Item to broadcast</param>
    /// <param name="reason">Reason for broadcast</param>
    void BroadcastItem(WorkspaceItem item, string reason);

    /// <summary>
    /// Gets recent broadcasts.
    /// </summary>
    /// <param name="count">Number of broadcasts to retrieve</param>
    /// <returns>List of recent broadcasts</returns>
    List<WorkspaceBroadcast> GetRecentBroadcasts(int count = 10);

    /// <summary>
    /// Searches workspace items by tags.
    /// </summary>
    /// <param name="tags">Tags to search for</param>
    /// <returns>Items matching any of the tags</returns>
    List<WorkspaceItem> SearchByTags(List<string> tags);

    /// <summary>
    /// Cleans up expired items and applies attention policies.
    /// </summary>
    void ApplyAttentionPolicies();

    /// <summary>
    /// Gets workspace statistics.
    /// </summary>
    /// <returns>Current workspace statistics</returns>
    WorkspaceStatistics GetStatistics();
}