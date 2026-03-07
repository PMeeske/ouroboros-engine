namespace Ouroboros.Tests.Pipeline.Middleware;

using Ouroboros.Pipeline.Middleware;

[Trait("Category", "Unit")]
public class PipelineResultTests
{
    [Fact]
    public void Successful_HasSuccessTrueAndOutput()
    {
        var result = PipelineResult.Successful("output text");

        result.Success.Should().BeTrue();
        result.Output.Should().Be("output text");
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Failed_HasSuccessFalseAndError()
    {
        var ex = new InvalidOperationException("something broke");
        var result = PipelineResult.Failed(ex);

        result.Success.Should().BeFalse();
        result.Output.Should().BeNull();
        result.Error.Should().Be(ex);
    }

    [Fact]
    public void Error_DefaultsToNull()
    {
        var result = new PipelineResult(true, "out");
        result.Error.Should().BeNull();
    }
}
