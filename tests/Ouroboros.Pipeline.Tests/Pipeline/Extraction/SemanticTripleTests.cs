namespace Ouroboros.Tests.Pipeline.Extraction;

using Ouroboros.Pipeline.Extraction;

[Trait("Category", "Unit")]
public class SemanticTripleTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var triple = new SemanticTriple("doc1", "Author", "John");

        triple.Subject.Should().Be("doc1");
        triple.Predicate.Should().Be("Author");
        triple.Object.Should().Be("John");
    }

    [Theory]
    [InlineData("Author", "User")]
    [InlineData("CreatedBy", "User")]
    [InlineData("ModifiedBy", "User")]
    [InlineData("Status", "State")]
    [InlineData("Topic", "Concept")]
    [InlineData("Contains", "Concept")]
    [InlineData("References", "Doc")]
    [InlineData("DependsOn", "Doc")]
    [InlineData("Unknown", "Entity")]
    public void ToMeTTaFact_InfersCorrectObjectType(string predicate, string expectedType)
    {
        var triple = new SemanticTriple("doc1", predicate, "value");
        var fact = triple.ToMeTTaFact();

        fact.Should().Contain($"({expectedType} \"value\")");
        fact.Should().Contain($"(Doc \"doc1\")");
    }
}
