namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class SessionMetricsTests
{
    [Fact]
    public void TotalTokens_ReturnsSumOfInputAndOutput()
    {
        var metrics = new SessionMetrics(
            Model: "gpt-4",
            Provider: "openai",
            TotalRequests: 10,
            TotalInputTokens: 5000,
            TotalOutputTokens: 3000,
            TotalLatency: TimeSpan.FromSeconds(30),
            TotalCost: 0.15m,
            AverageLatency: TimeSpan.FromSeconds(3));

        metrics.TotalTokens.Should().Be(8000);
    }

    [Fact]
    public void ToCostString_WithZeroCost_OmitsCost()
    {
        var metrics = new SessionMetrics(
            "ollama", "local", 5, 1000, 500, TimeSpan.FromSeconds(10), 0m, TimeSpan.FromSeconds(2));

        var result = metrics.ToCostString();

        result.Should().Contain("1,500 tokens");
        result.Should().NotContain("$");
    }

    [Fact]
    public void ToCostString_WithCost_IncludesCost()
    {
        var metrics = new SessionMetrics(
            "gpt-4", "openai", 5, 1000, 500, TimeSpan.FromSeconds(10), 0.0234m, TimeSpan.FromSeconds(2));

        var result = metrics.ToCostString();

        result.Should().Contain("1,500 tokens");
        result.Should().Contain("$0.0234");
    }

    [Fact]
    public void Properties_ArePreserved()
    {
        var metrics = new SessionMetrics(
            "claude", "anthropic", 3, 2000, 1500, TimeSpan.FromSeconds(15), 0.05m, TimeSpan.FromSeconds(5));

        metrics.Model.Should().Be("claude");
        metrics.Provider.Should().Be("anthropic");
        metrics.TotalRequests.Should().Be(3);
        metrics.TotalInputTokens.Should().Be(2000);
        metrics.TotalOutputTokens.Should().Be(1500);
        metrics.TotalLatency.Should().Be(TimeSpan.FromSeconds(15));
        metrics.TotalCost.Should().Be(0.05m);
        metrics.AverageLatency.Should().Be(TimeSpan.FromSeconds(5));
    }
}
