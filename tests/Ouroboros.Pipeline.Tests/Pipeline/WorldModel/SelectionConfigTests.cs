namespace Ouroboros.Tests.Pipeline.WorldModel;

using Ouroboros.Pipeline.WorldModel;

[Trait("Category", "Unit")]
public class SelectionConfigTests
{
    [Fact]
    public void Default_HasExpectedValues()
    {
        var config = SelectionConfig.Default;

        config.MaxTools.Should().Be(5);
        config.MinConfidence.Should().Be(0.3);
        config.OptimizeFor.Should().Be(OptimizationStrategy.Balanced);
        config.AllowParallelExecution.Should().BeTrue();
    }

    [Fact]
    public void ForCost_HasLowerMaxTools()
    {
        var config = SelectionConfig.ForCost();

        config.MaxTools.Should().Be(3);
        config.OptimizeFor.Should().Be(OptimizationStrategy.Cost);
        config.AllowParallelExecution.Should().BeFalse();
    }

    [Fact]
    public void ForSpeed_HasLowestMaxTools()
    {
        var config = SelectionConfig.ForSpeed();

        config.MaxTools.Should().Be(2);
        config.OptimizeFor.Should().Be(OptimizationStrategy.Speed);
    }

    [Fact]
    public void ForQuality_HasHigherMaxTools()
    {
        var config = SelectionConfig.ForQuality();

        config.MaxTools.Should().Be(10);
        config.OptimizeFor.Should().Be(OptimizationStrategy.Quality);
    }

    [Fact]
    public void WithMaxTools_EnforcesMinimumOfOne()
    {
        var config = SelectionConfig.Default.WithMaxTools(0);
        config.MaxTools.Should().Be(1);
    }

    [Fact]
    public void WithMinConfidence_ClampsToRange()
    {
        SelectionConfig.Default.WithMinConfidence(1.5).MinConfidence.Should().Be(1.0);
        SelectionConfig.Default.WithMinConfidence(-0.5).MinConfidence.Should().Be(0.0);
    }
}
