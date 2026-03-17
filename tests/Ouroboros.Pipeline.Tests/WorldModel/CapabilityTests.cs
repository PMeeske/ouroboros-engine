using FluentAssertions;
using Ouroboros.Pipeline.WorldModel;

namespace Ouroboros.Tests.WorldModel;

[Trait("Category", "Unit")]
public sealed class CapabilityTests
{
    [Fact]
    public void Create_WithNoTools_ReturnsCapabilityWithEmptyToolList()
    {
        // Act
        var capability = Capability.Create("search", "Search capability");

        // Assert
        capability.Name.Should().Be("search");
        capability.Description.Should().Be("Search capability");
        capability.RequiredTools.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithTools_ReturnsCapabilityWithToolList()
    {
        // Act
        var capability = Capability.Create("analyze", "Analysis capability", "tool1", "tool2");

        // Assert
        capability.RequiredTools.Should().HaveCount(2);
        capability.RequiredTools.Should().Contain("tool1");
        capability.RequiredTools.Should().Contain("tool2");
    }

    [Fact]
    public void Create_NullName_ThrowsArgumentNullException()
    {
        var act = () => Capability.Create(null!, "desc");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_NullDescription_ThrowsArgumentNullException()
    {
        var act = () => Capability.Create("name", null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_NullTools_ThrowsArgumentNullException()
    {
        var act = () => Capability.Create("name", "desc", null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CanExecuteWith_AllToolsAvailable_ReturnsTrue()
    {
        // Arrange
        var capability = Capability.Create("cap", "desc", "tool_a", "tool_b");
        var availableTools = new HashSet<string> { "tool_a", "tool_b", "tool_c" };

        // Act & Assert
        capability.CanExecuteWith(availableTools).Should().BeTrue();
    }

    [Fact]
    public void CanExecuteWith_SomeToolsMissing_ReturnsFalse()
    {
        // Arrange
        var capability = Capability.Create("cap", "desc", "tool_a", "tool_b");
        var availableTools = new HashSet<string> { "tool_a" };

        // Act & Assert
        capability.CanExecuteWith(availableTools).Should().BeFalse();
    }

    [Fact]
    public void CanExecuteWith_NoRequiredTools_ReturnsTrue()
    {
        // Arrange
        var capability = Capability.Create("cap", "desc");
        var availableTools = new HashSet<string>();

        // Act & Assert
        capability.CanExecuteWith(availableTools).Should().BeTrue();
    }

    [Fact]
    public void CanExecuteWith_NullAvailableTools_ThrowsArgumentNullException()
    {
        // Arrange
        var capability = Capability.Create("cap", "desc");

        // Act
        var act = () => capability.CanExecuteWith(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        // Arrange
        var cap1 = Capability.Create("search", "Search", "tool1");
        var cap2 = Capability.Create("search", "Search", "tool1");

        // Assert
        cap1.Should().Be(cap2);
    }
}
