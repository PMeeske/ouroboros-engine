namespace Ouroboros.Pipeline.GraphRAG.Models;

/// <summary>
/// Represents a logical inference made during symbolic reasoning.
/// </summary>
/// <param name="Premise">The premises used for the inference.</param>
/// <param name="Conclusion">The conclusion drawn.</param>
/// <param name="Confidence">Confidence level of the inference (0.0 to 1.0).</param>
/// <param name="Rule">The rule that was applied.</param>
public sealed record Inference(
    IReadOnlyList<string> Premise,
    string Conclusion,
    double Confidence,
    string? Rule = null);