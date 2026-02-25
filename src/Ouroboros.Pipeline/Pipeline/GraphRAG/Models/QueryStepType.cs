namespace Ouroboros.Pipeline.GraphRAG.Models;

/// <summary>
/// Types of steps in a query plan.
/// </summary>
public enum QueryStepType
{
    /// <summary>Vector similarity search.</summary>
    VectorSearch,

    /// <summary>Graph traversal to find related entities.</summary>
    GraphTraversal,

    /// <summary>Symbolic pattern matching.</summary>
    SymbolicMatch,

    /// <summary>Entity type filtering.</summary>
    TypeFilter,

    /// <summary>Property-based filtering.</summary>
    PropertyFilter,

    /// <summary>Result aggregation.</summary>
    Aggregate,

    /// <summary>Result ranking and sorting.</summary>
    Rank,

    /// <summary>Logical inference step.</summary>
    Inference
}