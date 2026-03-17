using NSubstitute;
using Ouroboros.Core.Steps;
using Ouroboros.Pipeline.Learning;

namespace Ouroboros.Tests.Learning;

public class OnlineLearningExtensionsTests
{
    [Fact]
    public void ToExperience_ConvertsFeedbackToExperience()
    {
        // Arrange
        var feedback = Feedback.Explicit("source1", "input context", "output text", 0.7);

        // Act
        var experience = feedback.ToExperience("next context");

        // Assert
        experience.State.Should().Be("input context");
        experience.Action.Should().Be("output text");
        experience.Reward.Should().Be(0.7);
        experience.NextState.Should().Be("next context");
        experience.Priority.Should().Be(1.0); // Explicit -> 1.0
    }

    [Fact]
    public void ToExperience_CorrectiveFeedback_HasHigherPriority()
    {
        // Arrange
        var feedback = Feedback.Corrective("source", "input", "wrong", "right");

        // Act
        var experience = feedback.ToExperience("next");

        // Assert
        experience.Priority.Should().Be(1.5);
    }

    [Fact]
    public void ToExperience_ImplicitFeedback_HasLowerPriority()
    {
        // Arrange
        var feedback = Feedback.Implicit("source", "input", "output", 0.3);

        // Act
        var experience = feedback.ToExperience("next");

        // Assert
        experience.Priority.Should().Be(0.5);
    }

    [Fact]
    public void ToExperience_ComparativeFeedback_HasMediumPriority()
    {
        // Arrange
        var feedback = Feedback.Comparative("source", "input", "chosen", "rejected");

        // Act
        var experience = feedback.ToExperience("next");

        // Assert
        experience.Priority.Should().Be(0.8);
    }

    [Fact]
    public void ToExperience_IncludesMetadata()
    {
        // Arrange
        var feedback = Feedback.Explicit("source1", "input", "output", 0.5);

        // Act
        var experience = feedback.ToExperience("next");

        // Assert
        experience.Metadata.Should().ContainKey("feedbackId");
        experience.Metadata.Should().ContainKey("sourceId");
        experience.Metadata["sourceId"].Should().Be("source1");
        experience.Metadata.Should().ContainKey("feedbackType");
        experience.Metadata["feedbackType"].Should().Be("Explicit");
    }

    [Fact]
    public void ToFeedback_ConvertsExperienceToFeedback()
    {
        // Arrange
        var experience = Experience.Create("state", "action", 0.6, "nextState")
            .WithMetadata("sourceId", "src1")
            .WithMetadata("feedbackType", "Explicit");

        // Act
        var feedback = experience.ToFeedback();

        // Assert
        feedback.SourceId.Should().Be("src1");
        feedback.InputContext.Should().Be("state");
        feedback.Output.Should().Be("action");
        feedback.Score.Should().Be(0.6);
        feedback.Type.Should().Be(FeedbackType.Explicit);
    }

    [Fact]
    public void ToFeedback_WithNoMetadata_UsesDefaults()
    {
        // Arrange
        var experience = Experience.Create("state", "action", 0.5, "next");

        // Act
        var feedback = experience.ToFeedback();

        // Assert
        feedback.SourceId.Should().Be("unknown");
        feedback.Type.Should().Be(FeedbackType.Implicit); // Default
    }

    [Fact]
    public void ToFeedback_WithInvalidFeedbackType_DefaultsToImplicit()
    {
        // Arrange
        var experience = Experience.Create("state", "action", 0.5, "next")
            .WithMetadata("feedbackType", "InvalidType");

        // Act
        var feedback = experience.ToFeedback();

        // Assert
        feedback.Type.Should().Be(FeedbackType.Implicit);
    }

    [Fact]
    public void ToExperience_RoundTrip_PreservesData()
    {
        // Arrange
        var original = Feedback.Explicit("source", "input", "output", 0.7);

        // Act
        var experience = original.ToExperience("next");
        var roundTripped = experience.ToFeedback();

        // Assert
        roundTripped.SourceId.Should().Be("source");
        roundTripped.InputContext.Should().Be("input");
        roundTripped.Output.Should().Be("output");
        roundTripped.Score.Should().Be(0.7);
        roundTripped.Type.Should().Be(FeedbackType.Explicit);
    }

    [Fact]
    public async Task WithLearning_ExecutesStepAndRecordsFeedback()
    {
        // Arrange
        var learner = Substitute.For<IOnlineLearner>();
        learner.ProcessFeedback(Arg.Any<Feedback>())
            .Returns(Result<IReadOnlyList<LearningUpdate>, string>.Success(
                Array.Empty<LearningUpdate>()));

        Step<string, string> originalStep = input => Task.FromResult($"processed: {input}");
        var wrappedStep = originalStep.WithLearning(
            learner, "testSource", (input, output) => 0.8);

        // Act
        var result = await wrappedStep("hello");

        // Assert
        result.Should().Be("processed: hello");
        learner.Received(1).ProcessFeedback(Arg.Is<Feedback>(f =>
            f.SourceId == "testSource" && f.Score == 0.8));
    }
}
