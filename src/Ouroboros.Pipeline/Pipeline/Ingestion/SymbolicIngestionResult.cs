using Ouroboros.Pipeline.Extraction;

namespace Ouroboros.Pipeline.Ingestion;

/// <summary>
/// Result of symbolic ingestion containing both vector and symbolic representations.
/// </summary>
/// <param name="DocumentId">The document identifier.</param>
/// <param name="VectorIds">The IDs of vectors stored in the vector store.</param>
/// <param name="Triples">The semantic triples stored in the AtomSpace.</param>
public sealed record SymbolicIngestionResult(
    string DocumentId,
    IReadOnlyList<string> VectorIds,
    IReadOnlyList<SemanticTriple> Triples);