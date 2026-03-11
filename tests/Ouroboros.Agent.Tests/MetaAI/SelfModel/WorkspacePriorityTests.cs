using FluentAssertions;
using Ouroboros.Agent.MetaAI.SelfModel;
using Xunit;

namespace Ouroboros.Tests.MetaAI.SelfModel;

[Trait("Category", "Unit")]
public class WorkspacePriorityTests
{
    [Theory]
    [InlineData(WorkspacePriority.Low)]
    [InlineData(WorkspacePriority.Normal)]
    [InlineData(WorkspacePriority.High)]
    [InlineData(WorkspacePriority.Critical)]
    public void WorkspacePriority_AllValues_AreDefined(WorkspacePriority priority)
    {
        // Act & Assert
        Enum.IsDefined(priority).Should().BeTrue();
    }

    [Fact]
    public void WorkspacePriority_HasFourValues()
    {
        // Act
        var values = Enum.GetValues<WorkspacePriority>();

        // Assert
        values.Should().HaveCount(4);
    }

    [Fact]
    public void WorkspacePriority_Low_HasValue0()
    {
        // Act & Assert
        ((int)WorkspacePriority.Low).Should().Be(0);
    }

    [Fact]
    public void WorkspacePriority_Normal_HasValue1()
    {
        // Act & Assert
        ((int)WorkspacePriority.Normal).Should().Be(1);
    }

    [Fact]
    public void WorkspacePriority_High_HasValue2()
    {
        // Act & Assert
        ((int)WorkspacePriority.High).Should().Be(2);
    }

    [Fact]
    public void WorkspacePriority_Critical_HasValue3()
    {
        // Act & Assert
        ((int)WorkspacePriority.Critical).Should().Be(3);
    }
}
