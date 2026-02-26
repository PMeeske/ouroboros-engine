namespace Ouroboros.Tests.Pipeline.GraphRAG.Models;

using Ouroboros.Pipeline.GraphRAG.Models;

[Trait("Category", "Unit")]
public class RelationshipTests
{
    [Fact]
    public void Create_ReturnsRelationshipWithEmptyProperties()
    {
        var rel = Relationship.Create("r1", "WorksFor", "e1", "e2");

        rel.Id.Should().Be("r1");
        rel.Type.Should().Be("WorksFor");
        rel.SourceEntityId.Should().Be("e1");
        rel.TargetEntityId.Should().Be("e2");
        rel.Properties.Should().BeEmpty();
    }

    [Fact]
    public void WithProperty_AddsPropertyAndReturnsNewRelationship()
    {
        var rel = Relationship.Create("r1", "WorksFor", "e1", "e2");
        var updated = rel.WithProperty("since", 2020);

        updated.Properties.Should().ContainKey("since");
        rel.Properties.Should().BeEmpty();
    }

    [Fact]
    public void Weight_DefaultsToOne() =>
        Relationship.Create("r1", "T", "a", "b").Weight.Should().Be(1.0);

    [Fact]
    public void IsBidirectional_DefaultsToFalse() =>
        Relationship.Create("r1", "T", "a", "b").IsBidirectional.Should().BeFalse();
}
