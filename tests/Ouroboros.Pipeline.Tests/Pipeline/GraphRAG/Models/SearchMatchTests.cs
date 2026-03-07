namespace Ouroboros.Tests.Pipeline.GraphRAG.Models;

using Ouroboros.Pipeline.GraphRAG.Models;

[Trait("Category", "Unit")]
public class SearchMatchTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var match = new SearchMatch("e1", "Entity1", "Person", "Content here", 0.85, 0.9, 0.7);

        match.EntityId.Should().Be("e1");
        match.EntityName.Should().Be("Entity1");
        match.EntityType.Should().Be("Person");
        match.Content.Should().Be("Content here");
        match.Relevance.Should().Be(0.85);
        match.VectorScore.Should().Be(0.9);
        match.SymbolicScore.Should().Be(0.7);
    }

    [Fact]
    public void Record_SupportsEquality()
    {
        var m1 = new SearchMatch("e1", "N", "T", "C", 0.5, 0.6, 0.4);
        var m2 = new SearchMatch("e1", "N", "T", "C", 0.5, 0.6, 0.4);

        m1.Should().Be(m2);
    }
}
