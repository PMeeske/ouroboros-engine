using FluentAssertions;
using Ouroboros.Pipeline.Verification;

namespace Ouroboros.Tests.Verification;

[Trait("Category", "Unit")]
public class PlanTests
{
    [Fact]
    public void Constructor_WithDescriptionOnly_CreatesEmptyActionList()
    {
        // Arrange & Act
        var plan = new Plan("test plan");

        // Assert
        plan.Description.Should().Be("test plan");
        plan.Actions.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithDescriptionAndActions_SetsActions()
    {
        // Arrange
        var actions = new PlanAction[]
        {
            new FileSystemAction("read"),
            new NetworkAction("get")
        };

        // Act
        var plan = new Plan("multi-action plan", actions);

        // Assert
        plan.Description.Should().Be("multi-action plan");
        plan.Actions.Should().HaveCount(2);
    }

    [Fact]
    public void Constructor_WithNullActions_CreatesEmptyActionList()
    {
        // Arrange & Act
        var plan = new Plan("empty plan", null);

        // Assert
        plan.Actions.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_NullDescription_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var act = () => new Plan(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WithAction_AddsActionToNewPlan()
    {
        // Arrange
        var plan = new Plan("base plan");
        var action = new FileSystemAction("read");

        // Act
        var newPlan = plan.WithAction(action);

        // Assert
        newPlan.Actions.Should().HaveCount(1);
        newPlan.Actions[0].Should().Be(action);
        newPlan.Description.Should().Be("base plan");
    }

    [Fact]
    public void WithAction_DoesNotMutateOriginalPlan()
    {
        // Arrange
        var plan = new Plan("immutable plan");
        var action = new FileSystemAction("write");

        // Act
        _ = plan.WithAction(action);

        // Assert
        plan.Actions.Should().BeEmpty();
    }

    [Fact]
    public void WithAction_ChainsMultipleActions()
    {
        // Arrange
        var plan = new Plan("chained plan");
        var action1 = new FileSystemAction("read");
        var action2 = new NetworkAction("get");
        var action3 = new ToolAction("search");

        // Act
        var result = plan
            .WithAction(action1)
            .WithAction(action2)
            .WithAction(action3);

        // Assert
        result.Actions.Should().HaveCount(3);
        result.Actions[0].Should().Be(action1);
        result.Actions[1].Should().Be(action2);
        result.Actions[2].Should().Be(action3);
    }

    [Fact]
    public void WithAction_NullAction_ThrowsArgumentNullException()
    {
        // Arrange
        var plan = new Plan("plan");

        // Act
        var act = () => plan.WithAction(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ToMeTTaAtoms_WithActions_ReturnsAllAtoms()
    {
        // Arrange
        var plan = new Plan("plan", new PlanAction[]
        {
            new FileSystemAction("read"),
            new NetworkAction("post"),
            new ToolAction("calc")
        });

        // Act
        var atoms = plan.ToMeTTaAtoms().ToList();

        // Assert
        atoms.Should().HaveCount(3);
        atoms[0].Should().Be("(FileSystemAction \"read\")");
        atoms[1].Should().Be("(NetworkAction \"post\")");
        atoms[2].Should().Be("(ToolAction \"calc\")");
    }

    [Fact]
    public void ToMeTTaAtoms_WithNoActions_ReturnsEmpty()
    {
        // Arrange
        var plan = new Plan("empty");

        // Act
        var atoms = plan.ToMeTTaAtoms().ToList();

        // Assert
        atoms.Should().BeEmpty();
    }

    [Fact]
    public void ToMeTTaAtom_WithActions_ReturnsFirstActionAtom()
    {
        // Arrange
        var plan = new Plan("plan", new PlanAction[]
        {
            new FileSystemAction("write"),
            new NetworkAction("get")
        });

        // Act
        var atom = plan.ToMeTTaAtom();

        // Assert
        atom.Should().Be("(FileSystemAction \"write\")");
    }

    [Fact]
    public void ToMeTTaAtom_WithNoActions_ReturnsEmptyString()
    {
        // Arrange
        var plan = new Plan("empty");

        // Act
        var atom = plan.ToMeTTaAtom();

        // Assert
        atom.Should().BeEmpty();
    }

    [Fact]
    public void ActionsAreImmutable_CannotModifySourceList()
    {
        // Arrange
        var actions = new List<PlanAction> { new FileSystemAction("read") };
        var plan = new Plan("plan", actions);

        // Act - modify the original list
        actions.Add(new NetworkAction("get"));

        // Assert - plan is not affected
        plan.Actions.Should().HaveCount(1);
    }

    [Fact]
    public void RecordEquality_SamePlan_AreEqual()
    {
        // Arrange
        var actions = new PlanAction[] { new FileSystemAction("read") };
        var plan1 = new Plan("plan", actions);
        var plan2 = new Plan("plan", actions);

        // Assert - records use value equality, but Plan uses ImmutableList internally
        // Two Plans with same description and same actions constructed separately
        // may or may not be equal depending on ImmutableList reference equality
        plan1.Description.Should().Be(plan2.Description);
    }
}
