using Ouroboros.Pipeline.Learning;

namespace Ouroboros.Tests.Learning;

public class GradientLearnerConfigTests
{
    [Fact]
    public void Default_ReturnsSensibleDefaults()
    {
        // Act
        var config = GradientLearnerConfig.Default;

        // Assert
        config.LearningRate.Should().Be(0.01);
        config.Momentum.Should().Be(0.9);
        config.AdaptiveLearningRate.Should().BeTrue();
        config.GradientClipThreshold.Should().Be(1.0);
        config.MinConfidenceThreshold.Should().Be(0.1);
        config.BatchAccumulationSize.Should().Be(1);
    }

    [Fact]
    public void Conservative_HasSlowerLearning()
    {
        // Act
        var config = GradientLearnerConfig.Conservative;

        // Assert
        config.LearningRate.Should().Be(0.001);
        config.Momentum.Should().Be(0.95);
        config.AdaptiveLearningRate.Should().BeTrue();
        config.GradientClipThreshold.Should().Be(0.5);
        config.MinConfidenceThreshold.Should().Be(0.3);
        config.BatchAccumulationSize.Should().Be(10);
    }

    [Fact]
    public void Aggressive_HasFasterLearning()
    {
        // Act
        var config = GradientLearnerConfig.Aggressive;

        // Assert
        config.LearningRate.Should().Be(0.1);
        config.Momentum.Should().Be(0.5);
        config.AdaptiveLearningRate.Should().BeFalse();
        config.GradientClipThreshold.Should().Be(5.0);
        config.MinConfidenceThreshold.Should().Be(0.0);
        config.BatchAccumulationSize.Should().Be(1);
    }

    [Fact]
    public void Validate_WithValidConfig_ReturnsSuccess()
    {
        // Act
        var result = GradientLearnerConfig.Default.Validate();

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithZeroLearningRate_ReturnsFailure()
    {
        // Arrange
        var config = GradientLearnerConfig.Default with { LearningRate = 0 };

        // Act
        var result = config.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("LearningRate");
    }

    [Fact]
    public void Validate_WithLearningRateGreaterThanOne_ReturnsFailure()
    {
        // Arrange
        var config = GradientLearnerConfig.Default with { LearningRate = 1.5 };

        // Act
        var result = config.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithNegativeMomentum_ReturnsFailure()
    {
        // Arrange
        var config = GradientLearnerConfig.Default with { Momentum = -0.1 };

        // Act
        var result = config.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Momentum");
    }

    [Fact]
    public void Validate_WithMomentumEqualToOne_ReturnsFailure()
    {
        // Arrange
        var config = GradientLearnerConfig.Default with { Momentum = 1.0 };

        // Act
        var result = config.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithNegativeGradientClipThreshold_ReturnsFailure()
    {
        // Arrange
        var config = GradientLearnerConfig.Default with { GradientClipThreshold = -1.0 };

        // Act
        var result = config.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("GradientClipThreshold");
    }

    [Fact]
    public void Validate_WithNegativeMinConfidenceThreshold_ReturnsFailure()
    {
        // Arrange
        var config = GradientLearnerConfig.Default with { MinConfidenceThreshold = -0.1 };

        // Act
        var result = config.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("MinConfidenceThreshold");
    }

    [Fact]
    public void Validate_WithZeroBatchAccumulationSize_ReturnsFailure()
    {
        // Arrange
        var config = GradientLearnerConfig.Default with { BatchAccumulationSize = 0 };

        // Act
        var result = config.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("BatchAccumulationSize");
    }
}
