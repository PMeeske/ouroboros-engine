using FluentAssertions;
using Ouroboros.Agent;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public class DivideAndConquerConfigTests
{
    [Fact]
    public void DefaultConstructor_ShouldSetDefaults()
    {
        // Arrange & Act
        var sut = new DivideAndConquerConfig();

        // Assert
        sut.MaxParallelism.Should().Be(4);
        sut.ChunkSize.Should().Be(500);
        sut.MergeResults.Should().BeTrue();
        sut.MergeSeparator.Should().Be("\n\n---\n\n");
    }

    [Fact]
    public void Constructor_WithCustomValues_ShouldSetAllProperties()
    {
        // Arrange & Act
        var sut = new DivideAndConquerConfig(
            MaxParallelism: 8,
            ChunkSize: 1000,
            MergeResults: false,
            MergeSeparator: " | ");

        // Assert
        sut.MaxParallelism.Should().Be(8);
        sut.ChunkSize.Should().Be(1000);
        sut.MergeResults.Should().BeFalse();
        sut.MergeSeparator.Should().Be(" | ");
    }

    [Fact]
    public void RecordEquality_SameDefaults_ShouldBeEqual()
    {
        // Arrange
        var a = new DivideAndConquerConfig();
        var b = new DivideAndConquerConfig();

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void WithExpression_ShouldCreateModifiedCopy()
    {
        // Arrange
        var original = new DivideAndConquerConfig();

        // Act
        var modified = original with { ChunkSize = 250 };

        // Assert
        modified.ChunkSize.Should().Be(250);
        modified.MaxParallelism.Should().Be(4);
    }
}
