using FluentAssertions;
using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class ConcretePlanTests
{
    [Fact]
    public void Constructor_ShouldSetAllProperties()
    {
        // Arrange
        var steps = new List<string> { "step-1", "step-2", "step-3" };

        // Act
        var sut = new ConcretePlan("BuildWidget", steps);

        // Assert
        sut.AbstractTaskName.Should().Be("BuildWidget");
        sut.ConcreteSteps.Should().HaveCount(3);
        sut.ConcreteSteps.Should().ContainInOrder("step-1", "step-2", "step-3");
    }

    [Fact]
    public void Constructor_WithEmptySteps_ShouldWork()
    {
        // Arrange & Act
        var sut = new ConcretePlan("EmptyTask", new List<string>());

        // Assert
        sut.AbstractTaskName.Should().Be("EmptyTask");
        sut.ConcreteSteps.Should().BeEmpty();
    }

    [Fact]
    public void RecordEquality_SameValues_ShouldBeEqual()
    {
        // Arrange
        var steps = new List<string> { "a", "b" };
        var a = new ConcretePlan("Task", steps);
        var b = new ConcretePlan("Task", steps);

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void WithExpression_ShouldCreateModifiedCopy()
    {
        // Arrange
        var original = new ConcretePlan("OriginalTask", new List<string> { "s1" });

        // Act
        var modified = original with { AbstractTaskName = "ModifiedTask" };

        // Assert
        modified.AbstractTaskName.Should().Be("ModifiedTask");
        modified.ConcreteSteps.Should().BeSameAs(original.ConcreteSteps);
        original.AbstractTaskName.Should().Be("OriginalTask");
    }
}
