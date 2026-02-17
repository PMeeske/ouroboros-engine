using ExecutionContext = Ouroboros.Pipeline.Memory.ExecutionContext;

namespace Ouroboros.Tests.Pipeline.Memory;

/// <summary>
/// Unit tests for ExecutionContext record type.
/// </summary>
[Trait("Category", "Unit")]
public class ExecutionContextTests
{
    [Fact]
    public void ExecutionContext_WithGoal_ShouldCreateContext()
    {
        // Act
        var context = ExecutionContext.WithGoal("test goal");

        // Assert
        context.Goal.Should().Be("test goal");
        context.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void ExecutionContext_WithMetadata_ShouldAddMetadata()
    {
        // Arrange
        var context = ExecutionContext.WithGoal("test goal");

        // Act
        var updated = context.WithMetadata("key1", "value1");

        // Assert
        context.Metadata.Should().BeEmpty(); // Original unchanged
        updated.Metadata.Should().ContainKey("key1");
        updated.Metadata["key1"].Should().Be("value1");
    }

    [Fact]
    public void ExecutionContext_WithMultipleMetadata_ShouldAccumulate()
    {
        // Arrange
        var context = ExecutionContext.WithGoal("test goal");

        // Act
        var updated = context
            .WithMetadata("key1", "value1")
            .WithMetadata("key2", 42)
            .WithMetadata("key3", true);

        // Assert
        updated.Metadata.Should().HaveCount(3);
        updated.Metadata["key1"].Should().Be("value1");
        updated.Metadata["key2"].Should().Be(42);
        updated.Metadata["key3"].Should().Be(true);
    }
}