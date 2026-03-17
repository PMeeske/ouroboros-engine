using NSubstitute;
using Ouroboros.Pipeline.Learning;

namespace Ouroboros.Tests.Learning;

public class MetaLearningArrowTests
{
    private readonly IMetaLearner _metaLearner = Substitute.For<IMetaLearner>();

    [Fact]
    public void EvaluateArrow_WithNullMetaLearner_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => MetaLearningArrow.EvaluateArrow(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task EvaluateArrow_DelegatesToMetaLearner()
    {
        // Arrange
        var strategy = LearningStrategy.Default;
        var metrics = LearningMetrics.Empty;
        _metaLearner.EvaluateStrategy(strategy, metrics).Returns(0.85);
        var step = MetaLearningArrow.EvaluateArrow(_metaLearner);

        // Act
        var result = await step((strategy, metrics));

        // Assert
        result.Should().Be(0.85);
        _metaLearner.Received(1).EvaluateStrategy(strategy, metrics);
    }

    [Fact]
    public void AdaptArrow_WithNullMetaLearner_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => MetaLearningArrow.AdaptArrow(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task AdaptArrow_DelegatesToMetaLearner()
    {
        // Arrange
        var strategy = LearningStrategy.Default;
        var metrics = LearningMetrics.Empty;
        var adapted = LearningStrategy.Exploratory();
        _metaLearner.AdaptStrategy(strategy, metrics)
            .Returns(Result<LearningStrategy, string>.Success(adapted));
        var step = MetaLearningArrow.AdaptArrow(_metaLearner);

        // Act
        var result = await step((strategy, metrics));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Exploratory");
    }

    [Fact]
    public void SelectArrow_WithNullMetaLearner_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => MetaLearningArrow.SelectArrow(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task SelectArrow_DelegatesToMetaLearner()
    {
        // Arrange
        var strategies = new[] { LearningStrategy.Default, LearningStrategy.Exploratory() };
        var metrics = LearningMetrics.Empty;
        var selected = strategies[0];
        _metaLearner.SelectBestStrategy(strategies, metrics)
            .Returns(Result<LearningStrategy, string>.Success(selected));
        var step = MetaLearningArrow.SelectArrow(_metaLearner);

        // Act
        var result = await step((strategies, metrics));

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void AdaptAndValidateArrow_WithNullMetaLearner_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => MetaLearningArrow.AdaptAndValidateArrow(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task AdaptAndValidateArrow_WhenAdaptSucceedsAndValid_ReturnsSuccess()
    {
        // Arrange
        var strategy = LearningStrategy.Default;
        var metrics = LearningMetrics.Empty;
        var adapted = LearningStrategy.Default; // Valid strategy
        _metaLearner.AdaptStrategy(strategy, metrics)
            .Returns(Result<LearningStrategy, string>.Success(adapted));
        var step = MetaLearningArrow.AdaptAndValidateArrow(_metaLearner);

        // Act
        var result = await step((strategy, metrics));

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task AdaptAndValidateArrow_WhenAdaptFails_ReturnsFailure()
    {
        // Arrange
        var strategy = LearningStrategy.Default;
        var metrics = LearningMetrics.Empty;
        _metaLearner.AdaptStrategy(strategy, metrics)
            .Returns(Result<LearningStrategy, string>.Failure("Adapt failed"));
        var step = MetaLearningArrow.AdaptAndValidateArrow(_metaLearner);

        // Act
        var result = await step((strategy, metrics));

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task AdaptAndValidateArrow_WhenAdaptedStrategyInvalid_ReturnsFailure()
    {
        // Arrange
        var strategy = LearningStrategy.Default;
        var metrics = LearningMetrics.Empty;
        var invalid = LearningStrategy.Default with { Name = "" }; // Invalid
        _metaLearner.AdaptStrategy(strategy, metrics)
            .Returns(Result<LearningStrategy, string>.Success(invalid));
        var step = MetaLearningArrow.AdaptAndValidateArrow(_metaLearner);

        // Act
        var result = await step((strategy, metrics));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("validation");
    }

    [Fact]
    public async Task UpdateMetricsArrow_UpdatesMetricsWithReward()
    {
        // Arrange
        var metrics = LearningMetrics.Empty;
        var step = MetaLearningArrow.UpdateMetricsArrow();

        // Act
        var result = await step((metrics, 0.8));

        // Assert
        result.TotalEpisodes.Should().Be(1);
        result.AverageReward.Should().Be(0.8);
    }

    [Fact]
    public async Task PerformanceScoreArrow_ComputesScore()
    {
        // Arrange
        var metrics = LearningMetrics.FromRewards(new[] { 0.5, 0.6, 0.7 });
        var step = MetaLearningArrow.PerformanceScoreArrow();

        // Act
        var result = await step(metrics);

        // Assert
        result.Should().BeOfType(typeof(double));
    }

    [Fact]
    public void IterativeAdaptArrow_WithNullMetaLearner_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => MetaLearningArrow.IterativeAdaptArrow(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task IterativeAdaptArrow_PerformsIterativeAdaptation()
    {
        // Arrange
        var strategy = LearningStrategy.Default;
        var rewards = new[] { 0.3, 0.5, 0.7 };

        _metaLearner.AdaptStrategy(Arg.Any<LearningStrategy>(), Arg.Any<LearningMetrics>())
            .Returns(call => Result<LearningStrategy, string>.Success(
                ((LearningStrategy)call[0]).WithLearningRate(0.002)));
        _metaLearner.EvaluateStrategy(Arg.Any<LearningStrategy>(), Arg.Any<LearningMetrics>())
            .Returns(0.5);

        var step = MetaLearningArrow.IterativeAdaptArrow(_metaLearner, maxIterations: 3);

        // Act
        var result = await step((strategy, rewards));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Final.Should().NotBeNull();
        result.Value.Metrics.Should().NotBeNull();
    }

    [Fact]
    public async Task IterativeAdaptArrow_WhenAdaptFails_ReturnsFailure()
    {
        // Arrange
        _metaLearner.AdaptStrategy(Arg.Any<LearningStrategy>(), Arg.Any<LearningMetrics>())
            .Returns(Result<LearningStrategy, string>.Failure("Failed"));
        var step = MetaLearningArrow.IterativeAdaptArrow(_metaLearner);

        // Act
        var result = await step((LearningStrategy.Default, new[] { 0.5 }));

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task IterativeAdaptArrow_ConvergesEarly()
    {
        // Arrange
        var callCount = 0;
        _metaLearner.AdaptStrategy(Arg.Any<LearningStrategy>(), Arg.Any<LearningMetrics>())
            .Returns(call =>
            {
                callCount++;
                return Result<LearningStrategy, string>.Success((LearningStrategy)call[0]);
            });
        _metaLearner.EvaluateStrategy(Arg.Any<LearningStrategy>(), Arg.Any<LearningMetrics>())
            .Returns(0.5); // Same score = convergence

        var step = MetaLearningArrow.IterativeAdaptArrow(_metaLearner, maxIterations: 10, convergenceThreshold: 0.01);

        // Act
        var result = await step((LearningStrategy.Default, new[] { 0.5, 0.6 }));

        // Assert
        result.IsSuccess.Should().BeTrue();
        // Should have converged early (score doesn't change)
        callCount.Should().BeLessThan(10);
    }
}
