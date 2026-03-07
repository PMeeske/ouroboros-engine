namespace Ouroboros.Tests.Pipeline.Metacognition;

using Ouroboros.Pipeline.Metacognition;

[Trait("Category", "Unit")]
public class HealthStatusTests
{
    [Theory]
    [InlineData(HealthStatus.Healthy, 0)]
    [InlineData(HealthStatus.Degraded, 1)]
    [InlineData(HealthStatus.Impaired, 2)]
    [InlineData(HealthStatus.Critical, 3)]
    public void EnumValues_AreDefinedCorrectly(HealthStatus value, int expectedInt)
    {
        ((int)value).Should().Be(expectedInt);
    }
}
