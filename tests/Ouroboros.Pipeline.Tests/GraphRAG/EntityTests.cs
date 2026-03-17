using Ouroboros.Pipeline.GraphRAG.Models;

namespace Ouroboros.Tests.GraphRAG;

[Trait("Category", "Unit")]
public sealed class EntityTests
{
    [Fact]
    public void Constructor_WithAllParameters_SetsProperties()
    {
        // Arrange
        var properties = new Dictionary<string, object> { ["key"] = "value" };

        // Act
        var entity = new Entity("e1", "Person", "Alice", properties);

        // Assert
        entity.Id.Should().Be("e1");
        entity.Type.Should().Be("Person");
        entity.Name.Should().Be("Alice");
        entity.Properties.Should().ContainKey("key");
        entity.Properties["key"].Should().Be("value");
    }

    [Fact]
    public void VectorStoreId_DefaultsToNull()
    {
        // Arrange & Act
        var entity = Entity.Create("e1", "Person", "Alice");

        // Assert
        entity.VectorStoreId.Should().BeNull();
    }

    [Fact]
    public void VectorStoreId_CanBeSetViaInit()
    {
        // Arrange & Act
        var entity = Entity.Create("e1", "Person", "Alice") with { VectorStoreId = "vs-123" };

        // Assert
        entity.VectorStoreId.Should().Be("vs-123");
    }

    [Fact]
    public void Create_WithMinimalProperties_ReturnsEntityWithEmptyProperties()
    {
        // Arrange & Act
        var entity = Entity.Create("e1", "Concept", "AI");

        // Assert
        entity.Id.Should().Be("e1");
        entity.Type.Should().Be("Concept");
        entity.Name.Should().Be("AI");
        entity.Properties.Should().BeEmpty();
    }

    [Fact]
    public void WithProperty_AddsPropertyToNewInstance()
    {
        // Arrange
        var entity = Entity.Create("e1", "Person", "Alice");

        // Act
        var updated = entity.WithProperty("age", 30);

        // Assert
        updated.Properties.Should().ContainKey("age");
        updated.Properties["age"].Should().Be(30);
        entity.Properties.Should().BeEmpty("original should be unchanged");
    }

    [Fact]
    public void WithProperty_OverwritesExistingProperty()
    {
        // Arrange
        var entity = Entity.Create("e1", "Person", "Alice").WithProperty("age", 30);

        // Act
        var updated = entity.WithProperty("age", 31);

        // Assert
        updated.Properties["age"].Should().Be(31);
    }

    [Fact]
    public void WithProperty_PreservesExistingProperties()
    {
        // Arrange
        var entity = Entity.Create("e1", "Person", "Alice")
            .WithProperty("age", 30);

        // Act
        var updated = entity.WithProperty("city", "Berlin");

        // Assert
        updated.Properties.Should().HaveCount(2);
        updated.Properties["age"].Should().Be(30);
        updated.Properties["city"].Should().Be("Berlin");
    }

    [Fact]
    public void RecordEquality_WithSameValues_AreEqual()
    {
        // Arrange
        var props = new Dictionary<string, object>();
        var entity1 = new Entity("e1", "Person", "Alice", props);
        var entity2 = new Entity("e1", "Person", "Alice", props);

        // Act & Assert
        entity1.Should().Be(entity2);
    }

    [Fact]
    public void RecordEquality_WithDifferentIds_AreNotEqual()
    {
        // Arrange
        var props = new Dictionary<string, object>();
        var entity1 = new Entity("e1", "Person", "Alice", props);
        var entity2 = new Entity("e2", "Person", "Alice", props);

        // Act & Assert
        entity1.Should().NotBe(entity2);
    }
}
