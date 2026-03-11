using FluentAssertions;
using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class HumanInTheLoopConfigTests
{
    [Fact]
    public void DefaultConstructor_ShouldSetDefaults()
    {
        // Arrange & Act
        var sut = new HumanInTheLoopConfig();

        // Assert
        sut.RequireApprovalForCriticalSteps.Should().BeTrue();
        sut.EnableInteractiveRefinement.Should().BeTrue();
        sut.DefaultTimeout.Should().Be(default(TimeSpan));
    }

    [Fact]
    public void Constructor_WithCustomValues_ShouldSetAllProperties()
    {
        // Arrange
        var patterns = new List<string> { "delete*", "deploy*" };
        var timeout = TimeSpan.FromMinutes(5);

        // Act
        var sut = new HumanInTheLoopConfig(
            RequireApprovalForCriticalSteps: false,
            EnableInteractiveRefinement: false,
            DefaultTimeout: timeout,
            CriticalActionPatterns: patterns);

        // Assert
        sut.RequireApprovalForCriticalSteps.Should().BeFalse();
        sut.EnableInteractiveRefinement.Should().BeFalse();
        sut.DefaultTimeout.Should().Be(timeout);
        sut.CriticalActionPatterns.Should().HaveCount(2);
    }

    [Fact]
    public void RecordEquality_SameDefaults_ShouldBeEqual()
    {
        // Arrange
        var a = new HumanInTheLoopConfig();
        var b = new HumanInTheLoopConfig();

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void WithExpression_ShouldCreateModifiedCopy()
    {
        // Arrange
        var original = new HumanInTheLoopConfig();

        // Act
        var modified = original with { RequireApprovalForCriticalSteps = false };

        // Assert
        modified.RequireApprovalForCriticalSteps.Should().BeFalse();
        modified.EnableInteractiveRefinement.Should().BeTrue();
    }
}
