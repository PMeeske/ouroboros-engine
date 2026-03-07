namespace Ouroboros.Tests.Pipeline.Retrieval;

using LangChain.DocumentLoaders;
using Ouroboros.Pipeline.Retrieval;

[Trait("Category", "Unit")]
public class HybridRetrievalResultTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var symbolic = new List<string> { "doc1", "doc2" };
        var semantic = new List<Document>
        {
            new() { PageContent = "Hello world", Metadata = new Dictionary<string, object> { { "id", "doc3" } } },
        };

        var result = new HybridRetrievalResult("test query", symbolic, semantic);

        result.Query.Should().Be("test query");
        result.SymbolicMatches.Should().HaveCount(2);
        result.SemanticMatches.Should().HaveCount(1);
    }

    [Fact]
    public void AllDocumentIds_CombinesBothSourcesAndDeduplicates()
    {
        var symbolic = new List<string> { "doc1" };
        var semantic = new List<Document>
        {
            new() { PageContent = "content", Metadata = new Dictionary<string, object> { { "id", "doc1" } } },
            new() { PageContent = "other", Metadata = new Dictionary<string, object> { { "id", "doc2" } } },
        };

        var result = new HybridRetrievalResult("query", symbolic, semantic);

        result.AllDocumentIds.Should().HaveCount(2);
    }

    [Fact]
    public void AllDocumentIds_PrioritizesSymbolicMatches()
    {
        var symbolic = new List<string> { "doc1" };
        var semantic = new List<Document>
        {
            new() { PageContent = "content", Metadata = new Dictionary<string, object> { { "id", "doc2" } } },
        };

        var result = new HybridRetrievalResult("query", symbolic, semantic);
        var ids = result.AllDocumentIds.ToList();

        ids[0].Should().Be("doc1");
    }
}
