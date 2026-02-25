#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Symbolic Rule Type Definitions
// Represents extracted or learned symbolic rules
// ==========================================================

namespace Ouroboros.Agent.NeuralSymbolic;

/// <summary>
/// Represents a symbolic rule that can be used in reasoning.
/// </summary>
public sealed record SymbolicRule(
    string Name,
    string MeTTaRepresentation,
    string NaturalLanguageDescription,
    List<string> Preconditions,
    List<string> Effects,
    double Confidence,
    RuleSource Source);