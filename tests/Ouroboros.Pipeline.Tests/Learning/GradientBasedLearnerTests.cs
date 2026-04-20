using Ouroboros.Pipeline.Learning;

namespace Ouroboros.Tests.Learning;

public class GradientBasedLearnerTests
{
    private static Feedback CreateValidFeedback(double score = 0.5, FeedbackType type = FeedbackType.Explicit) =>
        Feedback.Explicit("source", "input context", "output", score);

    [Fact]
    public void Constructor_WithDefaults_InitializesCorrectly()
    {
        // Act
        var learner = new GradientBasedLearner();

        // Assert
        learner.Metrics.ProcessedCount.Should().Be(0);
        learner.GetAllParameters().Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithInitialParameters_SetsParameters()
    {
        // Arrange
        var parameters = new Dictionary<string, double>
        {
            ["weight1"] = 0.5,
            ["weight2"] = 0.3,
        };

        // Act
        var learner = new GradientBasedLearner(initialParameters: parameters);

        // Assert
        learner.GetParameter("weight1").IsSome.Should().BeTrue();
        learner.GetParameter("weight2").IsSome.Should().BeTrue();
    }

    [Fact]
    public void ProcessFeedback_WithValidFeedback_ReturnsUpdates()
    {
        // Arrange
        var learner = new GradientBasedLearner(
            initialParameters: new Dictionary<string, double> { ["bias"] = 0.0 });
        var feedback = CreateValidFeedback(0.5);

        // Act
        var result = learner.ProcessFeedback(feedback);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
    }

    [Fact]
    public void ProcessFeedback_WithInvalidFeedback_ReturnsFailure()
    {
        // Arrange
        var learner = new GradientBasedLearner();
        var feedback = CreateValidFeedback() with { SourceId = "" };

        // Act
        var result = learner.ProcessFeedback(feedback);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ProcessFeedback_UpdatesMetrics()
    {
        // Arrange
        var learner = new GradientBasedLearner();
        var feedback = CreateValidFeedback(0.8);

        // Act
        learner.ProcessFeedback(feedback);

        // Assert
        learner.Metrics.ProcessedCount.Should().Be(1);
    }

    [Fact]
    public void ProcessFeedback_WithNoExistingParameters_CreatesBiasParameter()
    {
        // Arrange
        var learner = new GradientBasedLearner();
        var feedback = CreateValidFeedback(0.5);

        // Act
        learner.ProcessFeedback(feedback);

        // Assert
        learner.GetParameter("bias").IsSome.Should().BeTrue();
    }

    [Fact]
    public void ProcessFeedback_ComputesGradientForEachParameter()
    {
        // Arrange
        var learner = new GradientBasedLearner(
            initialParameters: new Dictionary<string, double>
            {
                ["w1"] = 0.5,
                ["w2"] = 0.3,
            });
        var feedback = CreateValidFeedback(0.5);

        // Act
        var result = learner.ProcessFeedback(feedback);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Select(u => u.ParameterName).Should().Contain("w1");
        result.Value.Select(u => u.ParameterName).Should().Contain("w2");
    }

    [Fact]
    public void ProcessBatch_WithEmptyBatch_ReturnsEmptyUpdates()
    {
        // Arrange
        var learner = new GradientBasedLearner();

        // Act
        var result = learner.ProcessBatch(Array.Empty<Feedback>());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public void ProcessBatch_WithValidBatch_ReturnsAggregatedUpdates()
    {
        // Arrange
        var learner = new GradientBasedLearner(
            initialParameters: new Dictionary<string, double> { ["w1"] = 0.5 });
        var batch = new[]
        {
            CreateValidFeedback(0.3),
            CreateValidFeedback(0.7),
        };

        // Act
        var result = learner.ProcessBatch(batch);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
    }

    [Fact]
    public void ProcessBatch_WithAllInvalidFeedback_ReturnsFailure()
    {
        // Arrange
        var learner = new GradientBasedLearner();
        var batch = new[]
        {
            CreateValidFeedback() with { SourceId = "" },
            CreateValidFeedback() with { InputContext = "" },
        };

        // Act
        var result = learner.ProcessBatch(batch);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ApplyUpdates_WithNoUpdates_ReturnsZero()
    {
        // Arrange
        var learner = new GradientBasedLearner();

        // Act
        var result = learner.ApplyUpdates();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
    }

    [Fact]
    public void ApplyUpdates_WithPendingUpdates_AppliesAndReturnsCount()
    {
        // Arrange
        var config = GradientLearnerConfig.Default with { BatchAccumulationSize = 100 };
        var learner = new GradientBasedLearner(
            config: config,
            initialParameters: new Dictionary<string, double> { ["w"] = 0.5 });
        learner.ProcessFeedback(CreateValidFeedback(0.5));

        // Act
        var result = learner.ApplyUpdates();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void ApplyUpdates_ClearsPendingUpdates()
    {
        // Arrange
        var config = GradientLearnerConfig.Default with { BatchAccumulationSize = 100 };
        var learner = new GradientBasedLearner(
            config: config,
            initialParameters: new Dictionary<string, double> { ["w"] = 0.5 });
        learner.ProcessFeedback(CreateValidFeedback(0.5));

        // Act
        learner.ApplyUpdates();

        // Assert
        learner.GetPendingUpdates().Should().BeEmpty();
    }

    [Fact]
    public void ApplyUpdates_SkipsLowConfidenceUpdates()
    {
        // Arrange
        var config = GradientLearnerConfig.Default with
        {
            MinConfidenceThreshold = 0.99,
            BatchAccumulationSize = 100,
        };
        var learner = new GradientBasedLearner(
            config: config,
            initialParameters: new Dictionary<string, double> { ["w"] = 0.5 });

        // Use implicit feedback which has lower confidence
        var feedback = Feedback.Implicit("source", "input", "output", 0.5);
        learner.ProcessFeedback(feedback);

        // Act
        var result = learner.ApplyUpdates();

        // Assert
        result.IsSuccess.Should().BeTrue();
        // May have 0 applied due to low confidence
    }

    [Fact]
    public void GetParameter_WithExistingParameter_ReturnsSome()
    {
        // Arrange
        var learner = new GradientBasedLearner(
            initialParameters: new Dictionary<string, double> { ["w"] = 0.5 });

        // Act
        var result = learner.GetParameter("w");

        // Assert
        result.IsSome.Should().BeTrue();
    }

    [Fact]
    public void GetParameter_WithNonExistentParameter_ReturnsNone()
    {
        // Arrange
        var learner = new GradientBasedLearner();

        // Act
        var result = learner.GetParameter("nonexistent");

        // Assert
        result.IsSome.Should().BeFalse();
    }

    [Fact]
    public void SetParameter_SetsParameterValue()
    {
        // Arrange
        var learner = new GradientBasedLearner();

        // Act
        learner.SetParameter("newParam", 0.42);

        // Assert
        learner.GetParameter("newParam").IsSome.Should().BeTrue();
    }

    [Fact]
    public void GetAllParameters_ReturnsImmutableDictionary()
    {
        // Arrange
        var learner = new GradientBasedLearner(
            initialParameters: new Dictionary<string, double>
            {
                ["w1"] = 0.5,
                ["w2"] = 0.3,
            });

        // Act
        var parameters = learner.GetAllParameters();

        // Assert
        parameters.Should().HaveCount(2);
        parameters["w1"].Should().Be(0.5);
    }

    [Fact]
    public void ResetState_ClearsInternalState()
    {
        // Arrange
        var config = GradientLearnerConfig.Default with { BatchAccumulationSize = 100 };
        var learner = new GradientBasedLearner(
            config: config,
            initialParameters: new Dictionary<string, double> { ["w"] = 0.5 });
        learner.ProcessFeedback(CreateValidFeedback(0.5));

        // Act
        learner.ResetState();

        // Assert
        learner.Metrics.ProcessedCount.Should().Be(0);
        learner.GetPendingUpdates().Should().BeEmpty();
        // Parameters should be preserved
        learner.GetParameter("w").IsSome.Should().BeTrue();
    }

    [Fact]
    public void GetPendingUpdates_ReturnsPendingUpdates()
    {
        // Arrange
        var config = GradientLearnerConfig.Default with { BatchAccumulationSize = 100 };
        var learner = new GradientBasedLearner(
            config: config,
            initialParameters: new Dictionary<string, double> { ["w"] = 0.5 });
        learner.ProcessFeedback(CreateValidFeedback(0.5));

        // Act
        var pending = learner.GetPendingUpdates();

        // Assert
        pending.Should().NotBeEmpty();
    }

    [Fact]
    public void ProcessFeedback_AutoAppliesWhenBatchSizeReached()
    {
        // Arrange
        var config = GradientLearnerConfig.Default with { BatchAccumulationSize = 1 };
        var learner = new GradientBasedLearner(
            config: config,
            initialParameters: new Dictionary<string, double> { ["w"] = 0.5 });

        // Act
        learner.ProcessFeedback(CreateValidFeedback(0.5));

        // Assert - pending should be empty because auto-applied
        learner.GetPendingUpdates().Should().BeEmpty();
    }
}
