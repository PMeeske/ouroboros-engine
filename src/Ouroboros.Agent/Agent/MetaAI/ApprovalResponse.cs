namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents an approval response.
/// </summary>
public sealed record ApprovalResponse(
    string RequestId,
    bool Approved,
    string? Reason,
    Dictionary<string, object>? Modifications,
    DateTime RespondedAt);