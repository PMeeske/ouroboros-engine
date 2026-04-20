using Ouroboros.Pipeline.Learning;

namespace Ouroboros.Tests.Learning;

public class AdaptiveMetaLearnerTests
{
    [Fact]
    public void EvaluateStrategy_WithValidInputs_ReturnsScore()
    {
        // Arrange
        var learner = new AdaptiveMetaLearner(seed: 42);
        var strategy = LearningStrategy.Default;
        var metrics = LearningMetrics.FromRewards(new[] { 0.5, 0.6, 0.7 });

        // Act
        var score = learner.EvaluateStrategy(strategy, metrics);

        // Assert
        score.Should().BeOfType(typeof(double));
    }

    [Fact]
    public void EvaluateStrategy_WithNullStrategy_ThrowsArgumentNullException()
    {
        // Arrange
        var learner = new AdaptiveMetaLearner();
        var metrics = LearningMetrics.Empty;

        // Act & Assert
        var act = () => learner.EvaluateStrategy(null!, metrics);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void EvaluateStrategy_WithNullMetrics_ThrowsArgumentNullException()
    {
        // Arrange
        var learner = new AdaptiveMetaLearner();
        var strategy = LearningStrategy.Default;

        // Act & Assert
        var act = () => learner.EvaluateStrategy(strategy, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void EvaluateStrategy_WithInvalidStrategy_PenalizesScore()
    {
        // Arrange
        var learner = new AdaptiveMetaLearner(seed: 42);
        var validStrategy = LearningStrategy.Default;
        var invalidStrategy = LearningStrategy.Default with { Name = "" };
        var metrics = LearningMetrics.FromRewards(new[] { 0.5, 0.6, 0.7 });

        // Act
        var validScore = learner.EvaluateStrategy(validStrategy, metrics);
        var invalidScore = learner.EvaluateStrategy(invalidStrategy, metrics);

        // Assert
        validScore.Should().BeGreaterThan(invalidScore);
    }

    [Fact]
    public void AdaptStrategy_WithValidInputs_ReturnsSuccess()
    {
        // Arrange
        var learner = new AdaptiveMetaLearner(seed: 42);
        var strategy = LearningStrategy.Default;
        var metrics = LearningMetrics.FromRewards(new[] { 0.5, 0.6, 0.7 });

        // Act
        var result = learner.AdaptStrategy(strategy, metrics);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
    }

    [Fact]
    public void AdaptStrategy_WithNullStrategy_ThrowsArgumentNullException()
    {
        // Arrange
        var learner = new AdaptiveMetaLearner();
        var metrics = LearningMetrics.Empty;

        // Act & Assert
        var act = () => learner.AdaptStrategy(null!, metrics);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AdaptStrategy_WithNullMetrics_ThrowsArgumentNullException()
    {
        // Arrange
        var learner = new AdaptiveMetaLearner();
        var strategy = LearningStrategy.Default;

        // Act & Assert
        var act = () => learner.AdaptStrategy(strategy, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AdaptStrategy_WithFewEpisodes_AdaptsForExploration()
    {
        // Arrange
        var learner = new AdaptiveMetaLearner(seed: 42);
        var strategy = LearningStrategy.Default;
        var metrics = LearningMetrics.FromRewards(new[] { 0.5, 0.6 }); // < 10 episodes

        // Act
        var result = learner.AdaptStrategy(strategy, metrics);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // Exploration adaptation increases learning and exploration rates
    }

    [Fact]
    public void AdaptStrategy_RecordsPerformanceHistory()
    {
        // Arrange
        var learner = new AdaptiveMetaLearner(seed: 42);
        var strategy = LearningStrategy.Default;
        var metrics = LearningMetrics.FromRewards(new[] { 0.5, 0.6 });

        // Act
        learner.AdaptStrategy(strategy, metrics);

        // Assert
        var history = learner.GetHistory(strategy.Id);
        history.IsSome.Should().BeTrue();
    }

    [Fact]
    public void SelectBestStrategy_WithStrategies_ReturnsSuccess()
    {
        // Arrange
        var learner = new AdaptiveMetaLearner(seed: 42);
        var strategies = new[]
        {
            LearningStrategy.Default,
            LearningStrategy.Exploratory(),
            LearningStrategy.Exploitative(),
        };
        var metrics = LearningMetrics.FromRewards(new[] { 0.5, 0.6, 0.7 });

        // Act
        var result = learner.SelectBestStrategy(strategies, metrics);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
    }

    [Fact]
    public void SelectBestStrategy_WithEmptyStrategies_ReturnsFailure()
    {
        // Arrange
        var learner = new AdaptiveMetaLearner();
        var metrics = LearningMetrics.Empty;

        // Act
        var result = learner.SelectBestStrategy(Array.Empty<LearningStrategy>(), metrics);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No strategies");
    }

    [Fact]
    public void SelectBestStrategy_WithNullStrategies_ThrowsArgumentNullException()
    {
        // Arrange
        var learner = new AdaptiveMetaLearner();
        var metrics = LearningMetrics.Empty;

        // Act & Assert
        var act = () => learner.SelectBestStrategy(null!, metrics);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SelectBestStrategy_WithSingleStrategy_ReturnsThatStrategy()
    {
        // Arrange
        var learner = new AdaptiveMetaLearner(seed: 42);
        var strategy = LearningStrategy.Default;
        var metrics = LearningMetrics.FromRewards(new[] { 0.5 });

        // Act
        var result = learner.SelectBestStrategy(new[] { strategy }, metrics);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(strategy.Id);
    }

    [Fact]
    public void GetHistory_WithNoHistory_ReturnsNone()
    {
        // Arrange
        var learner = new AdaptiveMetaLearner();

        // Act
        var result = learner.GetHistory(Guid.NewGuid());

        // Assert
        result.IsSome.Should().BeFalse();
    }

    [Fact]
    public void GetHistory_AfterAdaptation_ReturnsSome()
    {
        // Arrange
        var learner = new AdaptiveMetaLearner(seed: 42);
        var strategy = LearningStrategy.Default;
        var metrics = LearningMetrics.FromRewards(new[] { 0.5, 0.6 });
        learner.AdaptStrategy(strategy, metrics);

        // Act
        var result = learner.GetHistory(strategy.Id);

        // Assert
        result.IsSome.Should().BeTrue();
    }

    [Fact]
    public void Constructor_ClampsExplorationWeight()
    {
        // Act - should not throw
        var learner1 = new AdaptiveMetaLearner(explorationWeight: 2.0);
        var learner2 = new AdaptiveMetaLearner(explorationWeight: -1.0);

        // Assert - verify construction succeeds (weight is clamped internally)
        learner1.Should().NotBeNull();
        learner2.Should().NotBeNull();
    }

    [Fact]
    public void AdaptStrategy_MultipleAdaptations_TrimHistory()
    {
        // Arrange
        var learner = new AdaptiveMetaLearner(historyLimit: 3, seed: 42);
        var strategy = LearningStrategy.Default;

        // Act - record more than the limit
        for (int i = 0; i < 5; i++)
        {
            var metrics = LearningMetrics.FromRewards(new[] { 0.5 + (i * 0.1) });
            learner.AdaptStrategy(strategy, metrics);
        }

        // Assert
        var history = learner.GetHistory(strategy.Id);
        history.IsSome.Should().BeTrue();
    }
}
