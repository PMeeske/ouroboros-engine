namespace Ouroboros.Tests.Pipeline.Middleware;

using Ouroboros.Pipeline.Middleware;

[Trait("Category", "Unit")]
public class PipelineContextTests
{
    [Fact]
    public void FromInput_CreatesContextWithEmptyMetadata()
    {
        var context = PipelineContext.FromInput("test input");

        context.Input.Should().Be("test input");
        context.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_SetsInputAndMetadata()
    {
        var metadata = new Dictionary<string, object> { { "key", "value" } };
        var context = new PipelineContext("input", metadata);

        context.Input.Should().Be("input");
        context.Metadata.Should().ContainKey("key");
    }

    [Fact]
    public void Record_SupportsEquality()
    {
        var meta = new Dictionary<string, object>();
        var c1 = new PipelineContext("input", meta);
        var c2 = new PipelineContext("input", meta);

        c1.Should().Be(c2);
    }
}
