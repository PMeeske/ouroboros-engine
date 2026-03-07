namespace Ouroboros.Tests.Pipeline.Verification;

using Ouroboros.Pipeline.Verification;

[Trait("Category", "Unit")]
public class FileSystemActionTests
{
    [Fact]
    public void Constructor_SetsOperationAndPath()
    {
        var action = new FileSystemAction("write", "/tmp/file.txt");

        action.Operation.Should().Be("write");
        action.Path.Should().Be("/tmp/file.txt");
    }

    [Fact]
    public void Path_DefaultsToNull()
    {
        var action = new FileSystemAction("read");
        action.Path.Should().BeNull();
    }

    [Fact]
    public void ToMeTTaAtom_ContainsOperation()
    {
        var action = new FileSystemAction("delete");
        action.ToMeTTaAtom().Should().Be("(FileSystemAction \"delete\")");
    }

    [Fact]
    public void InheritsFromPlanAction()
    {
        var action = new FileSystemAction("read");
        action.Should().BeAssignableTo<PlanAction>();
    }
}
