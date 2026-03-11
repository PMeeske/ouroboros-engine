using FluentAssertions;
using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class AbstractTaskTests
{
    [Fact]
    public void Constructor_ShouldSetAllProperties()
    {
        // Arrange
        var preconditions = new List<string> { "has-tools", "is-authorized" };
        var decomposition = new TaskDecomposition("BuildApp", new List<string> { "code", "test" }, new List<string>());
        var decompositions = new List<TaskDecomposition> { decomposition };

        // Act
        var sut = new AbstractTask("BuildApp", preconditions, decompositions);

        // Assert
        sut.Name.Should().Be("BuildApp");
        sut.Preconditions.Should().HaveCount(2);
        sut.PossibleDecompositions.Should().HaveCount(1);
    }

    [Fact]
    public void Constructor_WithEmptyCollections_ShouldWork()
    {
        // Arrange & Act
        var sut = new AbstractTask("SimpleTask", new List<string>(), new List<TaskDecomposition>());

        // Assert
        sut.Name.Should().Be("SimpleTask");
        sut.Preconditions.Should().BeEmpty();
        sut.PossibleDecompositions.Should().BeEmpty();
    }

    [Fact]
    public void RecordEquality_SameReferences_ShouldBeEqual()
    {
        // Arrange
        var preconditions = new List<string> { "ready" };
        var decompositions = new List<TaskDecomposition>();
        var a = new AbstractTask("Task1", preconditions, decompositions);
        var b = new AbstractTask("Task1", preconditions, decompositions);

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void WithExpression_ShouldCreateModifiedCopy()
    {
        // Arrange
        var original = new AbstractTask("Original", new List<string>(), new List<TaskDecomposition>());

        // Act
        var modified = original with { Name = "Modified" };

        // Assert
        modified.Name.Should().Be("Modified");
        modified.Preconditions.Should().BeSameAs(original.Preconditions);
    }
}
