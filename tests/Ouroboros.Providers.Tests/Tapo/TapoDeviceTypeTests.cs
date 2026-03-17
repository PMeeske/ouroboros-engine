using FluentAssertions;

namespace Ouroboros.Tests.Tapo;

[Trait("Category", "Unit")]
public class TapoDeviceTypeTests
{
    [Fact]
    public void TapoDeviceType_ShouldHave_ExpectedNumberOfValues()
    {
        // Arrange & Act
        var values = Enum.GetValues<TapoDeviceType>();

        // Assert
        // 18 light/plug + 9 camera = 27
        values.Should().HaveCount(27);
    }

    [Theory]
    [InlineData(TapoDeviceType.L510)]
    [InlineData(TapoDeviceType.L520)]
    [InlineData(TapoDeviceType.L610)]
    public void TapoDeviceType_NonColorLightBulbs_ShouldBeDefined(TapoDeviceType deviceType)
    {
        // Act & Assert
        Enum.IsDefined(deviceType).Should().BeTrue();
    }

    [Theory]
    [InlineData(TapoDeviceType.L530)]
    [InlineData(TapoDeviceType.L535)]
    [InlineData(TapoDeviceType.L630)]
    public void TapoDeviceType_ColorLightBulbs_ShouldBeDefined(TapoDeviceType deviceType)
    {
        // Act & Assert
        Enum.IsDefined(deviceType).Should().BeTrue();
    }

    [Theory]
    [InlineData(TapoDeviceType.L900)]
    [InlineData(TapoDeviceType.L920)]
    [InlineData(TapoDeviceType.L930)]
    public void TapoDeviceType_LightStrips_ShouldBeDefined(TapoDeviceType deviceType)
    {
        // Act & Assert
        Enum.IsDefined(deviceType).Should().BeTrue();
    }

    [Theory]
    [InlineData(TapoDeviceType.P100)]
    [InlineData(TapoDeviceType.P105)]
    public void TapoDeviceType_SmartPlugs_ShouldBeDefined(TapoDeviceType deviceType)
    {
        // Act & Assert
        Enum.IsDefined(deviceType).Should().BeTrue();
    }

    [Theory]
    [InlineData(TapoDeviceType.P110)]
    [InlineData(TapoDeviceType.P110M)]
    [InlineData(TapoDeviceType.P115)]
    public void TapoDeviceType_EnergyMonitoringPlugs_ShouldBeDefined(TapoDeviceType deviceType)
    {
        // Act & Assert
        Enum.IsDefined(deviceType).Should().BeTrue();
    }

    [Theory]
    [InlineData(TapoDeviceType.P300)]
    [InlineData(TapoDeviceType.P304)]
    [InlineData(TapoDeviceType.P304M)]
    [InlineData(TapoDeviceType.P316)]
    public void TapoDeviceType_PowerStrips_ShouldBeDefined(TapoDeviceType deviceType)
    {
        // Act & Assert
        Enum.IsDefined(deviceType).Should().BeTrue();
    }

    [Theory]
    [InlineData(TapoDeviceType.C100)]
    [InlineData(TapoDeviceType.C200)]
    [InlineData(TapoDeviceType.C210)]
    [InlineData(TapoDeviceType.C220)]
    [InlineData(TapoDeviceType.C310)]
    [InlineData(TapoDeviceType.C320)]
    [InlineData(TapoDeviceType.C420)]
    [InlineData(TapoDeviceType.C500)]
    [InlineData(TapoDeviceType.C520)]
    public void TapoDeviceType_Cameras_ShouldBeDefined(TapoDeviceType deviceType)
    {
        // Act & Assert
        Enum.IsDefined(deviceType).Should().BeTrue();
    }

    [Theory]
    [InlineData("L530", TapoDeviceType.L530)]
    [InlineData("P110", TapoDeviceType.P110)]
    [InlineData("C200", TapoDeviceType.C200)]
    public void TapoDeviceType_Parse_ShouldReturnExpectedValue(string name, TapoDeviceType expected)
    {
        // Act
        var parsed = Enum.Parse<TapoDeviceType>(name);

        // Assert
        parsed.Should().Be(expected);
    }

    [Fact]
    public void TapoDeviceType_ShouldSerializeAsString()
    {
        // Arrange
        var deviceType = TapoDeviceType.L530;

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(deviceType);

        // Assert
        json.Should().Be("\"L530\"");
    }

    [Fact]
    public void TapoDeviceType_ShouldDeserializeFromString()
    {
        // Arrange
        var json = "\"C200\"";

        // Act
        var result = System.Text.Json.JsonSerializer.Deserialize<TapoDeviceType>(json);

        // Assert
        result.Should().Be(TapoDeviceType.C200);
    }
}
