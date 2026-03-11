using FluentAssertions;
using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class ExecutedStepTests
{
    [Fact]
    public void Constructor_ShouldSetAllProperties()
    {
        // Arrange
        var duration = TimeSpan.FromMilliseconds(250);
        var outputs = new Dictionary<string, object>
        {
            { "result", "success" },
            { "count", 42 }
        };

        // Act
        var sut = new ExecutedStep("ParseInput", true, duration, outputs);

        // Assert
        sut.StepName.Should().Be("ParseInput");
        sut.Success.Should().BeTrue();
        sut.Duration.Should().Be(duration);
        sut.Outputs.Should().HaveCount(2);
        sut.Outputs["result"].Should().Be("success");
    }

    [Fact]
    public void Constructor_WithFailedStep_ShouldWork()
    {
        // Arrange & Act
        var sut = new ExecutedStep(
            "FailingStep",
            false,
            TimeSpan.FromSeconds(5),
            new Dictionary<string, object> { { "error", "timeout" } });

        // Assert
        sut.Success.Should().BeFalse();
        sut.Outputs.Should().ContainKey("error");
    }

    [Fact]
    public void Constructor_WithEmptyOutputs_ShouldWork()
    {
        // Arrange & Act
        var sut = new ExecutedStep("NoOutput", true, TimeSpan.Zero, new Dictionary<string, object>());

        // Assert
        sut.Outputs.Should().BeEmpty();
        sut.Duration.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void RecordEquality_SameReferences_ShouldBeEqual()
    {
        // Arrange
        var outputs = new Dictionary<string, object>();
        var duration = TimeSpan.FromSeconds(1);
        var a = new ExecutedStep("S", true, duration, outputs);
        var b = new ExecutedStep("S", true, duration, outputs);

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void WithExpression_ShouldCreateModifiedCopy()
    {
        // Arrange
        var original = new ExecutedStep("Step", true, TimeSpan.FromSeconds(1), new Dictionary<string, object>());

        // Act
        var modified = original with { Success = false };

        // Assert
        modified.Success.Should().BeFalse();
        modified.StepName.Should().Be("Step");
    }
}
