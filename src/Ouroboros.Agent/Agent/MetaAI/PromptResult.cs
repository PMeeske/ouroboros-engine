namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Result for a single prompt evaluation.
/// </summary>
public sealed record PromptResult(
    string Prompt,
    bool Success,
    double LatencyMs,
    double ConfidenceScore,
    string? SelectedModel,
    string? Error);