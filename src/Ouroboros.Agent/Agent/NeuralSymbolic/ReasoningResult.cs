#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Reasoning Result Type Definitions
// Represents results of hybrid reasoning operations
// ==========================================================

namespace Ouroboros.Agent.NeuralSymbolic;

/// <summary>
/// Result of a hybrid reasoning operation.
/// </summary>
public sealed record ReasoningResult(
    string Query,
    string Answer,
    ReasoningMode ModeUsed,
    List<ReasoningStep> Steps,
    double Confidence,
    bool SymbolicSucceeded,
    bool NeuralSucceeded,
    TimeSpan Duration);