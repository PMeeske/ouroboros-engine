using Ouroboros.Pipeline.GraphRAG.Models;

namespace Ouroboros.Tests.GraphRAG;

[Trait("Category", "Unit")]
public sealed class SearchMatchTests
{
    [Fact]
    public void Constructor_WithAllParameters_SetsProperties()
    {
        // Arrange & Act
        var match = new SearchMatch("e1", "Alice", "Person", "Some content", 0.9, 0.85, 0.95);

        // Assert
        match.EntityId.Should().Be("e1");
        match.EntityName.Should().Be("Alice");
        match.EntityType.Should().Be("Person");
        match.Content.Should().Be("Some content");
        match.Relevance.Should().Be(0.9);
        match.VectorScore.Should().Be(0.85);
        match.SymbolicScore.Should().Be(0.95);
    }

    [Fact]
    public void RecordEquality_WithSameValues_AreEqual()
    {
        // Arrange
        var match1 = new SearchMatch("e1", "Alice", "Person", "Content", 0.9, 0.85, 0.95);
        var match2 = new SearchMatch("e1", "Alice", "Person", "Content", 0.9, 0.85, 0.95);

        // Act & Assert
        match1.Should().Be(match2);
    }

    [Fact]
    public void RecordEquality_WithDifferentRelevance_AreNotEqual()
    {
        // Arrange
        var match1 = new SearchMatch("e1", "Alice", "Person", "Content", 0.9, 0.85, 0.95);
        var match2 = new SearchMatch("e1", "Alice", "Person", "Content", 0.8, 0.85, 0.95);

        // Act & Assert
        match1.Should().NotBe(match2);
    }

    [Fact]
    public void Relevance_WithZeroScore_IsZero()
    {
        // Arrange & Act
        var match = new SearchMatch("e1", "Alice", "Person", "Content", 0.0, 0.0, 0.0);

        // Assert
        match.Relevance.Should().Be(0.0);
    }

    [Fact]
    public void Relevance_WithMaxScore_IsOne()
    {
        // Arrange & Act
        var match = new SearchMatch("e1", "Alice", "Person", "Content", 1.0, 1.0, 1.0);

        // Assert
        match.Relevance.Should().Be(1.0);
    }
}
