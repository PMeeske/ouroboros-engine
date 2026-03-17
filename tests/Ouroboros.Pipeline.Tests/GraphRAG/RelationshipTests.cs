using Ouroboros.Pipeline.GraphRAG.Models;

namespace Ouroboros.Tests.GraphRAG;

[Trait("Category", "Unit")]
public sealed class RelationshipTests
{
    [Fact]
    public void Constructor_WithAllParameters_SetsProperties()
    {
        // Arrange
        var properties = new Dictionary<string, object> { ["since"] = "2020" };

        // Act
        var rel = new Relationship("r1", "WorksFor", "e1", "e2", properties);

        // Assert
        rel.Id.Should().Be("r1");
        rel.Type.Should().Be("WorksFor");
        rel.SourceEntityId.Should().Be("e1");
        rel.TargetEntityId.Should().Be("e2");
        rel.Properties.Should().ContainKey("since");
    }

    [Fact]
    public void Weight_DefaultsToOne()
    {
        // Arrange & Act
        var rel = Relationship.Create("r1", "WorksFor", "e1", "e2");

        // Assert
        rel.Weight.Should().Be(1.0);
    }

    [Fact]
    public void Weight_CanBeSetViaInit()
    {
        // Arrange & Act
        var rel = Relationship.Create("r1", "WorksFor", "e1", "e2") with { Weight = 0.5 };

        // Assert
        rel.Weight.Should().Be(0.5);
    }

    [Fact]
    public void IsBidirectional_DefaultsToFalse()
    {
        // Arrange & Act
        var rel = Relationship.Create("r1", "WorksFor", "e1", "e2");

        // Assert
        rel.IsBidirectional.Should().BeFalse();
    }

    [Fact]
    public void IsBidirectional_CanBeSetViaInit()
    {
        // Arrange & Act
        var rel = Relationship.Create("r1", "Knows", "e1", "e2") with { IsBidirectional = true };

        // Assert
        rel.IsBidirectional.Should().BeTrue();
    }

    [Fact]
    public void Create_WithMinimalParameters_ReturnsRelationshipWithEmptyProperties()
    {
        // Arrange & Act
        var rel = Relationship.Create("r1", "LocatedIn", "e1", "e2");

        // Assert
        rel.Id.Should().Be("r1");
        rel.Type.Should().Be("LocatedIn");
        rel.SourceEntityId.Should().Be("e1");
        rel.TargetEntityId.Should().Be("e2");
        rel.Properties.Should().BeEmpty();
    }

    [Fact]
    public void WithProperty_AddsPropertyToNewInstance()
    {
        // Arrange
        var rel = Relationship.Create("r1", "WorksFor", "e1", "e2");

        // Act
        var updated = rel.WithProperty("role", "Engineer");

        // Assert
        updated.Properties.Should().ContainKey("role");
        updated.Properties["role"].Should().Be("Engineer");
        rel.Properties.Should().BeEmpty("original should be unchanged");
    }

    [Fact]
    public void WithProperty_OverwritesExistingProperty()
    {
        // Arrange
        var rel = Relationship.Create("r1", "WorksFor", "e1", "e2")
            .WithProperty("role", "Engineer");

        // Act
        var updated = rel.WithProperty("role", "Manager");

        // Assert
        updated.Properties["role"].Should().Be("Manager");
    }

    [Fact]
    public void WithProperty_PreservesExistingProperties()
    {
        // Arrange
        var rel = Relationship.Create("r1", "WorksFor", "e1", "e2")
            .WithProperty("role", "Engineer");

        // Act
        var updated = rel.WithProperty("since", "2020");

        // Assert
        updated.Properties.Should().HaveCount(2);
        updated.Properties["role"].Should().Be("Engineer");
        updated.Properties["since"].Should().Be("2020");
    }

    [Fact]
    public void RecordEquality_WithSameValues_AreEqual()
    {
        // Arrange
        var props = new Dictionary<string, object>();
        var rel1 = new Relationship("r1", "WorksFor", "e1", "e2", props);
        var rel2 = new Relationship("r1", "WorksFor", "e1", "e2", props);

        // Act & Assert
        rel1.Should().Be(rel2);
    }
}
