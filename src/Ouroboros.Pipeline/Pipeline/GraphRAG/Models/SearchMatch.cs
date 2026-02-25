namespace Ouroboros.Pipeline.GraphRAG.Models;

/// <summary>
/// Represents a single match from the search.
/// </summary>
/// <param name="EntityId">The ID of the matched entity.</param>
/// <param name="EntityName">The name of the matched entity.</param>
/// <param name="EntityType">The type of the matched entity.</param>
/// <param name="Content">The text content associated with the match.</param>
/// <param name="Relevance">The combined relevance score (0.0 to 1.0).</param>
/// <param name="VectorScore">The vector similarity score.</param>
/// <param name="SymbolicScore">The symbolic reasoning score.</param>
public sealed record SearchMatch(
    string EntityId,
    string EntityName,
    string EntityType,
    string Content,
    double Relevance,
    double VectorScore,
    double SymbolicScore);