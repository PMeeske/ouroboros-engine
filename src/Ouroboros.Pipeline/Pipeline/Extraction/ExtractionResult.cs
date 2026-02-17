namespace Ouroboros.Pipeline.Extraction;

/// <summary>
/// Extraction result containing all triples from a document.
/// </summary>
/// <param name="DocumentId">The source document identifier.</param>
/// <param name="Triples">The extracted semantic triples.</param>
public sealed record ExtractionResult(string DocumentId, IReadOnlyList<SemanticTriple> Triples);