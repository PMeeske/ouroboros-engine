namespace Ouroboros.Agent.NeuralSymbolic;

/// <summary>
/// Mode for hybrid reasoning operations.
/// </summary>
public enum ReasoningMode
{
    /// <summary>Try symbolic first, fall back to neural.</summary>
    SymbolicFirst,

    /// <summary>Try neural first, verify with symbolic.</summary>
    NeuralFirst,

    /// <summary>Run both in parallel, combine results.</summary>
    Parallel,

    /// <summary>Use only symbolic reasoning.</summary>
    SymbolicOnly,

    /// <summary>Use only neural reasoning.</summary>
    NeuralOnly
}