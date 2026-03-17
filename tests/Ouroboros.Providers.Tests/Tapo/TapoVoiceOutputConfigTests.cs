using FluentAssertions;

namespace Ouroboros.Tests.Tapo;

[Trait("Category", "Unit")]
public class TapoVoiceOutputConfigTests
{
    [Fact]
    public void TapoVoiceOutputConfig_Construction_WithDefaults_ShouldSetDefaultValues()
    {
        // Arrange & Act
        var config = new TapoVoiceOutputConfig("kitchen-speaker");

        // Assert
        config.DeviceName.Should().Be("kitchen-speaker");
        config.Volume.Should().Be(75);
        config.SampleRate.Should().Be(16000);
    }

    [Fact]
    public void TapoVoiceOutputConfig_Construction_WithCustomValues_ShouldSetProperties()
    {
        // Arrange & Act
        var config = new TapoVoiceOutputConfig("bedroom-speaker", Volume: 50, SampleRate: 44100);

        // Assert
        config.DeviceName.Should().Be("bedroom-speaker");
        config.Volume.Should().Be(50);
        config.SampleRate.Should().Be(44100);
    }

    [Fact]
    public void TapoVoiceOutputConfig_Equality_SameValues_ShouldBeEqual()
    {
        // Arrange
        var a = new TapoVoiceOutputConfig("speaker1");
        var b = new TapoVoiceOutputConfig("speaker1");

        // Act & Assert
        a.Should().Be(b);
    }

    [Fact]
    public void TapoVoiceOutputConfig_Equality_DifferentValues_ShouldNotBeEqual()
    {
        // Arrange
        var a = new TapoVoiceOutputConfig("speaker1", Volume: 50);
        var b = new TapoVoiceOutputConfig("speaker1", Volume: 75);

        // Act & Assert
        a.Should().NotBe(b);
    }

    [Fact]
    public void TapoVoiceOutputConfig_With_ShouldCreateModifiedCopy()
    {
        // Arrange
        var original = new TapoVoiceOutputConfig("speaker1");

        // Act
        var modified = original with { Volume = 100 };

        // Assert
        modified.Volume.Should().Be(100);
        original.Volume.Should().Be(75);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    public void TapoVoiceOutputConfig_Volume_ShouldAcceptValidValues(int volume)
    {
        // Arrange & Act
        var config = new TapoVoiceOutputConfig("speaker", Volume: volume);

        // Assert
        config.Volume.Should().Be(volume);
    }
}
