namespace Ouroboros.Network;

/// <summary>
/// Payload for a step execution node in the MerkleDag.
/// Contains full synopsis of the pipeline token and its execution.
/// </summary>
/// <param name="TokenName">Primary pipeline token name.</param>
/// <param name="Aliases">Alternative names for this token.</param>
/// <param name="SourceClass">Class containing this step.</param>
/// <param name="Description">Human-readable description.</param>
/// <param name="Arguments">Arguments passed to the step.</param>
/// <param name="Synopsis">Formatted execution synopsis.</param>
/// <param name="DurationMs">Execution duration in milliseconds.</param>
/// <param name="Success">Whether execution succeeded.</param>
/// <param name="Error">Error message if failed.</param>
/// <param name="ExecutedAt">When the step was executed.</param>
public sealed record StepExecutionPayload(
    string TokenName,
    string[] Aliases,
    string SourceClass,
    string Description,
    string? Arguments,
    string Synopsis,
    long? DurationMs,
    bool Success,
    string? Error,
    DateTime ExecutedAt);