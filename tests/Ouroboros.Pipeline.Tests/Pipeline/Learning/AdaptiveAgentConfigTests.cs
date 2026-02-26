namespace Ouroboros.Tests.Pipeline.Learning;

using Ouroboros.Pipeline.Learning;

[Trait("Category", "Unit")]
public class AdaptiveAgentConfigTests
{
    [Fact]
    public void Default_HasBalancedSettings()
    {
        var config = AdaptiveAgentConfig.Default;
        config.Should().NotBeNull();
    }

    [Fact]
    public void Aggressive_HasHigherRates()
    {
        var config = AdaptiveAgentConfig.Aggressive;
        config.Should().NotBeNull();
    }

    [Fact]
    public void Conservative_HasLowerRates()
    {
        var config = AdaptiveAgentConfig.Conservative;
        config.Should().NotBeNull();
    }

    [Fact]
    public void Configs_AreDifferent()
    {
        AdaptiveAgentConfig.Default.Should().NotBe(AdaptiveAgentConfig.Aggressive);
        AdaptiveAgentConfig.Default.Should().NotBe(AdaptiveAgentConfig.Conservative);
    }
}
