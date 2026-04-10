namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class MindOperationTests
{
    [Fact]
    public void Return_CreatesOperationWithValue()
    {
        var op = MindOperation<string>.Return("hello");

        op.SupportsStreaming.Should().BeFalse();
    }

    [Fact]
    public void FromAsync_CreatesNonStreamingOperation()
    {
        var op = MindOperation<int>.FromAsync((_, _) => Task.FromResult(42));

        op.SupportsStreaming.Should().BeFalse();
    }

    [Fact]
    public void FromStream_CreatesStreamingOperation()
    {
        var op = MindOperation<string>.FromStream(
            (_, _) => R3.Observable.Empty<(bool, string)>(),
            (_, _) => Task.FromResult("done"));

        op.SupportsStreaming.Should().BeTrue();
    }

    [Fact]
    public void GetStream_WhenNoStream_ReturnsNull()
    {
        var op = MindOperation<string>.Return("test");

        op.GetStream(null!).Should().BeNull();
    }

    [Fact]
    public void Select_TransformsResult()
    {
        var op = MindOperation<int>.Return(0);
        var mapped = op.Select(x => x.ToString());

        mapped.SupportsStreaming.Should().BeFalse();
    }
}
