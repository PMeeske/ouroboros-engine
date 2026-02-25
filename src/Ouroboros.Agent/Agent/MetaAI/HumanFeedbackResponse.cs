namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents human feedback response.
/// </summary>
public sealed record HumanFeedbackResponse(
    string RequestId,
    string Response,
    Dictionary<string, object>? Metadata,
    DateTime RespondedAt);