namespace Ouroboros.Tests.Pipeline.Learning;

using Ouroboros.Pipeline.Learning;

[Trait("Category", "Unit")]
public class FeedbackTests
{
    [Fact]
    public void Explicit_CreatesFeedbackWithExplicitType()
    {
        var fb = Feedback.Explicit("source1", "input context", "output text", 0.8);

        fb.Type.Should().Be(FeedbackType.Explicit);
        fb.Score.Should().Be(0.8);
        fb.SourceId.Should().Be("source1");
        fb.InputContext.Should().Be("input context");
        fb.Output.Should().Be("output text");
        fb.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Explicit_ClampsScoreToRange()
    {
        var fb = Feedback.Explicit("src", "ctx", "out", 2.0);
        fb.Score.Should().Be(1.0);

        var fb2 = Feedback.Explicit("src", "ctx", "out", -5.0);
        fb2.Score.Should().Be(-1.0);
    }

    [Fact]
    public void Implicit_CreatesFeedbackWithImplicitType()
    {
        var fb = Feedback.Implicit("source1", "input", "output", 0.5);

        fb.Type.Should().Be(FeedbackType.Implicit);
        fb.Score.Should().Be(0.5);
    }

    [Fact]
    public void Corrective_CreatesFeedbackWithCorrectiveType()
    {
        var fb = Feedback.Corrective("source1", "input", "actual", "preferred");

        fb.Type.Should().Be(FeedbackType.Corrective);
        fb.Score.Should().Be(-0.5); // Corrective always has -0.5 score
        fb.Tags.Should().Contain(t => t.Contains("preferred:"));
    }

    [Fact]
    public void Comparative_CreatesFeedbackWithComparativeType()
    {
        var fb = Feedback.Comparative("source1", "input", "chosen", "rejected", 0.7);

        fb.Type.Should().Be(FeedbackType.Comparative);
        fb.Score.Should().Be(0.7);
        fb.Tags.Should().Contain(t => t.Contains("rejected:"));
    }

    [Fact]
    public void Comparative_ClampsPreferenceStrength()
    {
        var fb = Feedback.Comparative("source1", "input", "chosen", "rejected", 2.0);
        fb.Score.Should().Be(1.0);
    }

    [Fact]
    public void WithTags_AddsTagsToFeedback()
    {
        var fb = Feedback.Explicit("src", "ctx", "out", 0.9).WithTags("tag1", "tag2");

        fb.Tags.Should().Contain("tag1");
        fb.Tags.Should().Contain("tag2");
    }

    [Fact]
    public void Validate_ReturnsSuccess_ForValidFeedback()
    {
        var fb = Feedback.Explicit("source1", "input context", "output", 0.5);
        var result = fb.Validate();

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Validate_ReturnsFailure_ForEmptySourceId()
    {
        var fb = new Feedback(
            Guid.NewGuid(),
            "",
            "ctx",
            "out",
            0.5,
            FeedbackType.Explicit,
            DateTime.UtcNow,
            ImmutableList<string>.Empty);

        var result = fb.Validate();
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Validate_ReturnsFailure_ForEmptyInputContext()
    {
        var fb = new Feedback(
            Guid.NewGuid(),
            "source",
            "",
            "out",
            0.5,
            FeedbackType.Explicit,
            DateTime.UtcNow,
            ImmutableList<string>.Empty);

        var result = fb.Validate();
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Explicit_AcceptsTags()
    {
        var fb = Feedback.Explicit("src", "ctx", "out", 0.5, "t1", "t2");

        fb.Tags.Should().Contain("t1");
        fb.Tags.Should().Contain("t2");
    }
}
