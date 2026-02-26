namespace Ouroboros.Tests.Routing;

[Trait("Category", "Unit")]
public sealed class HybridRoutingOptionsTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var options = new HybridRoutingOptions();

        options.Enabled.Should().BeTrue();
        options.DefaultOllamaModel.Should().Be("llama3.1:8b");
        options.ReasoningOllamaModel.Should().Be("deepseek-r1:32b");
        options.UseDeepSeekForPlanning.Should().BeTrue();
        options.FallbackToLocal.Should().BeTrue();
    }

    [Fact]
    public void CustomValues_ArePreserved()
    {
        var options = new HybridRoutingOptions(
            Enabled: false,
            DefaultOllamaModel: "custom:7b",
            ReasoningOllamaModel: "custom:32b",
            UseDeepSeekForPlanning: false,
            FallbackToLocal: false);

        options.Enabled.Should().BeFalse();
        options.DefaultOllamaModel.Should().Be("custom:7b");
    }
}
