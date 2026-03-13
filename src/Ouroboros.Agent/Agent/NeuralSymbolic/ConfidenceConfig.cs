// ==========================================================
// Confidence Configuration for Neural-Symbolic Bridge
// Centralizes heuristic confidence scores for calibration
// ==========================================================

namespace Ouroboros.Agent.NeuralSymbolic;

/// <summary>
/// Configuration for confidence scoring in neural-symbolic hybrid reasoning.
/// Default values are initial heuristics — calibrate empirically for production.
/// </summary>
public sealed record ConfidenceConfig
{
    /// <summary>
    /// Confidence when symbolic verification confirms neural result.
    /// </summary>
    public double SymbolicVerifiedNeural { get; init; } = 0.9;

    /// <summary>
    /// Confidence when symbolic verification cannot confirm neural result.
    /// </summary>
    public double UnverifiedNeural { get; init; } = 0.6;

    /// <summary>
    /// Confidence when both symbolic and neural produce agreeing results.
    /// </summary>
    public double ParallelAgreement { get; init; } = 0.95;

    /// <summary>
    /// Base confidence for grounding operations.
    /// </summary>
    public double BaseGrounding { get; init; } = 0.8;

    /// <summary>
    /// Confidence reduction per consistency conflict detected.
    /// </summary>
    public double ConflictPenalty { get; init; } = 0.2;
}
