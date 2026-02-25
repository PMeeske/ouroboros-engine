namespace Ouroboros.Providers;

/// <summary>
/// Type of sub-goal for routing purposes.
/// </summary>
public enum SubGoalType
{
    /// <summary>Factual lookup or recall.</summary>
    Retrieval,
    /// <summary>Text transformation or formatting.</summary>
    Transform,
    /// <summary>Logical reasoning or analysis.</summary>
    Reasoning,
    /// <summary>Creative writing or generation.</summary>
    Creative,
    /// <summary>Code generation or review.</summary>
    Coding,
    /// <summary>Mathematical computation or proof.</summary>
    Math,
    /// <summary>Aggregation or synthesis of other results.</summary>
    Synthesis
}