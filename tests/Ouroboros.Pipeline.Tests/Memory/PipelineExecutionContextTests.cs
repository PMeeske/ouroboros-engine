using FluentAssertions;

namespace Ouroboros.Tests.Memory;

[Trait("Category", "Unit")]
public class PipelineExecutionContextTests
{
    [Fact]
    public void Constructor_WithGoalAndMetadata_SetsPropertiesCorrectly()
    {
        // Arrange
        var metadata = ImmutableDictionary<string, object>.Empty.Add("key", "value");

        // Act
        var context = new PipelineExecutionContext("test goal", metadata);

        // Assert
        context.Goal.Should().Be("test goal");
        context.Metadata.Should().HaveCount(1);
        context.Metadata["key"].Should().Be("value");
    }

    [Fact]
    public void WithGoal_CreatesContextWithEmptyMetadata()
    {
        // Act
        var context = PipelineExecutionContext.WithGoal("my goal");

        // Assert
        context.Goal.Should().Be("my goal");
        context.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void WithGoal_EmptyString_CreatesValidContext()
    {
        // Act
        var context = PipelineExecutionContext.WithGoal(string.Empty);

        // Assert
        context.Goal.Should().BeEmpty();
        context.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void WithMetadata_AddsMetadataEntry()
    {
        // Arrange
        var context = PipelineExecutionContext.WithGoal("goal");

        // Act
        var updated = context.WithMetadata("environment", "production");

        // Assert
        updated.Goal.Should().Be("goal");
        updated.Metadata.Should().HaveCount(1);
        updated.Metadata["environment"].Should().Be("production");
    }

    [Fact]
    public void WithMetadata_MultipleCalls_AccumulatesMetadata()
    {
        // Arrange
        var context = PipelineExecutionContext.WithGoal("goal");

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

    [Fact]
    public void WithMetadata_DoesNotMutateOriginal()
    {
        // Arrange
        var original = PipelineExecutionContext.WithGoal("goal");

        // Act
        var modified = original.WithMetadata("key", "value");

        // Assert
        original.Metadata.Should().BeEmpty();
        modified.Metadata.Should().HaveCount(1);
    }

    [Fact]
    public void RecordEquality_WithSameValues_AreEqual()
    {
        // Arrange
        var metadata = ImmutableDictionary<string, object>.Empty;
        var context1 = new PipelineExecutionContext("goal", metadata);
        var context2 = new PipelineExecutionContext("goal", metadata);

        // Assert
        context1.Should().Be(context2);
    }

    [Fact]
    public void RecordEquality_WithDifferentGoals_AreNotEqual()
    {
        // Arrange
        var metadata = ImmutableDictionary<string, object>.Empty;
        var context1 = new PipelineExecutionContext("goal1", metadata);
        var context2 = new PipelineExecutionContext("goal2", metadata);

        // Assert
        context1.Should().NotBe(context2);
    }

    [Fact]
    public void With_Expression_CreatesModifiedCopy()
    {
        // Arrange
        var original = PipelineExecutionContext.WithGoal("original");

        // Act
        var modified = original with { Goal = "modified" };

        // Assert
        modified.Goal.Should().Be("modified");
        original.Goal.Should().Be("original");
    }

    [Fact]
    public void Metadata_IsImmutableDictionary()
    {
        // Arrange
        var context = PipelineExecutionContext.WithGoal("goal");

        // Assert
        context.Metadata.Should().BeAssignableTo<ImmutableDictionary<string, object>>();
    }
}
