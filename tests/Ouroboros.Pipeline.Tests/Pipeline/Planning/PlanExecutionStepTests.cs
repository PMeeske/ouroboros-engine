namespace Ouroboros.Tests.Pipeline.Planning;

using Ouroboros.Pipeline.Planning;

[Trait("Category", "Unit")]
public class PlanExecutionStepTests
{
    [Fact]
    public void Properties_CanBeSetAndRetrieved()
    {
        var start = DateTime.UtcNow;
        var end = start.AddSeconds(5);

        var step = new PlanExecutionStep
        {
            ToolName = "search",
            Input = "query",
            Output = "result",
            Success = true,
            Error = null,
            StartTime = start,
            EndTime = end,
        };

        step.ToolName.Should().Be("search");
        step.Input.Should().Be("query");
        step.Output.Should().Be("result");
        step.Success.Should().BeTrue();
        step.Error.Should().BeNull();
    }

    [Fact]
    public void Duration_ComputesTimeDifference()
    {
        var start = DateTime.UtcNow;
        var end = start.AddSeconds(3);

        var step = new PlanExecutionStep
        {
            ToolName = "test",
            StartTime = start,
            EndTime = end,
        };

        step.Duration.Should().Be(TimeSpan.FromSeconds(3));
    }
}
