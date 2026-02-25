namespace Ouroboros.Pipeline.GraphRAG.Models;

/// <summary>
/// Types of queries supported by the hybrid retriever.
/// </summary>
public enum QueryType
{
    /// <summary>Direct entity lookup or simple similarity search.</summary>
    SingleHop,

    /// <summary>Query requiring traversal through multiple relationships.</summary>
    MultiHop,

    /// <summary>Query requiring aggregation of multiple entities.</summary>
    Aggregation,

    /// <summary>Query comparing properties of entities.</summary>
    Comparison
}