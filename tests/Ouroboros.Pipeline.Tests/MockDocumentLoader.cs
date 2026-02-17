namespace Ouroboros.Tests.Pipeline;

/// <summary>
/// Mock document loader for testing purposes.
/// </summary>
public class MockDocumentLoader : IDocumentLoader
{
    private readonly List<Document> _documents;

    public MockDocumentLoader() : this(new List<Document>())
    {
    }

    public MockDocumentLoader(List<Document> documents)
    {
        _documents = documents;
    }

    public Task<IReadOnlyCollection<Document>> LoadAsync(
        DataSource source,
        DocumentLoaderSettings? settings = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyCollection<Document>>(_documents);
    }
}