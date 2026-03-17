using FluentAssertions;

namespace Ouroboros.Tests.Memory;

[Trait("Category", "Unit")]
public class ConsolidationStrategyTests
{
    [Fact]
    public void Compress_Exists()
    {
        // Arrange & Act
        var value = ConsolidationStrategy.Compress;

        // Assert
        value.Should().BeDefined();
        ((int)value).Should().Be(0);
    }

    [Fact]
    public void Abstract_Exists()
    {
        // Arrange & Act
        var value = ConsolidationStrategy.Abstract;

        // Assert
        value.Should().BeDefined();
        ((int)value).Should().Be(1);
    }

    [Fact]
    public void Prune_Exists()
    {
        // Arrange & Act
        var value = ConsolidationStrategy.Prune;

        // Assert
        value.Should().BeDefined();
        ((int)value).Should().Be(2);
    }

    [Fact]
    public void Hierarchical_Exists()
    {
        // Arrange & Act
        var value = ConsolidationStrategy.Hierarchical;

        // Assert
        value.Should().BeDefined();
        ((int)value).Should().Be(3);
    }

    [Fact]
    public void Enum_HasExactlyFourValues()
    {
        // Arrange & Act
        var values = Enum.GetValues<ConsolidationStrategy>();

        // Assert
        values.Should().HaveCount(4);
    }

    [Theory]
    [InlineData(ConsolidationStrategy.Compress, "Compress")]
    [InlineData(ConsolidationStrategy.Abstract, "Abstract")]
    [InlineData(ConsolidationStrategy.Prune, "Prune")]
    [InlineData(ConsolidationStrategy.Hierarchical, "Hierarchical")]
    public void ToString_ReturnsExpectedName(ConsolidationStrategy strategy, string expectedName)
    {
        // Act
        var name = strategy.ToString();

        // Assert
        name.Should().Be(expectedName);
    }

    [Theory]
    [InlineData("Compress", true)]
    [InlineData("Abstract", true)]
    [InlineData("Prune", true)]
    [InlineData("Hierarchical", true)]
    [InlineData("Unknown", false)]
    public void TryParse_WithValidAndInvalidNames_ReturnsExpected(string name, bool expectedResult)
    {
        // Act
        var result = Enum.TryParse<ConsolidationStrategy>(name, out var parsed);

        // Assert
        result.Should().Be(expectedResult);
    }
}
