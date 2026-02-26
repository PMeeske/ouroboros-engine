namespace Ouroboros.Tests.Tapo;

[Trait("Category", "Unit")]
public sealed class ColorTests
{
    [Fact]
    public void Ctor_SetsRgbValues()
    {
        var color = new Color { Red = 255, Green = 128, Blue = 0 };

        color.Red.Should().Be(255);
        color.Green.Should().Be(128);
        color.Blue.Should().Be(0);
    }

    [Fact]
    public void RoundTrips_ThroughJson()
    {
        var original = new Color { Red = 10, Green = 20, Blue = 30 };
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<Color>(json);

        deserialized.Should().Be(original);
    }

    [Fact]
    public void JsonPropertyNames_AreLowerCase()
    {
        var color = new Color { Red = 1, Green = 2, Blue = 3 };
        var json = JsonSerializer.Serialize(color);

        json.Should().Contain("\"red\"");
        json.Should().Contain("\"green\"");
        json.Should().Contain("\"blue\"");
    }
}
