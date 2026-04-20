using Ouroboros.Pipeline.Reasoning;

namespace Ouroboros.Tests.Pipeline.Reasoning;

[Trait("Category", "Unit")]
public sealed class ReasoningTraceEntryTests
{
    [Fact]
    public void Constructor_WithRequiredProperties_SetsValues()
    {
        // Arrange & Act
        var entry = new ReasoningTraceEntry
        {
            Event = "Entry",
            StepName = "test-step",
            Details = "some details"
        };

        // Assert
        entry.Event.Should().Be("Entry");
        entry.StepName.Should().Be("test-step");
        entry.Details.Should().Be("some details");
    }

    [Fact]
    public void Timestamp_DefaultsToUtcNow()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var entry = new ReasoningTraceEntry
        {
            Event = "Entry",
            StepName = "test-step"
        };

        var after = DateTime.UtcNow;

        // Assert
        entry.Timestamp.Should().BeOnOrAfter(before);
        entry.Timestamp.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void Details_DefaultsToNull()
    {
        // Arrange & Act
        var entry = new ReasoningTraceEntry
        {
            Event = "Success",
            StepName = "step-1"
        };

        // Assert
        entry.Details.Should().BeNull();
    }

    [Fact]
    public void Timestamp_CanBeOverridden()
    {
        // Arrange
        var specificTime = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var entry = new ReasoningTraceEntry
        {
            Event = "Failure",
            StepName = "step-2",
            Timestamp = specificTime
        };

        // Assert
        entry.Timestamp.Should().Be(specificTime);
    }

    [Fact]
    public void Properties_AreMutable()
    {
        // Arrange
        var entry = new ReasoningTraceEntry
        {
            Event = "Entry",
            StepName = "original"
        };

        // Act
        entry.Event = "Failure";
        entry.StepName = "updated";
        entry.Details = "new details";

        // Assert
        entry.Event.Should().Be("Failure");
        entry.StepName.Should().Be("updated");
        entry.Details.Should().Be("new details");
    }
}
