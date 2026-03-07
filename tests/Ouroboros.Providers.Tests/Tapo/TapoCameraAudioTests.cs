namespace Ouroboros.Tests.Tapo;

[Trait("Category", "Unit")]
public sealed class TapoCameraAudioTests
{
    [Fact]
    public void Ctor_SetsAllProperties()
    {
        var data = new byte[] { 1, 2, 3 };
        var ts = DateTime.UtcNow;

        var audio = new TapoCameraAudio(data, 44100, 2, TimeSpan.FromSeconds(5), ts, "cam1");

        audio.Data.Should().BeSameAs(data);
        audio.SampleRate.Should().Be(44100);
        audio.Channels.Should().Be(2);
        audio.Duration.Should().Be(TimeSpan.FromSeconds(5));
        audio.Timestamp.Should().Be(ts);
        audio.CameraName.Should().Be("cam1");
    }

    [Fact]
    public void Records_AreEqualByValue()
    {
        var data = new byte[] { 1, 2, 3 };
        var ts = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var a = new TapoCameraAudio(data, 16000, 1, TimeSpan.FromSeconds(1), ts, "cam");
        var b = new TapoCameraAudio(data, 16000, 1, TimeSpan.FromSeconds(1), ts, "cam");

        a.Should().Be(b);
    }
}
