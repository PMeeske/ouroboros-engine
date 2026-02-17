namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Result for a single variant in the experiment.
/// </summary>
public sealed record VariantResult(
    string VariantId,
    List<PromptResult> PromptResults,
    VariantMetrics Metrics);