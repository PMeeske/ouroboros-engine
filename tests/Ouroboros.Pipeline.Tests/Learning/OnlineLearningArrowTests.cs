using NSubstitute;
using Ouroboros.Pipeline.Learning;
using Unit = Ouroboros.Abstractions.Unit;

namespace Ouroboros.Tests.Learning;

public class OnlineLearningArrowTests
{
    private readonly IOnlineLearner _learner = Substitute.For<IOnlineLearner>();

    [Fact]
    public async Task ProcessFeedbackStep_DelegatesToLearner()
    {
        // Arrange
        var feedback = Feedback.Explicit("source", "input", "output", 0.5);
        var updates = new List<LearningUpdate>
        {
            LearningUpdate.FromGradient("p", 1.0, 0.5, 0.01),
        };
        _learner.ProcessFeedback(feedback)
            .Returns(Result<IReadOnlyList<LearningUpdate>, string>.Success(updates));
        var step = OnlineLearningArrow.ProcessFeedbackStep(_learner);

        // Act
        var result = await step(feedback);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
    }

    [Fact]
    public async Task ProcessBatchStep_DelegatesToLearner()
    {
        // Arrange
        var batch = new[]
        {
            Feedback.Explicit("s", "i", "o", 0.5),
            Feedback.Explicit("s", "i2", "o2", 0.7),
        };
        var updates = new List<LearningUpdate>();
        _learner.ProcessBatch(batch)
            .Returns(Result<IReadOnlyList<LearningUpdate>, string>.Success(updates));
        var step = OnlineLearningArrow.ProcessBatchStep(_learner);

        // Act
        var result = await step(batch);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _learner.Received(1).ProcessBatch(batch);
    }

    [Fact]
    public async Task ApplyUpdatesStep_DelegatesToLearner()
    {
        // Arrange
        _learner.ApplyUpdates().Returns(Result<int, string>.Success(3));
        var step = OnlineLearningArrow.ApplyUpdatesStep(_learner);

        // Act
        var result = await step(Unit.Value);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(3);
    }

    [Fact]
    public async Task GetMetricsStep_ReturnsCurrentMetrics()
    {
        // Arrange
        var metrics = OnlineLearningMetrics.Empty.WithNewScore(0.5);
        _learner.Metrics.Returns(metrics);
        var step = OnlineLearningArrow.GetMetricsStep(_learner);

        // Act
        var result = await step(Unit.Value);

        // Assert
        result.ProcessedCount.Should().Be(1);
    }

    [Fact]
    public async Task CreateExplicitFeedbackStep_CreatesFeedback()
    {
        // Arrange
        var step = OnlineLearningArrow.CreateExplicitFeedbackStep("mySource");

        // Act
        var result = await step(("input text", "output text", 0.8));

        // Assert
        result.SourceId.Should().Be("mySource");
        result.InputContext.Should().Be("input text");
        result.Output.Should().Be("output text");
        result.Score.Should().Be(0.8);
        result.Type.Should().Be(FeedbackType.Explicit);
    }

    [Fact]
    public async Task CreateCorrectiveFeedbackStep_CreatesFeedback()
    {
        // Arrange
        var step = OnlineLearningArrow.CreateCorrectiveFeedbackStep("mySource");

        // Act
        var result = await step(("input", "wrong output", "right output"));

        // Assert
        result.SourceId.Should().Be("mySource");
        result.Type.Should().Be(FeedbackType.Corrective);
        result.Output.Should().Be("wrong output");
        result.Tags.Should().Contain(t => t.Contains("right output"));
    }

    [Fact]
    public async Task FullLearningPipeline_ProcessesAndApplies()
    {
        // Arrange
        var updates = new List<LearningUpdate>
        {
            LearningUpdate.FromGradient("p", 1.0, 0.5, 0.01),
        };
        _learner.ProcessFeedback(Arg.Any<Feedback>())
            .Returns(Result<IReadOnlyList<LearningUpdate>, string>.Success(updates));
        _learner.ApplyUpdates().Returns(Result<int, string>.Success(1));
        var step = OnlineLearningArrow.FullLearningPipeline(_learner, "source");

        // Act
        var result = await step(("input", "output", 0.5));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1);
    }

    [Fact]
    public async Task FullLearningPipeline_WhenProcessFails_ReturnsFailure()
    {
        // Arrange
        _learner.ProcessFeedback(Arg.Any<Feedback>())
            .Returns(Result<IReadOnlyList<LearningUpdate>, string>.Failure("Error"));
        var step = OnlineLearningArrow.FullLearningPipeline(_learner, "source");

        // Act
        var result = await step(("input", "output", 0.5));

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task FilterFeedbackStep_WhenPredicatePasses_ReturnsSome()
    {
        // Arrange
        var feedback = Feedback.Explicit("s", "i", "o", 0.8);
        var step = OnlineLearningArrow.FilterFeedbackStep(f => f.Score > 0.5);

        // Act
        var result = await step(feedback);

        // Assert
        result.IsSome.Should().BeTrue();
    }

    [Fact]
    public async Task FilterFeedbackStep_WhenPredicateFails_ReturnsNone()
    {
        // Arrange
        var feedback = Feedback.Explicit("s", "i", "o", 0.3);
        var step = OnlineLearningArrow.FilterFeedbackStep(f => f.Score > 0.5);

        // Act
        var result = await step(feedback);

        // Assert
        result.IsSome.Should().BeFalse();
    }

    [Fact]
    public async Task EnrichFeedbackStep_AddsTags()
    {
        // Arrange
        var feedback = Feedback.Explicit("s", "i", "o", 0.5);
        var step = OnlineLearningArrow.EnrichFeedbackStep(f => new[] { "auto-tag", $"score:{f.Score}" });

        // Act
        var result = await step(feedback);

        // Assert
        result.Tags.Should().Contain("auto-tag");
        result.Tags.Should().Contain("score:0.5");
    }
}
