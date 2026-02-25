namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents a human feedback request.
/// </summary>
public sealed record HumanFeedbackRequest(
    string RequestId,
    string Context,
    string Question,
    List<string>? Options,
    DateTime RequestedAt,
    TimeSpan Timeout);