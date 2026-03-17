using FluentAssertions;
using NSubstitute;
using Ouroboros.Pipeline.WorldModel;
using Ouroboros.Tools;

namespace Ouroboros.Tests.WorldModel;

[Trait("Category", "Unit")]
public sealed class ToolSelectionTests
{
    [Fact]
    public void Empty_HasNoTools()
    {
        // Act
        var selection = ToolSelection.Empty;

        // Assert
        selection.SelectedTools.Should().BeEmpty();
        selection.HasTools.Should().BeFalse();
        selection.ConfidenceScore.Should().Be(0.0);
        selection.Reasoning.Should().Be("No tools selected.");
    }

    [Fact]
    public void Failed_CreatesEmptySelectionWithReason()
    {
        // Act
        var selection = ToolSelection.Failed("No matching tools");

        // Assert
        selection.HasTools.Should().BeFalse();
        selection.Reasoning.Should().Be("No matching tools");
        selection.ConfidenceScore.Should().Be(0.0);
    }

    [Fact]
    public void Failed_NullReason_ThrowsArgumentNullException()
    {
        var act = () => ToolSelection.Failed(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void HasTools_WithTools_ReturnsTrue()
    {
        // Arrange
        var tool = Substitute.For<ITool>();
        tool.Name.Returns("my_tool");
        var selection = new ToolSelection(
            new[] { tool },
            "Selected 1 tool",
            0.85,
            [],
            []);

        // Assert
        selection.HasTools.Should().BeTrue();
    }

    [Fact]
    public void ToolNames_ReturnsImmutableSetOfNames()
    {
        // Arrange
        var tool1 = Substitute.For<ITool>();
        tool1.Name.Returns("tool_a");
        var tool2 = Substitute.For<ITool>();
        tool2.Name.Returns("tool_b");
        var selection = new ToolSelection(
            new[] { tool1, tool2 },
            "Selected tools",
            0.9,
            [],
            []);

        // Act
        var names = selection.ToolNames;

        // Assert
        names.Should().HaveCount(2);
        names.Should().Contain("tool_a");
        names.Should().Contain("tool_b");
    }

    [Fact]
    public void WithAppliedConstraint_AddsConstraint()
    {
        // Arrange
        var selection = ToolSelection.Empty;
        var constraint = Constraint.Create("no-writes", "exclude:write_tool");

        // Act
        var updated = selection.WithAppliedConstraint(constraint);

        // Assert
        updated.AppliedConstraints.Should().HaveCount(1);
        updated.AppliedConstraints[0].Name.Should().Be("no-writes");
        selection.AppliedConstraints.Should().BeEmpty(); // original unchanged
    }

    [Fact]
    public void WithAppliedConstraint_NullConstraint_ThrowsArgumentNullException()
    {
        var act = () => ToolSelection.Empty.WithAppliedConstraint(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WithAppliedConstraint_MultipleConstraints_AccumulatesAll()
    {
        // Arrange
        var selection = ToolSelection.Empty;
        var c1 = Constraint.Create("c1", "rule1");
        var c2 = Constraint.Create("c2", "rule2");

        // Act
        var updated = selection
            .WithAppliedConstraint(c1)
            .WithAppliedConstraint(c2);

        // Assert
        updated.AppliedConstraints.Should().HaveCount(2);
    }
}
