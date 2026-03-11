using FluentAssertions;
using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class HtnHierarchicalPlanTests
{
    [Fact]
    public void Constructor_ShouldSetAllProperties()
    {
        // Arrange
        var abstractTasks = new List<AbstractTask>
        {
            new("Task1", new List<string> { "pre1" }, new List<TaskDecomposition>())
        };
        var refinements = new List<ConcretePlan>
        {
            new("Task1", new List<string> { "step-a", "step-b" })
        };

        // Act
        var sut = new HtnHierarchicalPlan("BuildSystem", abstractTasks, refinements);

        // Assert
        sut.Goal.Should().Be("BuildSystem");
        sut.AbstractTasks.Should().HaveCount(1);
        sut.Refinements.Should().HaveCount(1);
    }

    [Fact]
    public void Constructor_WithEmptyCollections_ShouldWork()
    {
        // Arrange & Act
        var sut = new HtnHierarchicalPlan(
            "SimpleGoal",
            new List<AbstractTask>(),
            new List<ConcretePlan>());

        // Assert
        sut.Goal.Should().Be("SimpleGoal");
        sut.AbstractTasks.Should().BeEmpty();
        sut.Refinements.Should().BeEmpty();
    }

    [Fact]
    public void RecordEquality_SameReferences_ShouldBeEqual()
    {
        // Arrange
        var tasks = new List<AbstractTask>();
        var refinements = new List<ConcretePlan>();
        var a = new HtnHierarchicalPlan("G", tasks, refinements);
        var b = new HtnHierarchicalPlan("G", tasks, refinements);

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void WithExpression_ShouldCreateModifiedCopy()
    {
        // Arrange
        var original = new HtnHierarchicalPlan("Goal1", new List<AbstractTask>(), new List<ConcretePlan>());

        // Act
        var modified = original with { Goal = "Goal2" };

        // Assert
        modified.Goal.Should().Be("Goal2");
        modified.AbstractTasks.Should().BeSameAs(original.AbstractTasks);
    }
}
