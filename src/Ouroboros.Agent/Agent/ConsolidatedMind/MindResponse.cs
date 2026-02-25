namespace Ouroboros.Agent.ConsolidatedMind;

/// <summary>
/// Result of a ConsolidatedMind execution.
/// </summary>
/// <param name="Response">The generated response.</param>
/// <param name="ThinkingContent">Optional thinking/reasoning content if available.</param>
/// <param name="UsedRoles">Roles that contributed to this response.</param>
/// <param name="ExecutionTimeMs">Total execution time in milliseconds.</param>
/// <param name="WasVerified">Whether the response was verified.</param>
/// <param name="Confidence">Overall confidence in the response.</param>
public sealed record MindResponse(
    string Response,
    string? ThinkingContent,
    SpecializedRole[] UsedRoles,
    double ExecutionTimeMs,
    bool WasVerified,
    double Confidence);