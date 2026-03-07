namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class StepExecutionPayloadTests
{
    [Fact]
    public void Ctor_SetsAllProperties()
    {
        var ts = DateTime.UtcNow;
        var payload = new StepExecutionPayload(
            "UseImprove",
            new[] { "Improve", "Refine" },
            "ImproveStep",
            "Improves the draft",
            "arg1=val",
            "Executed Improve on Draft",
            150L,
            true,
            null,
            ts);

        payload.TokenName.Should().Be("UseImprove");
        payload.Aliases.Should().HaveCount(2);
        payload.SourceClass.Should().Be("ImproveStep");
        payload.Description.Should().Be("Improves the draft");
        payload.Arguments.Should().Be("arg1=val");
        payload.Synopsis.Should().Contain("Improve");
        payload.DurationMs.Should().Be(150);
        payload.Success.Should().BeTrue();
        payload.Error.Should().BeNull();
        payload.ExecutedAt.Should().Be(ts);
    }

    [Fact]
    public void Ctor_WithError()
    {
        var payload = new StepExecutionPayload(
            "Token", Array.Empty<string>(), "Class", "Desc",
            null, "Synopsis", null, false, "Something went wrong", DateTime.UtcNow);

        payload.Success.Should().BeFalse();
        payload.Error.Should().Be("Something went wrong");
    }
}
