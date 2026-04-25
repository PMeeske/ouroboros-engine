using Ouroboros.Domain.Vectors;
using Ouroboros.Domain.TextSplitters;

namespace Ouroboros.Pipeline.Ingestion;

/// <summary>
/// Provides a single-pass helper for loading documents into an in-memory vector store.
/// </summary>
public static class InMemoryIngestion
{
    /// <summary>
    /// Loads documents from <paramref name="source"/> using <typeparamref name="TLoader"/>, splits them
    /// into chunks, embeds each chunk, and upserts the resulting vectors into <paramref name="store"/>.
    /// </summary>
    /// <typeparam name="TLoader">The document loader type, instantiated with a parameterless constructor.</typeparam>
    /// <param name="store">The target vector store to receive the embedded chunks.</param>
    /// <param name="embedding">The embedding model used to convert text chunks into float vectors.</param>
    /// <param name="source">The data source (file path, URL, etc.) passed to the loader.</param>
    /// <param name="splitter">Text splitter that controls chunk size and overlap.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The list of vectors that were added to the store.</returns>
    public static async Task<List<Vector>> LoadToMemory<TLoader>(
        IVectorStore store,
    IEmbeddingModel embedding,
        DataSource source,
        ITextSplitter splitter,
        CancellationToken ct = default)
        where TLoader : IDocumentLoader, new()
    {
        TLoader loader = new TLoader();
        List<Vector> vectors = [];

        foreach (Document doc in await loader.LoadAsync(source, cancellationToken: ct).ConfigureAwait(false))
        {
            string text = doc.PageContent;
            if (string.IsNullOrWhiteSpace(text)) continue;

            IReadOnlyList<string> chunks = splitter.SplitText(text);
            int i = 0;
            foreach (string chunk in chunks)
            {
                float[] resp = await embedding.CreateEmbeddingsAsync(chunk, ct).ConfigureAwait(false);
                Vector vec = new Vector()
                {
                    Id = $"{(doc.Metadata != null && doc.Metadata.TryGetValue("path", out object? p) ? p?.ToString() : "doc")}#{i}",
                    Text = chunk,
                    Metadata = new Dictionary<string, object?>(doc.Metadata!)
                    {
                        ["chunkIndex"] = i,
                        ["name"] = doc.Metadata != null && doc.Metadata.TryGetValue("name", out object? n) ? n : null
                    }!,
                    Embedding = resp
                };
                vectors.Add(vec);
                i++;
            }
        }

        await store.AddAsync(vectors, ct).ConfigureAwait(false);
        return vectors;
    }
}
