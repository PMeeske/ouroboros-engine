namespace Ouroboros.Tests.Pipeline.Learning;

using Ouroboros.Pipeline.Learning;

[Trait("Category", "Unit")]
public class LearningStrategyTests
{
    [Fact]
    public void Default_HasBalancedParameters()
    {
        var strategy = LearningStrategy.Default;

        strategy.LearningRate.Should().BeGreaterThan(0.0);
        strategy.ExplorationRate.Should().BeGreaterThan(0.0);
        strategy.DiscountFactor.Should().BeGreaterThan(0.0);
    }

    [Fact]
    public void Exploratory_HasHighExplorationRate()
    {
        var strategy = LearningStrategy.Exploratory();

        strategy.ExplorationRate.Should().BeGreaterThan(LearningStrategy.Default.ExplorationRate);
    }

    [Fact]
    public void Exploitative_HasLowExplorationRate()
    {
        var strategy = LearningStrategy.Exploitative();

        strategy.ExplorationRate.Should().BeLessThan(LearningStrategy.Default.ExplorationRate);
    }

    [Fact]
    public void WithLearningRate_CreatesNewStrategyWithUpdatedRate()
    {
        var strategy = LearningStrategy.Default.WithLearningRate(0.01);

        strategy.LearningRate.Should().Be(0.01);
    }

    [Fact]
    public void WithExplorationRate_CreatesNewStrategyWithUpdatedRate()
    {
        var strategy = LearningStrategy.Default.WithExplorationRate(0.5);

        strategy.ExplorationRate.Should().Be(0.5);
    }

    [Fact]
    public void WithDiscountFactor_CreatesNewStrategyWithUpdatedFactor()
    {
        var strategy = LearningStrategy.Default.WithDiscountFactor(0.95);

        strategy.DiscountFactor.Should().Be(0.95);
    }

    [Fact]
    public void Validate_ReturnsSuccess_ForValidStrategy()
    {
        var result = LearningStrategy.Default.Validate();
        result.IsSuccess.Should().BeTrue();
    }
}
