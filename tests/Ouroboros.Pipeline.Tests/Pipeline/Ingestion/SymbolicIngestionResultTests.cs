namespace Ouroboros.Tests.Pipeline.Ingestion;

using Ouroboros.Pipeline.Extraction;
using Ouroboros.Pipeline.Ingestion;

[Trait("Category", "Unit")]
public class SymbolicIngestionResultTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var vectorIds = new List<string> { "v1", "v2" };
        var triples = new List<SemanticTriple>
        {
            new("Subject", "predicate", "Object"),
        };

        var result = new SymbolicIngestionResult("doc1", vectorIds, triples);

        result.DocumentId.Should().Be("doc1");
        result.VectorIds.Should().HaveCount(2);
        result.Triples.Should().HaveCount(1);
    }

    [Fact]
    public void Record_SupportsEquality()
    {
        var vectorIds = new List<string> { "v1" };
        var triples = new List<SemanticTriple>();

        var r1 = new SymbolicIngestionResult("doc1", vectorIds, triples);
        var r2 = new SymbolicIngestionResult("doc1", vectorIds, triples);

        r1.Should().Be(r2);
    }

    [Fact]
    public void Constructor_AcceptsEmptyCollections()
    {
        var result = new SymbolicIngestionResult(
            "doc1",
            new List<string>(),
            new List<SemanticTriple>());

        result.VectorIds.Should().BeEmpty();
        result.Triples.Should().BeEmpty();
    }
}
