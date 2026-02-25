namespace Ouroboros.Agent.NeuralSymbolic;

/// <summary>
/// Type of reasoning step.
/// </summary>
public enum ReasoningStepType
{
    /// <summary>Symbolic deduction step.</summary>
    SymbolicDeduction,

    /// <summary>Symbolic induction step.</summary>
    SymbolicInduction,

    /// <summary>Neural inference step.</summary>
    NeuralInference,

    /// <summary>Combined neural-symbolic step.</summary>
    Combination
}