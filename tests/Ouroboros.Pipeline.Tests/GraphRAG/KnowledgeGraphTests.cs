using Ouroboros.Pipeline.GraphRAG.Models;

namespace Ouroboros.Tests.GraphRAG;

[Trait("Category", "Unit")]
public sealed class KnowledgeGraphTests
{
    private static Entity CreateEntity(string id, string type = "Person", string name = "Test") =>
        Entity.Create(id, type, name);

    private static Relationship CreateRelationship(string id, string type, string sourceId, string targetId) =>
        Relationship.Create(id, type, sourceId, targetId);

    #region Empty

    [Fact]
    public void Empty_HasNoEntitiesOrRelationships()
    {
        // Arrange & Act
        var graph = KnowledgeGraph.Empty;

        // Assert
        graph.Entities.Should().BeEmpty();
        graph.Relationships.Should().BeEmpty();
    }

    #endregion

    #region GetEntity

    [Fact]
    public void GetEntity_WithExistingId_ReturnsEntity()
    {
        // Arrange
        var entity = CreateEntity("e1", "Person", "Alice");
        var graph = KnowledgeGraph.Empty.WithEntity(entity);

        // Act
        var result = graph.GetEntity("e1");

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Alice");
    }

    [Fact]
    public void GetEntity_WithNonExistentId_ReturnsNull()
    {
        // Arrange
        var graph = KnowledgeGraph.Empty;

        // Act
        var result = graph.GetEntity("nonexistent");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetRelationships

    [Fact]
    public void GetRelationships_WithSourceEntity_ReturnsMatchingRelationships()
    {
        // Arrange
        var graph = KnowledgeGraph.Empty
            .WithEntity(CreateEntity("e1"))
            .WithEntity(CreateEntity("e2"))
            .WithRelationship(CreateRelationship("r1", "WorksFor", "e1", "e2"));

        // Act
        var result = graph.GetRelationships("e1").ToList();

        // Assert
        result.Should().HaveCount(1);
        result[0].Id.Should().Be("r1");
    }

    [Fact]
    public void GetRelationships_WithTargetEntity_ReturnsMatchingRelationships()
    {
        // Arrange
        var graph = KnowledgeGraph.Empty
            .WithEntity(CreateEntity("e1"))
            .WithEntity(CreateEntity("e2"))
            .WithRelationship(CreateRelationship("r1", "WorksFor", "e1", "e2"));

        // Act
        var result = graph.GetRelationships("e2").ToList();

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public void GetRelationships_WithUnrelatedEntity_ReturnsEmpty()
    {
        // Arrange
        var graph = KnowledgeGraph.Empty
            .WithEntity(CreateEntity("e1"))
            .WithEntity(CreateEntity("e2"))
            .WithRelationship(CreateRelationship("r1", "WorksFor", "e1", "e2"));

        // Act
        var result = graph.GetRelationships("e3").ToList();

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetEntitiesByType

    [Fact]
    public void GetEntitiesByType_WithMatchingType_ReturnsFilteredEntities()
    {
        // Arrange
        var graph = KnowledgeGraph.Empty
            .WithEntity(CreateEntity("e1", "Person", "Alice"))
            .WithEntity(CreateEntity("e2", "Organization", "Acme"))
            .WithEntity(CreateEntity("e3", "Person", "Bob"));

        // Act
        var result = graph.GetEntitiesByType("Person").ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Select(e => e.Name).Should().BeEquivalentTo("Alice", "Bob");
    }

    [Fact]
    public void GetEntitiesByType_IsCaseInsensitive()
    {
        // Arrange
        var graph = KnowledgeGraph.Empty
            .WithEntity(CreateEntity("e1", "Person", "Alice"));

        // Act
        var result = graph.GetEntitiesByType("person").ToList();

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public void GetEntitiesByType_WithNoMatch_ReturnsEmpty()
    {
        // Arrange
        var graph = KnowledgeGraph.Empty
            .WithEntity(CreateEntity("e1", "Person", "Alice"));

        // Act
        var result = graph.GetEntitiesByType("Organization").ToList();

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetRelationshipsByType

    [Fact]
    public void GetRelationshipsByType_WithMatchingType_ReturnsFilteredRelationships()
    {
        // Arrange
        var graph = KnowledgeGraph.Empty
            .WithRelationship(CreateRelationship("r1", "WorksFor", "e1", "e2"))
            .WithRelationship(CreateRelationship("r2", "LocatedIn", "e1", "e3"))
            .WithRelationship(CreateRelationship("r3", "WorksFor", "e3", "e2"));

        // Act
        var result = graph.GetRelationshipsByType("WorksFor").ToList();

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public void GetRelationshipsByType_IsCaseInsensitive()
    {
        // Arrange
        var graph = KnowledgeGraph.Empty
            .WithRelationship(CreateRelationship("r1", "WorksFor", "e1", "e2"));

        // Act
        var result = graph.GetRelationshipsByType("worksfor").ToList();

        // Assert
        result.Should().HaveCount(1);
    }

    #endregion

    #region WithEntity

    [Fact]
    public void WithEntity_ReturnsNewGraphWithAddedEntity()
    {
        // Arrange
        var graph = KnowledgeGraph.Empty;
        var entity = CreateEntity("e1", "Person", "Alice");

        // Act
        var result = graph.WithEntity(entity);

        // Assert
        result.Entities.Should().HaveCount(1);
        result.Entities[0].Name.Should().Be("Alice");
        graph.Entities.Should().BeEmpty("original should be unchanged");
    }

    [Fact]
    public void WithEntity_PreservesExistingEntities()
    {
        // Arrange
        var graph = KnowledgeGraph.Empty
            .WithEntity(CreateEntity("e1", "Person", "Alice"));

        // Act
        var result = graph.WithEntity(CreateEntity("e2", "Person", "Bob"));

        // Assert
        result.Entities.Should().HaveCount(2);
    }

    [Fact]
    public void WithEntity_PreservesRelationships()
    {
        // Arrange
        var graph = KnowledgeGraph.Empty
            .WithRelationship(CreateRelationship("r1", "WorksFor", "e1", "e2"));

        // Act
        var result = graph.WithEntity(CreateEntity("e1"));

        // Assert
        result.Relationships.Should().HaveCount(1);
    }

    #endregion

    #region WithRelationship

    [Fact]
    public void WithRelationship_ReturnsNewGraphWithAddedRelationship()
    {
        // Arrange
        var graph = KnowledgeGraph.Empty;
        var rel = CreateRelationship("r1", "WorksFor", "e1", "e2");

        // Act
        var result = graph.WithRelationship(rel);

        // Assert
        result.Relationships.Should().HaveCount(1);
        graph.Relationships.Should().BeEmpty("original should be unchanged");
    }

    [Fact]
    public void WithRelationship_PreservesEntities()
    {
        // Arrange
        var graph = KnowledgeGraph.Empty
            .WithEntity(CreateEntity("e1"));

        // Act
        var result = graph.WithRelationship(CreateRelationship("r1", "WorksFor", "e1", "e2"));

        // Assert
        result.Entities.Should().HaveCount(1);
    }

    #endregion

    #region Merge

    [Fact]
    public void Merge_CombinesEntitiesFromBothGraphs()
    {
        // Arrange
        var graph1 = KnowledgeGraph.Empty
            .WithEntity(CreateEntity("e1", "Person", "Alice"));
        var graph2 = KnowledgeGraph.Empty
            .WithEntity(CreateEntity("e2", "Person", "Bob"));

        // Act
        var result = graph1.Merge(graph2);

        // Assert
        result.Entities.Should().HaveCount(2);
    }

    [Fact]
    public void Merge_DeduplicatesEntitiesById()
    {
        // Arrange
        var graph1 = KnowledgeGraph.Empty
            .WithEntity(CreateEntity("e1", "Person", "Alice"));
        var graph2 = KnowledgeGraph.Empty
            .WithEntity(CreateEntity("e1", "Person", "Alice Updated"));

        // Act
        var result = graph1.Merge(graph2);

        // Assert
        result.Entities.Should().HaveCount(1);
        result.Entities[0].Name.Should().Be("Alice"); // original is kept
    }

    [Fact]
    public void Merge_CombinesRelationshipsFromBothGraphs()
    {
        // Arrange
        var graph1 = KnowledgeGraph.Empty
            .WithRelationship(CreateRelationship("r1", "WorksFor", "e1", "e2"));
        var graph2 = KnowledgeGraph.Empty
            .WithRelationship(CreateRelationship("r2", "LocatedIn", "e1", "e3"));

        // Act
        var result = graph1.Merge(graph2);

        // Assert
        result.Relationships.Should().HaveCount(2);
    }

    [Fact]
    public void Merge_DeduplicatesRelationshipsById()
    {
        // Arrange
        var graph1 = KnowledgeGraph.Empty
            .WithRelationship(CreateRelationship("r1", "WorksFor", "e1", "e2"));
        var graph2 = KnowledgeGraph.Empty
            .WithRelationship(CreateRelationship("r1", "WorksFor", "e1", "e2"));

        // Act
        var result = graph1.Merge(graph2);

        // Assert
        result.Relationships.Should().HaveCount(1);
    }

    [Fact]
    public void Merge_WithEmptyGraph_ReturnsSameContent()
    {
        // Arrange
        var graph = KnowledgeGraph.Empty
            .WithEntity(CreateEntity("e1"))
            .WithRelationship(CreateRelationship("r1", "Type", "e1", "e2"));

        // Act
        var result = graph.Merge(KnowledgeGraph.Empty);

        // Assert
        result.Entities.Should().HaveCount(1);
        result.Relationships.Should().HaveCount(1);
    }

    #endregion

    #region Traverse

    [Fact]
    public void Traverse_FromStartEntity_ReturnsSubgraph()
    {
        // Arrange
        var graph = KnowledgeGraph.Empty
            .WithEntity(CreateEntity("e1", "Person", "Alice"))
            .WithEntity(CreateEntity("e2", "Organization", "Acme"))
            .WithEntity(CreateEntity("e3", "Location", "Berlin"))
            .WithRelationship(CreateRelationship("r1", "WorksFor", "e1", "e2"))
            .WithRelationship(CreateRelationship("r2", "LocatedIn", "e2", "e3"));

        // Act
        var subgraph = graph.Traverse("e1", maxHops: 2);

        // Assert
        subgraph.Entities.Should().HaveCount(3);
        subgraph.Relationships.Should().HaveCount(2);
    }

    [Fact]
    public void Traverse_WithMaxHopsOne_ReturnsDirectNeighborsOnly()
    {
        // Arrange
        var graph = KnowledgeGraph.Empty
            .WithEntity(CreateEntity("e1", "Person", "Alice"))
            .WithEntity(CreateEntity("e2", "Organization", "Acme"))
            .WithEntity(CreateEntity("e3", "Location", "Berlin"))
            .WithRelationship(CreateRelationship("r1", "WorksFor", "e1", "e2"))
            .WithRelationship(CreateRelationship("r2", "LocatedIn", "e2", "e3"));

        // Act
        var subgraph = graph.Traverse("e1", maxHops: 1);

        // Assert
        subgraph.Entities.Should().HaveCount(2); // e1 and e2
        subgraph.Relationships.Should().HaveCount(1); // r1 only
    }

    [Fact]
    public void Traverse_WithMaxHopsZero_ReturnsStartEntityOnly()
    {
        // Arrange
        var graph = KnowledgeGraph.Empty
            .WithEntity(CreateEntity("e1", "Person", "Alice"))
            .WithEntity(CreateEntity("e2", "Organization", "Acme"))
            .WithRelationship(CreateRelationship("r1", "WorksFor", "e1", "e2"));

        // Act
        var subgraph = graph.Traverse("e1", maxHops: 0);

        // Assert
        subgraph.Entities.Should().HaveCount(1);
        subgraph.Entities[0].Id.Should().Be("e1");
        subgraph.Relationships.Should().BeEmpty();
    }

    [Fact]
    public void Traverse_WithNonExistentStartEntity_ReturnsEmptyEntities()
    {
        // Arrange
        var graph = KnowledgeGraph.Empty
            .WithEntity(CreateEntity("e1"));

        // Act
        var subgraph = graph.Traverse("nonexistent", maxHops: 2);

        // Assert
        subgraph.Entities.Should().BeEmpty();
    }

    [Fact]
    public void Traverse_WithCyclicGraph_DoesNotLoop()
    {
        // Arrange
        var graph = KnowledgeGraph.Empty
            .WithEntity(CreateEntity("e1", "Person", "Alice"))
            .WithEntity(CreateEntity("e2", "Person", "Bob"))
            .WithRelationship(CreateRelationship("r1", "Knows", "e1", "e2"))
            .WithRelationship(CreateRelationship("r2", "Knows", "e2", "e1"));

        // Act
        var subgraph = graph.Traverse("e1", maxHops: 10);

        // Assert
        subgraph.Entities.Should().HaveCount(2);
    }

    [Fact]
    public void Traverse_FollowsBothDirections()
    {
        // Arrange
        var graph = KnowledgeGraph.Empty
            .WithEntity(CreateEntity("e1"))
            .WithEntity(CreateEntity("e2"))
            .WithRelationship(CreateRelationship("r1", "Knows", "e2", "e1")); // e1 is target

        // Act
        var subgraph = graph.Traverse("e1", maxHops: 1);

        // Assert
        subgraph.Entities.Should().HaveCount(2); // follows relationship where e1 is target
    }

    #endregion
}
