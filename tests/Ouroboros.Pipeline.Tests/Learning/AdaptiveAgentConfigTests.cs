using Ouroboros.Pipeline.Learning;

namespace Ouroboros.Tests.Learning;

public class AdaptiveAgentConfigTests
{
    [Fact]
    public void Default_ReturnsSensibleDefaults()
    {
        // Act
        var config = AdaptiveAgentConfig.Default;

        // Assert
        config.AdaptationThreshold.Should().Be(0.1);
        config.RollbackThreshold.Should().Be(0.15);
        config.MinInteractionsBeforeAdaptation.Should().Be(50);
        config.EmaAlpha.Should().Be(0.1);
        config.StagnationWindowSize.Should().Be(20);
        config.MaxAdaptationHistory.Should().Be(100);
    }

    [Fact]
    public void Aggressive_HasLowerThresholds()
    {
        // Act
        var config = AdaptiveAgentConfig.Aggressive;

        // Assert
        config.AdaptationThreshold.Should().Be(0.05);
        config.RollbackThreshold.Should().Be(0.1);
        config.MinInteractionsBeforeAdaptation.Should().Be(20);
        config.EmaAlpha.Should().Be(0.2);
        config.StagnationWindowSize.Should().Be(10);
        config.MaxAdaptationHistory.Should().Be(200);
    }

    [Fact]
    public void Conservative_HasHigherThresholds()
    {
        // Act
        var config = AdaptiveAgentConfig.Conservative;

        // Assert
        config.AdaptationThreshold.Should().Be(0.2);
        config.RollbackThreshold.Should().Be(0.25);
        config.MinInteractionsBeforeAdaptation.Should().Be(100);
        config.EmaAlpha.Should().Be(0.05);
        config.StagnationWindowSize.Should().Be(50);
        config.MaxAdaptationHistory.Should().Be(50);
    }

    [Fact]
    public void Constructor_WithCustomValues_SetsAllProperties()
    {
        // Act
        var config = new AdaptiveAgentConfig(
            AdaptationThreshold: 0.3,
            RollbackThreshold: 0.4,
            MinInteractionsBeforeAdaptation: 200,
            EmaAlpha: 0.5,
            StagnationWindowSize: 30,
            MaxAdaptationHistory: 150);

        // Assert
        config.AdaptationThreshold.Should().Be(0.3);
        config.RollbackThreshold.Should().Be(0.4);
        config.MinInteractionsBeforeAdaptation.Should().Be(200);
        config.EmaAlpha.Should().Be(0.5);
        config.StagnationWindowSize.Should().Be(30);
        config.MaxAdaptationHistory.Should().Be(150);
    }
}
