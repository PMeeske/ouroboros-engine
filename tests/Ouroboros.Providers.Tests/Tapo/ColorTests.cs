using FluentAssertions;

namespace Ouroboros.Tests.Tapo;

[Trait("Category", "Unit")]
public class ColorTests
{
    [Fact]
    public void Color_Construction_ShouldSetRgbValues()
    {
        // Arrange & Act
        var color = new Color { Red = 255, Green = 128, Blue = 0 };

        // Assert
        color.Red.Should().Be(255);
        color.Green.Should().Be(128);
        color.Blue.Should().Be(0);
    }

    [Fact]
    public void Color_MinValues_ShouldBeZero()
    {
        // Arrange & Act
        var color = new Color { Red = 0, Green = 0, Blue = 0 };

        // Assert
        color.Red.Should().Be(0);
        color.Green.Should().Be(0);
        color.Blue.Should().Be(0);
    }

    [Fact]
    public void Color_MaxValues_ShouldBe255()
    {
        // Arrange & Act
        var color = new Color { Red = 255, Green = 255, Blue = 255 };

        // Assert
        color.Red.Should().Be(255);
        color.Green.Should().Be(255);
        color.Blue.Should().Be(255);
    }

    [Fact]
    public void Color_Equality_SameValues_ShouldBeEqual()
    {
        // Arrange
        var a = new Color { Red = 100, Green = 150, Blue = 200 };
        var b = new Color { Red = 100, Green = 150, Blue = 200 };

        // Act & Assert
        a.Should().Be(b);
    }

    [Fact]
    public void Color_Equality_DifferentValues_ShouldNotBeEqual()
    {
        // Arrange
        var a = new Color { Red = 100, Green = 150, Blue = 200 };
        var b = new Color { Red = 100, Green = 150, Blue = 201 };

        // Act & Assert
        a.Should().NotBe(b);
    }

    [Fact]
    public void Color_JsonSerialization_ShouldUseSnakeCasePropertyNames()
    {
        // Arrange
        var color = new Color { Red = 255, Green = 128, Blue = 64 };

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(color);

        // Assert
        json.Should().Contain("\"red\":");
        json.Should().Contain("\"green\":");
        json.Should().Contain("\"blue\":");
    }

    [Fact]
    public void Color_JsonDeserialization_ShouldRoundtrip()
    {
        // Arrange
        var original = new Color { Red = 42, Green = 84, Blue = 168 };

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(original);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<Color>(json);

        // Assert
        deserialized.Should().Be(original);
    }
}
