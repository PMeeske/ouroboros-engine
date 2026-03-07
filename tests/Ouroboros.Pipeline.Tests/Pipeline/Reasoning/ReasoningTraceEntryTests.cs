namespace Ouroboros.Tests.Pipeline.Reasoning;

using Ouroboros.Pipeline.Reasoning;

[Trait("Category", "Unit")]
public class ReasoningTraceEntryTests
{
    [Fact]
    public void RequiredProperties_MustBeSet()
    {
        var entry = new ReasoningTraceEntry
        {
            Event = "thinking",
            StepName = "step1",
        };

        entry.Event.Should().Be("thinking");
        entry.StepName.Should().Be("step1");
        entry.Details.Should().BeNull();
        entry.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void OptionalProperties_CanBeSet()
    {
        var entry = new ReasoningTraceEntry
        {
            Event = "draft",
            StepName = "step2",
            Details = "Some details",
            Timestamp = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };

        entry.Details.Should().Be("Some details");
        entry.Timestamp.Year.Should().Be(2025);
    }
}
