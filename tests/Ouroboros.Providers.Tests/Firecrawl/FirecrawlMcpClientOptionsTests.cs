using Ouroboros.Providers.Firecrawl;

namespace Ouroboros.Tests.Firecrawl;

[Trait("Category", "Unit")]
public sealed class FirecrawlMcpClientOptionsTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var options = new FirecrawlMcpClientOptions();

        options.ApiKey.Should().BeNull();
        options.BaseUrl.Should().Be("https://api.firecrawl.dev");
        options.ApiVersion.Should().Be("v1");
        options.Timeout.Should().Be(TimeSpan.FromSeconds(60));
        options.MaxRetries.Should().Be(3);
    }

    [Fact]
    public void ResolveApiKey_WithExplicitKey_ReturnsKey()
    {
        var options = new FirecrawlMcpClientOptions { ApiKey = "test-key" };
        options.ResolveApiKey().Should().Be("test-key");
    }

    [Fact]
    public void IsValid_WithApiKey_ReturnsTrue()
    {
        var options = new FirecrawlMcpClientOptions { ApiKey = "key" };
        options.IsValid().Should().BeTrue();
    }

    [Fact]
    public void IsValid_WithoutApiKey_ReturnsFalse()
    {
        var options = new FirecrawlMcpClientOptions();
        // Unless FIRECRAWL_API_KEY env var is set, should be false
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FIRECRAWL_API_KEY")))
        {
            options.IsValid().Should().BeFalse();
        }
    }
}
