namespace Ouroboros.Tests.Pipeline.Extraction;

using Ouroboros.Pipeline.Extraction;

[Trait("Category", "Unit")]
public class ExtractionResultTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var triples = new List<SemanticTriple>
        {
            new("doc1", "Author", "John")
        };

        var result = new ExtractionResult("doc1", triples);

        result.DocumentId.Should().Be("doc1");
        result.Triples.Should().HaveCount(1);
    }

    [Fact]
    public void Record_SupportsEquality()
    {
        var triples = new List<SemanticTriple>();
        var r1 = new ExtractionResult("doc1", triples);
        var r2 = new ExtractionResult("doc1", triples);

        r1.Should().Be(r2);
    }
}
