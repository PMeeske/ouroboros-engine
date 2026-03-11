using FluentAssertions;
using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class TaskDecompositionTests
{
    [Fact]
    public void Constructor_ShouldSetAllProperties()
    {
        // Arrange
        var subTasks = new List<string> { "parse", "validate", "transform" };
        var constraints = new List<string> { "parse < validate", "validate < transform" };

        // Act
        var sut = new TaskDecomposition("ProcessData", subTasks, constraints);

        // Assert
        sut.AbstractTask.Should().Be("ProcessData");
        sut.SubTasks.Should().HaveCount(3);
        sut.OrderingConstraints.Should().HaveCount(2);
    }

    [Fact]
    public void Constructor_WithEmptyCollections_ShouldWork()
    {
        // Arrange & Act
        var sut = new TaskDecomposition("Atomic", new List<string>(), new List<string>());

        // Assert
        sut.AbstractTask.Should().Be("Atomic");
        sut.SubTasks.Should().BeEmpty();
        sut.OrderingConstraints.Should().BeEmpty();
    }

    [Fact]
    public void RecordEquality_SameReferences_ShouldBeEqual()
    {
        // Arrange
        var subTasks = new List<string> { "a" };
        var constraints = new List<string>();
        var a = new TaskDecomposition("T", subTasks, constraints);
        var b = new TaskDecomposition("T", subTasks, constraints);

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void WithExpression_ShouldCreateModifiedCopy()
    {
        // Arrange
        var original = new TaskDecomposition("Original", new List<string> { "s1" }, new List<string>());

        // Act
        var modified = original with { AbstractTask = "Modified" };

        // Assert
        modified.AbstractTask.Should().Be("Modified");
        modified.SubTasks.Should().BeSameAs(original.SubTasks);
    }
}
