using FluentAssertions;
using Ouroboros.Pipeline.WorldModel;

namespace Ouroboros.Tests.WorldModel;

[Trait("Category", "Unit")]
public sealed class SelectionConfigTests
{
    [Fact]
    public void Default_HasExpectedValues()
    {
        // Act
        var config = SelectionConfig.Default;

        // Assert
        config.MaxTools.Should().Be(5);
        config.MinConfidence.Should().Be(0.3);
        config.OptimizeFor.Should().Be(OptimizationStrategy.Balanced);
        config.AllowParallelExecution.Should().BeTrue();
    }

    [Fact]
    public void ForCost_HasExpectedValues()
    {
        // Act
        var config = SelectionConfig.ForCost();

        // Assert
        config.MaxTools.Should().Be(3);
        config.MinConfidence.Should().Be(0.4);
        config.OptimizeFor.Should().Be(OptimizationStrategy.Cost);
        config.AllowParallelExecution.Should().BeFalse();
    }

    [Fact]
    public void ForSpeed_HasExpectedValues()
    {
        // Act
        var config = SelectionConfig.ForSpeed();

        // Assert
        config.MaxTools.Should().Be(2);
        config.MinConfidence.Should().Be(0.5);
        config.OptimizeFor.Should().Be(OptimizationStrategy.Speed);
        config.AllowParallelExecution.Should().BeTrue();
    }

    [Fact]
    public void ForQuality_HasExpectedValues()
    {
        // Act
        var config = SelectionConfig.ForQuality();

        // Assert
        config.MaxTools.Should().Be(10);
        config.MinConfidence.Should().Be(0.2);
        config.OptimizeFor.Should().Be(OptimizationStrategy.Quality);
        config.AllowParallelExecution.Should().BeTrue();
    }

    [Fact]
    public void WithMaxTools_ReturnsNewConfigWithUpdatedMaxTools()
    {
        // Arrange
        var original = SelectionConfig.Default;

        // Act
        var updated = original.WithMaxTools(8);

        // Assert
        updated.MaxTools.Should().Be(8);
        original.MaxTools.Should().Be(5); // original unchanged
    }

    [Fact]
    public void WithMaxTools_BelowOne_ClampsToOne()
    {
        // Act
        var config = SelectionConfig.Default.WithMaxTools(0);

        // Assert
        config.MaxTools.Should().Be(1);
    }

    [Fact]
    public void WithMaxTools_NegativeValue_ClampsToOne()
    {
        // Act
        var config = SelectionConfig.Default.WithMaxTools(-5);

        // Assert
        config.MaxTools.Should().Be(1);
    }

    [Fact]
    public void WithMinConfidence_ReturnsNewConfigWithUpdatedMinConfidence()
    {
        // Act
        var config = SelectionConfig.Default.WithMinConfidence(0.8);

        // Assert
        config.MinConfidence.Should().Be(0.8);
    }

    [Fact]
    public void WithMinConfidence_AboveOne_ClampsToOne()
    {
        // Act
        var config = SelectionConfig.Default.WithMinConfidence(1.5);

        // Assert
        config.MinConfidence.Should().Be(1.0);
    }

    [Fact]
    public void WithMinConfidence_BelowZero_ClampsToZero()
    {
        // Act
        var config = SelectionConfig.Default.WithMinConfidence(-0.5);

        // Assert
        config.MinConfidence.Should().Be(0.0);
    }

    [Fact]
    public void Constructor_CustomValues_SetsAllProperties()
    {
        // Act
        var config = new SelectionConfig(
            MaxTools: 7,
            MinConfidence: 0.6,
            OptimizeFor: OptimizationStrategy.Quality,
            AllowParallelExecution: false);

        // Assert
        config.MaxTools.Should().Be(7);
        config.MinConfidence.Should().Be(0.6);
        config.OptimizeFor.Should().Be(OptimizationStrategy.Quality);
        config.AllowParallelExecution.Should().BeFalse();
    }
}
