using FluentAssertions;

namespace Ouroboros.Tests.Tapo;

[Trait("Category", "Unit")]
public class EnergyDataIntervalTests
{
    [Fact]
    public void EnergyDataInterval_ShouldHave_ThreeValues()
    {
        // Arrange & Act
        var values = Enum.GetValues<EnergyDataInterval>();

        // Assert
        values.Should().HaveCount(3);
    }

    [Theory]
    [InlineData(EnergyDataInterval.Hourly, 0)]
    [InlineData(EnergyDataInterval.Daily, 1)]
    [InlineData(EnergyDataInterval.Monthly, 2)]
    public void EnergyDataInterval_Value_ShouldHaveExpectedIntValue(EnergyDataInterval interval, int expected)
    {
        // Act & Assert
        ((int)interval).Should().Be(expected);
    }

    [Theory]
    [InlineData("Hourly", EnergyDataInterval.Hourly)]
    [InlineData("Daily", EnergyDataInterval.Daily)]
    [InlineData("Monthly", EnergyDataInterval.Monthly)]
    public void EnergyDataInterval_Parse_ShouldReturnExpectedValue(string name, EnergyDataInterval expected)
    {
        // Act
        var parsed = Enum.Parse<EnergyDataInterval>(name);

        // Assert
        parsed.Should().Be(expected);
    }

    [Fact]
    public void EnergyDataInterval_ShouldSerializeAsString()
    {
        // Arrange
        var interval = EnergyDataInterval.Daily;

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(interval);

        // Assert
        json.Should().Be("\"Daily\"");
    }

    [Fact]
    public void EnergyDataInterval_ShouldDeserializeFromString()
    {
        // Arrange
        var json = "\"Monthly\"";

        // Act
        var result = System.Text.Json.JsonSerializer.Deserialize<EnergyDataInterval>(json);

        // Assert
        result.Should().Be(EnergyDataInterval.Monthly);
    }
}
