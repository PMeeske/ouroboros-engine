namespace Ouroboros.Tests.Pipeline.Metacognition;

using Ouroboros.Pipeline.Metacognition;

[Trait("Category", "Unit")]
public class TrendTests
{
    [Fact]
    public void EnumHasExpectedCount()
    {
        Enum.GetValues<Trend>().Should().HaveCount(5);
    }

    [Theory]
    [InlineData(Trend.Improving)]
    [InlineData(Trend.Stable)]
    [InlineData(Trend.Declining)]
    [InlineData(Trend.Volatile)]
    [InlineData(Trend.Unknown)]
    public void AllValues_AreDefined(Trend trend)
    {
        Enum.IsDefined(trend).Should().BeTrue();
    }
}
