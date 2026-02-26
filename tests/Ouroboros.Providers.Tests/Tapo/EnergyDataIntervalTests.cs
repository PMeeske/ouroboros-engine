namespace Ouroboros.Tests.Tapo;

[Trait("Category", "Unit")]
public sealed class EnergyDataIntervalTests
{
    [Theory]
    [InlineData(EnergyDataInterval.Hourly, 0)]
    [InlineData(EnergyDataInterval.Daily, 1)]
    [InlineData(EnergyDataInterval.Monthly, 2)]
    public void Enum_HasExpectedValues(EnergyDataInterval interval, int expected)
    {
        ((int)interval).Should().Be(expected);
    }

    [Fact]
    public void SerializesToString()
    {
        var json = JsonSerializer.Serialize(EnergyDataInterval.Daily);
        json.Should().Contain("Daily");
    }
}
