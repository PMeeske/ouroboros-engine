namespace Ouroboros.Tests.Providers;

[Trait("Category", "Unit")]
public sealed class LlmCostTrackerTests2
{
    [Theory]
    [InlineData("claude-sonnet-4-20250514", "Anthropic")]
    [InlineData("claude-3-haiku", "Anthropic")]
    [InlineData("gpt-4o", "OpenAI")]
    [InlineData("gpt-4o-mini", "OpenAI")]
    [InlineData("deepseek-chat", "DeepSeek")]
    [InlineData("deepseek-reasoner", "DeepSeek")]
    [InlineData("gemini-2.0-flash", "Google")]
    [InlineData("mistral-large", "Mistral")]
    [InlineData("llama3.2", "Local")]
    [InlineData("phi3", "Local")]
    public void GetProvider_KnownModels_ReturnsCorrectProvider(string model, string expected)
    {
        LlmCostTracker.GetProvider(model).Should().Be(expected);
    }

    [Theory]
    [InlineData("claude-unknown-model", "Anthropic")]
    [InlineData("gpt-99", "OpenAI")]
    [InlineData("o1-something", "OpenAI")]
    [InlineData("deepseek-v99", "DeepSeek")]
    [InlineData("gemini-99-turbo", "Google")]
    [InlineData("mistral-tiny", "Mistral")]
    [InlineData("llama-99b", "Meta")]
    [InlineData("phi-4", "Microsoft")]
    [InlineData("command-r", "Cohere")]
    [InlineData("qwen3-72b", "Alibaba")]
    [InlineData("grok-2", "xAI")]
    [InlineData("stable-diffusion", "Stability AI")]
    [InlineData("gemma2-9b", "Local")]
    public void GetProvider_PatternMatching_ReturnsCorrectProvider(string model, string expected)
    {
        LlmCostTracker.GetProvider(model).Should().Be(expected);
    }

    [Fact]
    public void GetProvider_EmptyString_ReturnsUnknown()
    {
        LlmCostTracker.GetProvider("").Should().Be("Unknown");
    }

    [Fact]
    public void GetProvider_NullString_ReturnsUnknown()
    {
        LlmCostTracker.GetProvider(null!).Should().Be("Unknown");
    }

    [Fact]
    public void GetPricing_KnownModel_ReturnsPricing()
    {
        var pricing = LlmCostTracker.GetPricing("gpt-4o");
        pricing.Provider.Should().Be("OpenAI");
        pricing.InputCostPer1M.Should().Be(2.50m);
        pricing.OutputCostPer1M.Should().Be(10.00m);
    }

    [Fact]
    public void GetPricing_LocalModel_ReturnsFree()
    {
        var pricing = LlmCostTracker.GetPricing("llama3");
        pricing.InputCostPer1M.Should().Be(0m);
        pricing.OutputCostPer1M.Should().Be(0m);
    }

    [Fact]
    public void GetPricing_UnknownModel_ReturnsZeroCost()
    {
        var pricing = LlmCostTracker.GetPricing("totally-unknown-model");
        pricing.InputCostPer1M.Should().Be(0m);
    }

    [Fact]
    public void CalculateCost_KnownModel_CalculatesCorrectly()
    {
        var cost = LlmCostTracker.CalculateCost("gpt-4o", 1_000_000, 1_000_000);
        cost.Should().Be(12.50m); // 2.50 + 10.00
    }

    [Fact]
    public void CalculateCost_ZeroTokens_ReturnsZero()
    {
        var cost = LlmCostTracker.CalculateCost("gpt-4o", 0, 0);
        cost.Should().Be(0m);
    }

    [Fact]
    public void CalculateCost_LocalModel_ReturnsZero()
    {
        var cost = LlmCostTracker.CalculateCost("llama3", 1_000_000, 1_000_000);
        cost.Should().Be(0m);
    }

    [Fact]
    public void StartAndEndRequest_TracksMetrics()
    {
        var tracker = new LlmCostTracker("gpt-4o", "OpenAI");
        tracker.StartRequest();
        var metrics = tracker.EndRequest(100, 50);

        metrics.Should().NotBeNull();
        metrics.InputTokens.Should().Be(100);
        metrics.OutputTokens.Should().Be(50);
    }

    [Fact]
    public void GetSessionMetrics_AfterRequests_ReturnsTotals()
    {
        var tracker = new LlmCostTracker("gpt-4o", "OpenAI");

        tracker.StartRequest();
        tracker.EndRequest(100, 50);
        tracker.StartRequest();
        tracker.EndRequest(200, 100);

        var session = tracker.GetSessionMetrics();

        session.TotalRequests.Should().Be(2);
        session.TotalInputTokens.Should().Be(300);
        session.TotalOutputTokens.Should().Be(150);
        session.Model.Should().Be("gpt-4o");
        session.Provider.Should().Be("OpenAI");
    }

    [Fact]
    public void Reset_ClearsAllMetrics()
    {
        var tracker = new LlmCostTracker("gpt-4o");
        tracker.StartRequest();
        tracker.EndRequest(100, 50);

        tracker.Reset();

        var session = tracker.GetSessionMetrics();
        session.TotalRequests.Should().Be(0);
        session.TotalInputTokens.Should().Be(0);
    }

    [Fact]
    public void FormatSessionSummary_ReturnsFormattedString()
    {
        var tracker = new LlmCostTracker("gpt-4o", "OpenAI");
        tracker.StartRequest();
        tracker.EndRequest(1000, 500);

        var summary = tracker.FormatSessionSummary();

        summary.Should().Contain("OpenAI");
        summary.Should().Contain("gpt-4o");
        summary.Should().Contain("Requests: 1");
    }

    [Fact]
    public void GetCostString_ZeroCost_ReturnsTokensOnly()
    {
        var tracker = new LlmCostTracker("llama3", "Local");
        tracker.StartRequest();
        tracker.EndRequest(100, 50);

        var costStr = tracker.GetCostString();
        costStr.Should().Contain("150");
        costStr.Should().NotContain("$");
    }

    [Fact]
    public void GetCostString_WithCost_ReturnsTokensAndCost()
    {
        var tracker = new LlmCostTracker("gpt-4o", "OpenAI");
        tracker.StartRequest();
        tracker.EndRequest(1_000_000, 1_000_000);

        var costStr = tracker.GetCostString();
        costStr.Should().Contain("$");
    }

    [Fact]
    public void GetCostAwarenessPrompt_LocalModel_IndicatesFree()
    {
        var prompt = LlmCostTracker.GetCostAwarenessPrompt("llama3");
        prompt.Should().Contain("free");
    }

    [Fact]
    public void GetCostAwarenessPrompt_PaidModel_IncludesPricing()
    {
        var prompt = LlmCostTracker.GetCostAwarenessPrompt("gpt-4o");
        prompt.Should().Contain("COST AWARENESS");
        prompt.Should().Contain("$");
    }

    [Fact]
    public void EndRequest_WithoutStartRequest_StillRecords()
    {
        var tracker = new LlmCostTracker("gpt-4o");
        // Don't call StartRequest
        var metrics = tracker.EndRequest(100, 50);

        metrics.Latency.Should().Be(TimeSpan.Zero);
        metrics.InputTokens.Should().Be(100);
    }

    [Fact]
    public void GetSessionMetrics_AverageLatency_CalculatedCorrectly()
    {
        var tracker = new LlmCostTracker("gpt-4o");

        tracker.StartRequest();
        tracker.EndRequest(100, 50);

        var session = tracker.GetSessionMetrics();
        session.AverageLatency.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }
}
