using LangChain.DocumentLoaders;

namespace Ouroboros.Pipeline.Retrieval;

/// <summary>
/// Result of a hybrid retrieval operation.
/// </summary>
/// <param name="Query">The original query.</param>
/// <param name="SymbolicMatches">Documents matched through symbolic (exact) reasoning.</param>
/// <param name="SemanticMatches">Documents matched through vector similarity.</param>
public sealed record HybridRetrievalResult(
    string Query,
    IReadOnlyList<string> SymbolicMatches,
    IReadOnlyList<Document> SemanticMatches)
{
    /// <summary>
    /// Gets all unique document IDs from both symbolic and semantic matches.
    /// Symbolic matches are prioritized.
    /// </summary>
    public IEnumerable<string> AllDocumentIds =>
        this.SymbolicMatches
            .Concat(this.SemanticMatches.Select(d => GetDocumentId(d)))
            .Distinct();

    /// <summary>
    /// Extracts the document ID from a Document, falling back to a content preview.
    /// </summary>
    private static string GetDocumentId(LangChain.DocumentLoaders.Document doc)
    {
        string fallback = doc.PageContent[..Math.Min(50, doc.PageContent.Length)];
        return doc.Metadata.TryGetValue("id", out var id) ? id?.ToString() ?? fallback : fallback;
    }
}