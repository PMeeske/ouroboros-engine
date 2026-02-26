namespace Ouroboros.Tests.Tapo;

[Trait("Category", "Unit")]
public sealed class TapoVoiceOutputConfigTests
{
    [Fact]
    public void Ctor_DefaultValues()
    {
        var config = new TapoVoiceOutputConfig("speaker-1");

        config.DeviceName.Should().Be("speaker-1");
        config.Volume.Should().Be(75);
        config.SampleRate.Should().Be(16000);
    }

    [Fact]
    public void Ctor_CustomValues()
    {
        var config = new TapoVoiceOutputConfig("speaker-2", Volume: 50, SampleRate: 44100);

        config.Volume.Should().Be(50);
        config.SampleRate.Should().Be(44100);
    }
}
