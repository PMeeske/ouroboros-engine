// ==========================================================
// MeTTa Expression Type Definitions
// Represents parsed MeTTa expressions
// ==========================================================

namespace Ouroboros.Agent.NeuralSymbolic;

/// <summary>
/// Represents a MeTTa expression with metadata.
/// </summary>
public sealed record MeTTaExpression(
    string RawExpression,
    ExpressionType Type,
    List<string> Symbols,
    List<string> Variables,
    Dictionary<string, object> Metadata);