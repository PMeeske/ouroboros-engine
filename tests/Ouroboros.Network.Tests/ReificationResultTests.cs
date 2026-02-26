namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class ReificationResultTests
{
    [Fact]
    public void Ctor_SetsAllProperties()
    {
        var result = new ReificationResult("branch-1", 5, 4, 20, 15);

        result.BranchName.Should().Be("branch-1");
        result.NodesCreated.Should().Be(5);
        result.TransitionsCreated.Should().Be(4);
        result.TotalNodes.Should().Be(20);
        result.TotalTransitions.Should().Be(15);
    }
}
