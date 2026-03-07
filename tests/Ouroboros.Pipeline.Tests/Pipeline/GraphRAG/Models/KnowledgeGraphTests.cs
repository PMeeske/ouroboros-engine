namespace Ouroboros.Tests.Pipeline.GraphRAG.Models;

using Ouroboros.Pipeline.GraphRAG.Models;

[Trait("Category", "Unit")]
public class KnowledgeGraphTests
{
    [Fact]
    public void Empty_HasNoEntitiesOrRelationships()
    {
        var graph = KnowledgeGraph.Empty;

        graph.Entities.Should().BeEmpty();
        graph.Relationships.Should().BeEmpty();
    }

    [Fact]
    public void GetEntity_ReturnsEntityWhenFound()
    {
        var entity = Entity.Create("e1", "Person", "John");
        var graph = KnowledgeGraph.Empty.WithEntity(entity);

        graph.GetEntity("e1").Should().NotBeNull();
        graph.GetEntity("e1")!.Name.Should().Be("John");
    }

    [Fact]
    public void GetEntity_ReturnsNullWhenNotFound()
    {
        var graph = KnowledgeGraph.Empty;
        graph.GetEntity("nonexistent").Should().BeNull();
    }

    [Fact]
    public void GetRelationships_ReturnsMatchingRelationships()
    {
        var rel = Relationship.Create("r1", "WorksFor", "e1", "e2");
        var graph = KnowledgeGraph.Empty.WithRelationship(rel);

        graph.GetRelationships("e1").Should().HaveCount(1);
        graph.GetRelationships("e2").Should().HaveCount(1);
        graph.GetRelationships("e3").Should().BeEmpty();
    }

    [Fact]
    public void GetEntitiesByType_FiltersByTypeIgnoringCase()
    {
        var e1 = Entity.Create("e1", "Person", "John");
        var e2 = Entity.Create("e2", "person", "Jane");
        var e3 = Entity.Create("e3", "Organization", "Acme");
        var graph = KnowledgeGraph.Empty
            .WithEntity(e1).WithEntity(e2).WithEntity(e3);

        graph.GetEntitiesByType("Person").Should().HaveCount(2);
    }

    [Fact]
    public void GetRelationshipsByType_FiltersByTypeIgnoringCase()
    {
        var r1 = Relationship.Create("r1", "WorksFor", "e1", "e2");
        var r2 = Relationship.Create("r2", "worksfor", "e3", "e4");
        var r3 = Relationship.Create("r3", "Knows", "e1", "e3");
        var graph = KnowledgeGraph.Empty
            .WithRelationship(r1).WithRelationship(r2).WithRelationship(r3);

        graph.GetRelationshipsByType("WorksFor").Should().HaveCount(2);
    }

    [Fact]
    public void WithEntity_AddsEntityAndReturnsNewGraph()
    {
        var graph = KnowledgeGraph.Empty;
        var entity = Entity.Create("e1", "Person", "John");

        var updated = graph.WithEntity(entity);

        updated.Entities.Should().HaveCount(1);
        graph.Entities.Should().BeEmpty();
    }

    [Fact]
    public void WithRelationship_AddsRelationshipAndReturnsNewGraph()
    {
        var graph = KnowledgeGraph.Empty;
        var rel = Relationship.Create("r1", "WorksFor", "e1", "e2");

        var updated = graph.WithRelationship(rel);

        updated.Relationships.Should().HaveCount(1);
        graph.Relationships.Should().BeEmpty();
    }

    [Fact]
    public void Merge_CombinesGraphsWithoutDuplicates()
    {
        var e1 = Entity.Create("e1", "Person", "John");
        var e2 = Entity.Create("e2", "Person", "Jane");
        var r1 = Relationship.Create("r1", "Knows", "e1", "e2");

        var graph1 = KnowledgeGraph.Empty.WithEntity(e1).WithRelationship(r1);
        var graph2 = KnowledgeGraph.Empty.WithEntity(e1).WithEntity(e2);

        var merged = graph1.Merge(graph2);

        merged.Entities.Should().HaveCount(2);
        merged.Relationships.Should().HaveCount(1);
    }

    [Fact]
    public void Traverse_ReturnsSubgraphWithinMaxHops()
    {
        var e1 = Entity.Create("e1", "A", "A");
        var e2 = Entity.Create("e2", "B", "B");
        var e3 = Entity.Create("e3", "C", "C");
        var r1 = Relationship.Create("r1", "Link", "e1", "e2");
        var r2 = Relationship.Create("r2", "Link", "e2", "e3");

        var graph = new KnowledgeGraph(
            new List<Entity> { e1, e2, e3 },
            new List<Relationship> { r1, r2 });

        var sub1 = graph.Traverse("e1", 1);
        sub1.Entities.Should().HaveCount(2);

        var sub2 = graph.Traverse("e1", 2);
        sub2.Entities.Should().HaveCount(3);
    }

    [Fact]
    public void Traverse_WithZeroHops_ReturnsOnlyStartEntity()
    {
        var e1 = Entity.Create("e1", "A", "A");
        var e2 = Entity.Create("e2", "B", "B");
        var r1 = Relationship.Create("r1", "Link", "e1", "e2");

        var graph = new KnowledgeGraph(
            new List<Entity> { e1, e2 },
            new List<Relationship> { r1 });

        var sub = graph.Traverse("e1", 0);
        sub.Entities.Should().HaveCount(1);
        sub.Entities[0].Id.Should().Be("e1");
    }
}
