using Ouroboros.Pipeline.Learning;

namespace Ouroboros.Tests.Learning;

public class FeedbackTests
{
    [Fact]
    public void Explicit_CreatesExplicitFeedback()
    {
        // Act
        var fb = Feedback.Explicit("source1", "input", "output", 0.8, "tag1", "tag2");

        // Assert
        fb.Id.Should().NotBeEmpty();
        fb.SourceId.Should().Be("source1");
        fb.InputContext.Should().Be("input");
        fb.Output.Should().Be("output");
        fb.Score.Should().Be(0.8);
        fb.Type.Should().Be(FeedbackType.Explicit);
        fb.Tags.Should().Contain("tag1");
        fb.Tags.Should().Contain("tag2");
        fb.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Explicit_ClampsScoreToRange()
    {
        // Act
        var fbHigh = Feedback.Explicit("s", "i", "o", 5.0);
        var fbLow = Feedback.Explicit("s", "i", "o", -5.0);

        // Assert
        fbHigh.Score.Should().Be(1.0);
        fbLow.Score.Should().Be(-1.0);
    }

    [Fact]
    public void Implicit_CreatesImplicitFeedback()
    {
        // Act
        var fb = Feedback.Implicit("source1", "input", "output", 0.3);

        // Assert
        fb.Type.Should().Be(FeedbackType.Implicit);
        fb.Score.Should().Be(0.3);
    }

    [Fact]
    public void Corrective_CreatesCorrectiveFeedback()
    {
        // Act
        var fb = Feedback.Corrective("source1", "input", "wrong output", "right output", "tag1");

        // Assert
        fb.Type.Should().Be(FeedbackType.Corrective);
        fb.Score.Should().Be(-0.5);
        fb.Output.Should().Be("wrong output");
        fb.Tags.Should().Contain(t => t.StartsWith("preferred:"));
    }

    [Fact]
    public void Comparative_CreatesComparativeFeedback()
    {
        // Act
        var fb = Feedback.Comparative("source1", "input", "chosen", "rejected", 0.7);

        // Assert
        fb.Type.Should().Be(FeedbackType.Comparative);
        fb.Score.Should().Be(0.7);
        fb.Output.Should().Be("chosen");
        fb.Tags.Should().Contain(t => t.StartsWith("rejected:"));
    }

    [Fact]
    public void Comparative_ClampsPreferenceStrength()
    {
        // Act
        var fb = Feedback.Comparative("s", "i", "chosen", "rejected", 5.0);

        // Assert
        fb.Score.Should().Be(1.0);
    }

    [Fact]
    public void WithTags_AddsAdditionalTags()
    {
        // Arrange
        var fb = Feedback.Explicit("s", "i", "o", 0.5, "existing");

        // Act
        var updated = fb.WithTags("new1", "new2");

        // Assert
        updated.Tags.Should().Contain("existing");
        updated.Tags.Should().Contain("new1");
        updated.Tags.Should().Contain("new2");
    }

    [Fact]
    public void Validate_WithValidFeedback_ReturnsSuccess()
    {
        // Arrange
        var fb = Feedback.Explicit("source", "context", "output", 0.5);

        // Act
        var result = fb.Validate();

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptySourceId_ReturnsFailure()
    {
        // Arrange
        var fb = Feedback.Explicit("source", "context", "output", 0.5) with { SourceId = "" };

        // Act
        var result = fb.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("SourceId");
    }

    [Fact]
    public void Validate_WithEmptyInputContext_ReturnsFailure()
    {
        // Arrange
        var fb = Feedback.Explicit("source", "context", "output", 0.5) with { InputContext = "" };

        // Act
        var result = fb.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("InputContext");
    }

    [Fact]
    public void Validate_WithOutOfRangeScore_ReturnsFailure()
    {
        // Arrange
        var fb = Feedback.Explicit("source", "context", "output", 0.5) with { Score = 1.5 };

        // Act
        var result = fb.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Score");
    }

    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var tags = ImmutableList.Create("t1", "t2");
        var timestamp = DateTime.UtcNow;

        // Act
        var fb = new Feedback(id, "src", "ctx", "out", 0.5, FeedbackType.Explicit, timestamp, tags);

        // Assert
        fb.Id.Should().Be(id);
        fb.SourceId.Should().Be("src");
        fb.InputContext.Should().Be("ctx");
        fb.Output.Should().Be("out");
        fb.Score.Should().Be(0.5);
        fb.Type.Should().Be(FeedbackType.Explicit);
        fb.Timestamp.Should().Be(timestamp);
        fb.Tags.Should().HaveCount(2);
    }
}
