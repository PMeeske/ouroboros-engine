namespace Ouroboros.Tests.Pipeline.Learning;

using Ouroboros.Pipeline.Learning;

[Trait("Category", "Unit")]
public class AdaptationEventTypeTests
{
    [Fact]
    public void EnumHasExpectedValues()
    {
        Enum.GetValues<AdaptationEventType>().Should().HaveCountGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void StrategyChange_IsDefined()
    {
        Enum.IsDefined(AdaptationEventType.StrategyChange).Should().BeTrue();
    }
}
