namespace Ouroboros.Tests.Pipeline.Memory;

/// <summary>
/// Unit tests for ConsolidationStrategy enum.
/// </summary>
[Trait("Category", "Unit")]
public class ConsolidationStrategyTests
{
    [Fact]
    public void ConsolidationStrategy_ShouldHaveAllExpectedValues()
    {
        // Arrange & Act
        var strategies = Enum.GetValues<ConsolidationStrategy>();

        // Assert
        strategies.Should().Contain(ConsolidationStrategy.Compress);
        strategies.Should().Contain(ConsolidationStrategy.Abstract);
        strategies.Should().Contain(ConsolidationStrategy.Prune);
        strategies.Should().Contain(ConsolidationStrategy.Hierarchical);
        strategies.Should().HaveCount(4);
    }
}