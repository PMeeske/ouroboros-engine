namespace Ouroboros.Tests.Pipeline.WorldModel;

using Ouroboros.Pipeline.WorldModel;

[Trait("Category", "Unit")]
public class ConstraintTests
{
    [Fact]
    public void Create_DefaultPriorityIsZero()
    {
        var constraint = Constraint.Create("safety", "No harmful actions");

        constraint.Name.Should().Be("safety");
        constraint.Rule.Should().Be("No harmful actions");
        constraint.Priority.Should().Be(0);
    }

    [Fact]
    public void Create_WithPriority_SetsPriority()
    {
        var constraint = Constraint.Create("safety", "rule", 50);
        constraint.Priority.Should().Be(50);
    }

    [Fact]
    public void Critical_HasPriority100()
    {
        var constraint = Constraint.Critical("safety", "Must not harm");
        constraint.Priority.Should().Be(100);
    }

    [Fact]
    public void Create_ThrowsOnNullName()
    {
        var act = () => Constraint.Create(null!, "rule");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_ThrowsOnNullRule()
    {
        var act = () => Constraint.Create("name", null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
