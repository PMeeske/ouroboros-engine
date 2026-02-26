using Ouroboros.Providers.DuckDuckGo;

namespace Ouroboros.Tests.DuckDuckGo;

[Trait("Category", "Unit")]
public sealed class DuckDuckGoMcpClientOptionsTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var options = new DuckDuckGoMcpClientOptions();

        options.SearchBaseUrl.Should().Be("https://html.duckduckgo.com");
        options.InstantAnswerBaseUrl.Should().Be("https://api.duckduckgo.com");
        options.Timeout.Should().Be(TimeSpan.FromSeconds(15));
        options.MaxRetries.Should().Be(2);
        options.SafeSearch.Should().Be("moderate");
    }

    [Fact]
    public void IsValid_WithDefaults_ReturnsTrue()
    {
        var options = new DuckDuckGoMcpClientOptions();
        options.IsValid().Should().BeTrue();
    }

    [Fact]
    public void IsValid_EmptySearchBaseUrl_ReturnsFalse()
    {
        var options = new DuckDuckGoMcpClientOptions { SearchBaseUrl = "" };
        options.IsValid().Should().BeFalse();
    }

    [Fact]
    public void IsValid_EmptyInstantAnswerBaseUrl_ReturnsFalse()
    {
        var options = new DuckDuckGoMcpClientOptions { InstantAnswerBaseUrl = "" };
        options.IsValid().Should().BeFalse();
    }

    [Fact]
    public void IsValid_ZeroTimeout_ReturnsFalse()
    {
        var options = new DuckDuckGoMcpClientOptions { Timeout = TimeSpan.Zero };
        options.IsValid().Should().BeFalse();
    }
}
