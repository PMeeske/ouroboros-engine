namespace Ouroboros.Tests.Pipeline;

/// <summary>
/// Mock document loader that returns a single document with predefined content.
/// </summary>
public class SingleDocumentLoader : IDocumentLoader
{
    public Task<IReadOnlyCollection<Document>> LoadAsync(
        DataSource source,
        DocumentLoaderSettings? settings = null,
        CancellationToken cancellationToken = default)
    {
        // Return a moderately long document that will be split into multiple chunks
        var longContent = string.Join(" ", Enumerable.Range(0, 100).Select(i => $"Word{i}"));
        var doc = new Document
        {
            PageContent = longContent,
            Metadata = new Dictionary<string, object>
            {
                ["path"] = "test.txt",
                ["name"] = "test.txt"
            }
        };

        return Task.FromResult<IReadOnlyCollection<Document>>(new[] { doc });
    }
}