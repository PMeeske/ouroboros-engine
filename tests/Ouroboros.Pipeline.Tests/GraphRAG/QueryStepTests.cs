using Ouroboros.Pipeline.GraphRAG.Models;

namespace Ouroboros.Tests.GraphRAG;

[Trait("Category", "Unit")]
public sealed class QueryStepTests
{
    [Fact]
    public void Constructor_WithAllParameters_SetsProperties()
    {
        // Arrange
        var dependencies = new List<int> { 1, 2 };

        // Act
        var step = new QueryStep(3, QueryStepType.GraphTraversal, "find related entities", dependencies);

        // Assert
        step.Order.Should().Be(3);
        step.StepType.Should().Be(QueryStepType.GraphTraversal);
        step.Query.Should().Be("find related entities");
        step.Dependencies.Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public void Constructor_WithEmptyDependencies_SetsEmptyList()
    {
        // Arrange & Act
        var step = new QueryStep(1, QueryStepType.VectorSearch, "search query", new List<int>());

        // Assert
        step.Dependencies.Should().BeEmpty();
    }

    [Fact]
    public void EntityTypeFilter_DefaultsToNull()
    {
        // Arrange & Act
        var step = new QueryStep(1, QueryStepType.TypeFilter, "query", new List<int>());

        // Assert
        step.EntityTypeFilter.Should().BeNull();
    }

    [Fact]
    public void EntityTypeFilter_CanBeSetViaInit()
    {
        // Arrange & Act
        var step = new QueryStep(1, QueryStepType.TypeFilter, "query", new List<int>())
        {
            EntityTypeFilter = new List<string> { "Person", "Organization" }
        };

        // Assert
        step.EntityTypeFilter.Should().HaveCount(2);
        step.EntityTypeFilter.Should().Contain("Person");
        step.EntityTypeFilter.Should().Contain("Organization");
    }

    [Fact]
    public void RelationshipTypeFilter_DefaultsToNull()
    {
        // Arrange & Act
        var step = new QueryStep(1, QueryStepType.GraphTraversal, "query", new List<int>());

        // Assert
        step.RelationshipTypeFilter.Should().BeNull();
    }

    [Fact]
    public void RelationshipTypeFilter_CanBeSetViaInit()
    {
        // Arrange & Act
        var step = new QueryStep(1, QueryStepType.GraphTraversal, "query", new List<int>())
        {
            RelationshipTypeFilter = new List<string> { "WorksFor", "LocatedIn" }
        };

        // Assert
        step.RelationshipTypeFilter.Should().HaveCount(2);
    }

    [Fact]
    public void MaxHops_DefaultsToNull()
    {
        // Arrange & Act
        var step = new QueryStep(1, QueryStepType.GraphTraversal, "query", new List<int>());

        // Assert
        step.MaxHops.Should().BeNull();
    }

    [Fact]
    public void MaxHops_CanBeSetViaInit()
    {
        // Arrange & Act
        var step = new QueryStep(1, QueryStepType.GraphTraversal, "query", new List<int>())
        {
            MaxHops = 5
        };

        // Assert
        step.MaxHops.Should().Be(5);
    }

    [Fact]
    public void RecordEquality_WithSameValues_AreEqual()
    {
        // Arrange
        var deps = new List<int>();
        var step1 = new QueryStep(1, QueryStepType.VectorSearch, "query", deps);
        var step2 = new QueryStep(1, QueryStepType.VectorSearch, "query", deps);

        // Act & Assert
        step1.Should().Be(step2);
    }
}
