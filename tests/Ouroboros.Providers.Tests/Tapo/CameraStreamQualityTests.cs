using FluentAssertions;

namespace Ouroboros.Tests.Tapo;

[Trait("Category", "Unit")]
public class CameraStreamQualityTests
{
    [Fact]
    public void CameraStreamQuality_ShouldHave_FiveValues()
    {
        // Arrange & Act
        var values = Enum.GetValues<CameraStreamQuality>();

        // Assert
        values.Should().HaveCount(5);
    }

    [Theory]
    [InlineData(CameraStreamQuality.Low, 0)]
    [InlineData(CameraStreamQuality.Standard, 1)]
    [InlineData(CameraStreamQuality.HD, 2)]
    [InlineData(CameraStreamQuality.FullHD, 3)]
    [InlineData(CameraStreamQuality.QHD, 4)]
    public void CameraStreamQuality_Value_ShouldHaveExpectedIntValue(CameraStreamQuality quality, int expected)
    {
        // Act & Assert
        ((int)quality).Should().Be(expected);
    }

    [Theory]
    [InlineData("Low", CameraStreamQuality.Low)]
    [InlineData("Standard", CameraStreamQuality.Standard)]
    [InlineData("HD", CameraStreamQuality.HD)]
    [InlineData("FullHD", CameraStreamQuality.FullHD)]
    [InlineData("QHD", CameraStreamQuality.QHD)]
    public void CameraStreamQuality_Parse_ShouldReturnExpectedValue(string name, CameraStreamQuality expected)
    {
        // Act
        var parsed = Enum.Parse<CameraStreamQuality>(name);

        // Assert
        parsed.Should().Be(expected);
    }

    [Fact]
    public void CameraStreamQuality_IsDefined_ShouldReturnTrueForValidValues()
    {
        // Act & Assert
        foreach (var value in Enum.GetValues<CameraStreamQuality>())
        {
            Enum.IsDefined(value).Should().BeTrue();
        }
    }

    [Fact]
    public void CameraStreamQuality_IsDefined_ShouldReturnFalseForInvalidValue()
    {
        // Act & Assert
        Enum.IsDefined((CameraStreamQuality)99).Should().BeFalse();
    }

    [Fact]
    public void CameraStreamQuality_ShouldSerializeAsString_ViaJsonConverter()
    {
        // Arrange
        var quality = CameraStreamQuality.HD;

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(quality);

        // Assert
        json.Should().Be("\"HD\"");
    }

    [Fact]
    public void CameraStreamQuality_ShouldDeserializeFromString_ViaJsonConverter()
    {
        // Arrange
        var json = "\"FullHD\"";

        // Act
        var quality = System.Text.Json.JsonSerializer.Deserialize<CameraStreamQuality>(json);

        // Assert
        quality.Should().Be(CameraStreamQuality.FullHD);
    }
}
