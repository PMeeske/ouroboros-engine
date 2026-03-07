namespace Ouroboros.Tests.Pipeline.Metacognition;

using Ouroboros.Pipeline.Metacognition;

[Trait("Category", "Unit")]
public class PerformanceDimensionTests
{
    [Fact]
    public void EnumHasExpectedCount()
    {
        Enum.GetValues<PerformanceDimension>().Should().HaveCount(6);
    }

    [Theory]
    [InlineData(PerformanceDimension.Accuracy)]
    [InlineData(PerformanceDimension.Speed)]
    [InlineData(PerformanceDimension.Creativity)]
    [InlineData(PerformanceDimension.Consistency)]
    [InlineData(PerformanceDimension.Adaptability)]
    [InlineData(PerformanceDimension.Communication)]
    public void AllValues_AreDefined(PerformanceDimension dim)
    {
        Enum.IsDefined(dim).Should().BeTrue();
    }
}
