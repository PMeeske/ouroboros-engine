using FluentAssertions;
using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class TemporalPlanTests
{
    [Fact]
    public void Constructor_ShouldSetAllProperties()
    {
        // Arrange
        var start = DateTime.UtcNow;
        var tasks = new List<ScheduledTask>
        {
            new("Build", start, start.AddHours(1), new List<string>()),
            new("Deploy", start.AddHours(1), start.AddHours(2), new List<string> { "Build" })
        };
        var totalDuration = TimeSpan.FromHours(2);
        var createdAt = DateTime.UtcNow;

        // Act
        var sut = new TemporalPlan("Ship v2.0", tasks, totalDuration, createdAt);

        // Assert
        sut.Goal.Should().Be("Ship v2.0");
        sut.Tasks.Should().HaveCount(2);
        sut.TotalDuration.Should().Be(totalDuration);
        sut.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void Constructor_WithDefaultCreatedAt_ShouldUseDefault()
    {
        // Arrange & Act
        var sut = new TemporalPlan(
            "Goal",
            new List<ScheduledTask>(),
            TimeSpan.FromMinutes(30));

        // Assert
        sut.CreatedAt.Should().Be(default(DateTime));
    }

    [Fact]
    public void Constructor_WithEmptyTasks_ShouldWork()
    {
        // Arrange & Act
        var sut = new TemporalPlan("EmptyGoal", new List<ScheduledTask>(), TimeSpan.Zero);

        // Assert
        sut.Tasks.Should().BeEmpty();
        sut.TotalDuration.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void RecordEquality_SameReferences_ShouldBeEqual()
    {
        // Arrange
        var tasks = new List<ScheduledTask>();
        var duration = TimeSpan.FromHours(1);
        var a = new TemporalPlan("G", tasks, duration);
        var b = new TemporalPlan("G", tasks, duration);

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void WithExpression_ShouldCreateModifiedCopy()
    {
        // Arrange
        var original = new TemporalPlan("Goal1", new List<ScheduledTask>(), TimeSpan.FromHours(1));

        // Act
        var modified = original with { Goal = "Goal2" };

        // Assert
        modified.Goal.Should().Be("Goal2");
        modified.TotalDuration.Should().Be(original.TotalDuration);
    }
}
