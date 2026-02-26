namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class RequestMetricsTests
{
    [Fact]
    public void TotalTokens_ReturnsSumOfInputAndOutput()
    {
        var metrics = new RequestMetrics(
            "gpt-4", 100, 200, TimeSpan.FromSeconds(2), 0.001m, DateTime.UtcNow);

        metrics.TotalTokens.Should().Be(300);
    }

    [Fact]
    public void TokensPerSecond_CalculatesCorrectly()
    {
        var metrics = new RequestMetrics(
            "gpt-4", 100, 200, TimeSpan.FromSeconds(2), 0m, DateTime.UtcNow);

        metrics.TokensPerSecond.Should().Be(100.0); // 200 / 2
    }

    [Fact]
    public void TokensPerSecond_ZeroLatency_ReturnsZero()
    {
        var metrics = new RequestMetrics(
            "gpt-4", 100, 200, TimeSpan.Zero, 0m, DateTime.UtcNow);

        metrics.TokensPerSecond.Should().Be(0.0);
    }

    [Fact]
    public void ToString_WithZeroCost_OmitsCost()
    {
        var metrics = new RequestMetrics(
            "ollama", 100, 200, TimeSpan.FromSeconds(2), 0m, DateTime.UtcNow);

        var result = metrics.ToString();

        result.Should().Contain("100");
        result.Should().Contain("200");
        result.Should().NotContain("$");
    }

    [Fact]
    public void ToString_WithCost_IncludesCost()
    {
        var metrics = new RequestMetrics(
            "gpt-4", 100, 200, TimeSpan.FromSeconds(2), 0.0050m, DateTime.UtcNow);

        var result = metrics.ToString();

        result.Should().Contain("$0.0050");
    }

    [Fact]
    public void Properties_ArePreserved()
    {
        var timestamp = DateTime.UtcNow;
        var metrics = new RequestMetrics(
            "claude", 500, 1000, TimeSpan.FromSeconds(5), 0.01m, timestamp);

        metrics.Model.Should().Be("claude");
        metrics.InputTokens.Should().Be(500);
        metrics.OutputTokens.Should().Be(1000);
        metrics.Latency.Should().Be(TimeSpan.FromSeconds(5));
        metrics.Cost.Should().Be(0.01m);
        metrics.Timestamp.Should().Be(timestamp);
    }
}
