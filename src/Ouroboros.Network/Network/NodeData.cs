namespace Ouroboros.Network;

/// <summary>
/// Internal serialization data for MonadNode.
/// </summary>
internal sealed record NodeData(
    Guid Id,
    string TypeName,
    string PayloadJson,
    DateTimeOffset CreatedAt,
    Guid[] ParentIds,
    string Hash);