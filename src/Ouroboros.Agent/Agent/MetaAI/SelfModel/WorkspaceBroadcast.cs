namespace Ouroboros.Agent.MetaAI.SelfModel;

/// <summary>
/// Workspace broadcast event.
/// </summary>
public sealed record WorkspaceBroadcast(
    WorkspaceItem Item,
    string BroadcastReason,
    DateTime BroadcastTime);