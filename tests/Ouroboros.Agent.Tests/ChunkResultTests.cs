namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class ChunkResultTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var result = new ChunkResult(
            ChunkIndex: 0,
            Input: "input",
            Output: "output",
            ExecutionTime: TimeSpan.FromSeconds(1),
            Success: true);

        result.ChunkIndex.Should().Be(0);
        result.Input.Should().Be("input");
        result.Output.Should().Be("output");
        result.ExecutionTime.Should().Be(TimeSpan.FromSeconds(1));
        result.Success.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithError_SetsError()
    {
        var result = new ChunkResult(
            ChunkIndex: 1,
            Input: "input",
            Output: "",
            ExecutionTime: TimeSpan.FromMilliseconds(50),
            Success: false,
            Error: "something went wrong");

        result.Success.Should().BeFalse();
        result.Error.Should().Be("something went wrong");
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var t = TimeSpan.FromSeconds(1);
        var a = new ChunkResult(0, "in", "out", t, true);
        var b = new ChunkResult(0, "in", "out", t, true);
        a.Should().Be(b);
    }

    [Fact]
    public void RecordWith_CreatesModifiedCopy()
    {
        var original = new ChunkResult(0, "in", "out", TimeSpan.Zero, true);
        var modified = original with { Success = false, Error = "fail" };

        modified.Success.Should().BeFalse();
        modified.Error.Should().Be("fail");
        original.Success.Should().BeTrue();
    }
}
