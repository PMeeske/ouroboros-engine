namespace Ouroboros.Tests.Pipeline.Verification;

using Ouroboros.Pipeline.Verification;

[Trait("Category", "Unit")]
public class PlanTests
{
    [Fact]
    public void Constructor_SetsDescription()
    {
        var plan = new Plan("Test plan");

        plan.Description.Should().Be("Test plan");
        plan.Actions.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_ThrowsOnNullDescription()
    {
        var act = () => new Plan(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_AcceptsActions()
    {
        var actions = new List<PlanAction> { new FileSystemAction("read") };
        var plan = new Plan("Plan", actions);

        plan.Actions.Should().HaveCount(1);
    }

    [Fact]
    public void WithAction_ReturnsNewPlanWithActionAdded()
    {
        var plan = new Plan("Plan");
        var updated = plan.WithAction(new FileSystemAction("write"));

        updated.Actions.Should().HaveCount(1);
        plan.Actions.Should().BeEmpty();
    }

    [Fact]
    public void WithAction_ThrowsOnNull()
    {
        var plan = new Plan("Plan");
        var act = () => plan.WithAction(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ToMeTTaAtoms_ReturnsAtomPerAction()
    {
        var plan = new Plan("Plan", new PlanAction[]
        {
            new FileSystemAction("read"),
            new NetworkAction("get"),
        });

        var atoms = plan.ToMeTTaAtoms().ToList();

        atoms.Should().HaveCount(2);
        atoms[0].Should().Contain("FileSystemAction");
        atoms[1].Should().Contain("NetworkAction");
    }

    [Fact]
    public void ToMeTTaAtom_ReturnsFirstActionAtom()
    {
        var plan = new Plan("Plan", new PlanAction[]
        {
            new ToolAction("search"),
            new FileSystemAction("read"),
        });

        plan.ToMeTTaAtom().Should().Contain("ToolAction");
    }

    [Fact]
    public void ToMeTTaAtom_ReturnsEmptyStringWhenNoActions()
    {
        var plan = new Plan("Plan");
        plan.ToMeTTaAtom().Should().BeEmpty();
    }
}
