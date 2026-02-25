namespace Ouroboros.Agent.NeuralSymbolic;

/// <summary>
/// Type of MeTTa expression.
/// </summary>
public enum ExpressionType
{
    /// <summary>Atomic expression.</summary>
    Atom,

    /// <summary>Variable expression.</summary>
    Variable,

    /// <summary>Compound expression.</summary>
    Expression,

    /// <summary>Function expression.</summary>
    Function,

    /// <summary>Rule expression.</summary>
    Rule,

    /// <summary>Query expression.</summary>
    Query
}