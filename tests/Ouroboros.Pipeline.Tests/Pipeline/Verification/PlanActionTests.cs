namespace Ouroboros.Tests.Pipeline.Verification;

using Ouroboros.Pipeline.Verification;

[Trait("Category", "Unit")]
public class PlanActionTests
{
    /// <summary>
    /// Concrete test subclass for the abstract PlanAction record.
    /// </summary>
    private sealed record TestPlanAction(string ActionName) : PlanAction
    {
        public override string ToMeTTaAtom() => $"(TestAction \"{ActionName}\")";
    }

    [Fact]
    public void ConcreteSubclass_CanBeCreated()
    {
        // Arrange & Act
        var action = new TestPlanAction("doSomething");

        // Assert
        action.Should().BeAssignableTo<PlanAction>();
        action.ActionName.Should().Be("doSomething");
    }

    [Fact]
    public void ToMeTTaAtom_ReturnsExpectedValue()
    {
        // Arrange
        var action = new TestPlanAction("analyze");

        // Act
        var atom = action.ToMeTTaAtom();

        // Assert
        atom.Should().Be("(TestAction \"analyze\")");
    }
}
