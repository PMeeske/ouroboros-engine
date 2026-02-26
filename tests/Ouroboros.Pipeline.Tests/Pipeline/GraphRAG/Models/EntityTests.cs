namespace Ouroboros.Tests.Pipeline.GraphRAG.Models;

using Ouroboros.Pipeline.GraphRAG.Models;

[Trait("Category", "Unit")]
public class EntityTests
{
    [Fact]
    public void Create_ReturnsEntityWithEmptyProperties()
    {
        var entity = Entity.Create("e1", "Person", "John");

        entity.Id.Should().Be("e1");
        entity.Type.Should().Be("Person");
        entity.Name.Should().Be("John");
        entity.Properties.Should().BeEmpty();
    }

    [Fact]
    public void WithProperty_AddsPropertyAndReturnsNewEntity()
    {
        var entity = Entity.Create("e1", "Person", "John");
        var updated = entity.WithProperty("age", 30);

        updated.Properties.Should().ContainKey("age");
        entity.Properties.Should().BeEmpty(); // original unchanged
    }

    [Fact]
    public void VectorStoreId_DefaultsToNull()
    {
        var entity = Entity.Create("e1", "Person", "John");
        entity.VectorStoreId.Should().BeNull();
    }

    [Fact]
    public void VectorStoreId_CanBeSetViaInitializer()
    {
        var entity = Entity.Create("e1", "Person", "John") with { VectorStoreId = "vs-1" };
        entity.VectorStoreId.Should().Be("vs-1");
    }
}
