namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents an approval request.
/// </summary>
public sealed record ApprovalRequest(
    string RequestId,
    string Action,
    Dictionary<string, object> Parameters,
    string Rationale,
    DateTime RequestedAt);