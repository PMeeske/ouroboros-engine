namespace Ouroboros.Tests.Pipeline.Learning;

using Ouroboros.Pipeline.Learning;

[Trait("Category", "Unit")]
public class LearningStateTests
{
    [Fact]
    public void Exploring_IsDefined()
    {
        Enum.IsDefined(typeof(LearningState), LearningState.Exploring).Should().BeTrue();
    }

    [Fact]
    public void Converging_IsDefined()
    {
        Enum.IsDefined(typeof(LearningState), LearningState.Converging).Should().BeTrue();
    }

    [Fact]
    public void Converged_IsDefined()
    {
        Enum.IsDefined(typeof(LearningState), LearningState.Converged).Should().BeTrue();
    }

    [Fact]
    public void Diverging_IsDefined()
    {
        Enum.IsDefined(typeof(LearningState), LearningState.Diverging).Should().BeTrue();
    }

    [Fact]
    public void Stagnant_IsDefined()
    {
        Enum.IsDefined(typeof(LearningState), LearningState.Stagnant).Should().BeTrue();
    }

    [Fact]
    public void Enum_HasExpectedCount()
    {
        // Act
        var values = Enum.GetValues<LearningState>();

        // Assert
        values.Should().HaveCount(5);
    }

    [Fact]
    public void Exploring_HasExpectedValue()
    {
        ((int)LearningState.Exploring).Should().Be(0);
    }

    [Fact]
    public void Converging_HasExpectedValue()
    {
        ((int)LearningState.Converging).Should().Be(1);
    }

    [Fact]
    public void Converged_HasExpectedValue()
    {
        ((int)LearningState.Converged).Should().Be(2);
    }

    [Fact]
    public void Diverging_HasExpectedValue()
    {
        ((int)LearningState.Diverging).Should().Be(3);
    }

    [Fact]
    public void Stagnant_HasExpectedValue()
    {
        ((int)LearningState.Stagnant).Should().Be(4);
    }
}
