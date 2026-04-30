namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class ChatRuntimeSettingsTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var settings = new ChatRuntimeSettings();

        settings.Temperature.Should().Be(0.7);
        settings.MaxTokens.Should().Be(512);
        settings.TimeoutSeconds.Should().Be(60);
        settings.Stream.Should().BeFalse();
        settings.Culture.Should().BeNull();
        settings.ThinkingBudgetTokens.Should().BeNull();
    }

    [Fact]
    public void CustomValues_ArePreserved()
    {
        var settings = new ChatRuntimeSettings(
            Temperature: 0.5,
            MaxTokens: 1024,
            TimeoutSeconds: 120,
            Stream: true,
            Culture: "de-DE",
            ThinkingBudgetTokens: 500);

        settings.Temperature.Should().Be(0.5);
        settings.MaxTokens.Should().Be(1024);
        settings.Stream.Should().BeTrue();
        settings.Culture.Should().Be("de-DE");
        settings.ThinkingBudgetTokens.Should().Be(500);
    }
}
