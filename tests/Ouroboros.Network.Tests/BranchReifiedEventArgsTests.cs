namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class BranchReifiedEventArgsTests
{
    [Fact]
    public void Ctor_SetsProperties()
    {
        var before = DateTime.UtcNow;
        var args = new BranchReifiedEventArgs("test-branch", 42);
        var after = DateTime.UtcNow;

        args.BranchName.Should().Be("test-branch");
        args.NodesCreated.Should().Be(42);
        args.Timestamp.Should().BeOnOrAfter(before);
        args.Timestamp.Should().BeOnOrBefore(after);
    }
}
