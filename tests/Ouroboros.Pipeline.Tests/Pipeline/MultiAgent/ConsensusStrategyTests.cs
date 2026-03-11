namespace Ouroboros.Tests.Pipeline.MultiAgent;

using Ouroboros.Pipeline.MultiAgent;

[Trait("Category", "Unit")]
public class ConsensusStrategyTests
{
    [Fact]
    public void Majority_IsDefined()
    {
        Enum.IsDefined(typeof(ConsensusStrategy), ConsensusStrategy.Majority).Should().BeTrue();
    }

    [Fact]
    public void SuperMajority_IsDefined()
    {
        Enum.IsDefined(typeof(ConsensusStrategy), ConsensusStrategy.SuperMajority).Should().BeTrue();
    }

    [Fact]
    public void Unanimous_IsDefined()
    {
        Enum.IsDefined(typeof(ConsensusStrategy), ConsensusStrategy.Unanimous).Should().BeTrue();
    }

    [Fact]
    public void WeightedByConfidence_IsDefined()
    {
        Enum.IsDefined(typeof(ConsensusStrategy), ConsensusStrategy.WeightedByConfidence).Should().BeTrue();
    }

    [Fact]
    public void HighestConfidence_IsDefined()
    {
        Enum.IsDefined(typeof(ConsensusStrategy), ConsensusStrategy.HighestConfidence).Should().BeTrue();
    }

    [Fact]
    public void RankedChoice_IsDefined()
    {
        Enum.IsDefined(typeof(ConsensusStrategy), ConsensusStrategy.RankedChoice).Should().BeTrue();
    }

    [Fact]
    public void Enum_HasExpectedCount()
    {
        // Act
        var values = Enum.GetValues<ConsensusStrategy>();

        // Assert
        values.Should().HaveCount(6);
    }

    [Fact]
    public void Majority_HasExpectedValue()
    {
        ((int)ConsensusStrategy.Majority).Should().Be(0);
    }

    [Fact]
    public void SuperMajority_HasExpectedValue()
    {
        ((int)ConsensusStrategy.SuperMajority).Should().Be(1);
    }

    [Fact]
    public void Unanimous_HasExpectedValue()
    {
        ((int)ConsensusStrategy.Unanimous).Should().Be(2);
    }

    [Fact]
    public void WeightedByConfidence_HasExpectedValue()
    {
        ((int)ConsensusStrategy.WeightedByConfidence).Should().Be(3);
    }

    [Fact]
    public void HighestConfidence_HasExpectedValue()
    {
        ((int)ConsensusStrategy.HighestConfidence).Should().Be(4);
    }

    [Fact]
    public void RankedChoice_HasExpectedValue()
    {
        ((int)ConsensusStrategy.RankedChoice).Should().Be(5);
    }
}
