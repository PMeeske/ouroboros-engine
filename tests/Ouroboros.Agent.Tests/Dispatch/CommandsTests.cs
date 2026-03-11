using FluentAssertions;
using Ouroboros.Agent.Dispatch;

namespace Ouroboros.Tests.Dispatch;

[Trait("Category", "Unit")]
public class CommandsTests
{
    [Fact]
    public void ProcessMindCommand_SetsProperties()
    {
        // Arrange & Act
        var sut = new ProcessMindCommand("Hello world", Complex: true);

        // Assert
        sut.Prompt.Should().Be("Hello world");
        sut.Complex.Should().BeTrue();
    }

    [Fact]
    public void ProcessMindCommand_DefaultComplexIsFalse()
    {
        // Arrange & Act
        var sut = new ProcessMindCommand("Simple prompt");

        // Assert
        sut.Prompt.Should().Be("Simple prompt");
        sut.Complex.Should().BeFalse();
    }

    [Fact]
    public void SelectModelCommand_SetsProperties()
    {
        // Arrange
        var context = new Dictionary<string, object> { { "domain", "science" } };

        // Act
        var sut = new SelectModelCommand("Test prompt", context);

        // Assert
        sut.Prompt.Should().Be("Test prompt");
        sut.Context.Should().BeEquivalentTo(context);
    }

    [Fact]
    public void SelectModelCommand_DefaultContextIsNull()
    {
        // Arrange & Act
        var sut = new SelectModelCommand("Test prompt");

        // Assert
        sut.Prompt.Should().Be("Test prompt");
        sut.Context.Should().BeNull();
    }

    [Fact]
    public void CreatePlanCommand_SetsProperties()
    {
        // Arrange
        var context = new Dictionary<string, object> { { "priority", "high" } };

        // Act
        var sut = new CreatePlanCommand("Build a feature", context);

        // Assert
        sut.Goal.Should().Be("Build a feature");
        sut.Context.Should().BeEquivalentTo(context);
    }

    [Fact]
    public void CreatePlanCommand_DefaultContextIsNull()
    {
        // Arrange & Act
        var sut = new CreatePlanCommand("Build a feature");

        // Assert
        sut.Goal.Should().Be("Build a feature");
        sut.Context.Should().BeNull();
    }
}
