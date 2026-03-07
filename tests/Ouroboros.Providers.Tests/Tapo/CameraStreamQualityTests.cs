namespace Ouroboros.Tests.Tapo;

[Trait("Category", "Unit")]
public sealed class CameraStreamQualityTests
{
    [Theory]
    [InlineData(CameraStreamQuality.Low, 0)]
    [InlineData(CameraStreamQuality.Standard, 1)]
    [InlineData(CameraStreamQuality.HD, 2)]
    [InlineData(CameraStreamQuality.FullHD, 3)]
    [InlineData(CameraStreamQuality.QHD, 4)]
    public void Enum_HasExpectedValues(CameraStreamQuality quality, int expected)
    {
        ((int)quality).Should().Be(expected);
    }

    [Fact]
    public void Enum_HasFiveMembers()
    {
        Enum.GetValues<CameraStreamQuality>().Should().HaveCount(5);
    }

    [Fact]
    public void SerializesToString()
    {
        var json = JsonSerializer.Serialize(CameraStreamQuality.FullHD);
        json.Should().Contain("FullHD");
    }

    [Fact]
    public void DeserializesFromString()
    {
        var result = JsonSerializer.Deserialize<CameraStreamQuality>("\"QHD\"");
        result.Should().Be(CameraStreamQuality.QHD);
    }
}
