using FluentAssertions;
using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class TaskAssignmentStatusTests
{
    [Fact]
    public void Pending_HasValue_Zero()
    {
        // Arrange & Act
        var value = (int)TaskAssignmentStatus.Pending;

        // Assert
        value.Should().Be(0);
    }

    [Fact]
    public void InProgress_HasValue_One()
    {
        // Arrange & Act
        var value = (int)TaskAssignmentStatus.InProgress;

        // Assert
        value.Should().Be(1);
    }

    [Fact]
    public void Completed_HasValue_Two()
    {
        // Arrange & Act
        var value = (int)TaskAssignmentStatus.Completed;

        // Assert
        value.Should().Be(2);
    }

    [Fact]
    public void Failed_HasValue_Three()
    {
        // Arrange & Act
        var value = (int)TaskAssignmentStatus.Failed;

        // Assert
        value.Should().Be(3);
    }

    [Fact]
    public void Enum_HasExactlyFourValues()
    {
        // Arrange & Act
        var values = Enum.GetValues<TaskAssignmentStatus>();

        // Assert
        values.Should().HaveCount(4);
    }

    [Fact]
    public void Enum_ContainsAllExpectedValues()
    {
        // Arrange & Act
        var values = Enum.GetValues<TaskAssignmentStatus>();

        // Assert
        values.Should().Contain(TaskAssignmentStatus.Pending);
        values.Should().Contain(TaskAssignmentStatus.InProgress);
        values.Should().Contain(TaskAssignmentStatus.Completed);
        values.Should().Contain(TaskAssignmentStatus.Failed);
    }
}
