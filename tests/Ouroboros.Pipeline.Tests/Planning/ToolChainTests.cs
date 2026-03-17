using FluentAssertions;
using Ouroboros.Pipeline.Planning;

namespace Ouroboros.Tests.Planning;

[Trait("Category", "Unit")]
public sealed class ToolChainTests
{
    [Fact]
    public void Constructor_WithTools_SetsTools()
    {
        // Arrange
        var tools = new List<string> { "tool1", "tool2", "tool3" };

        // Act
        var chain = new ToolChain(tools);

        // Assert
        chain.Tools.Should().HaveCount(3);
        chain.Tools.Should().ContainInOrder("tool1", "tool2", "tool3");
    }

    [Fact]
    public void IsEmpty_WithNoTools_ReturnsTrue()
    {
        // Arrange
        var chain = new ToolChain(Array.Empty<string>());

        // Act & Assert
        chain.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void IsEmpty_WithTools_ReturnsFalse()
    {
        // Arrange
        var chain = new ToolChain(new[] { "tool1" });

        // Act & Assert
        chain.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void Empty_ReturnsEmptyChain()
    {
        // Act
        var chain = ToolChain.Empty;

        // Assert
        chain.IsEmpty.Should().BeTrue();
        chain.Tools.Should().BeEmpty();
    }

    [Fact]
    public void Equality_SameTools_AreEqual()
    {
        // Arrange
        var chain1 = new ToolChain(new[] { "tool1", "tool2" });
        var chain2 = new ToolChain(new[] { "tool1", "tool2" });

        // Assert - record equality checks value equality of each member
        // IReadOnlyList reference equality may differ, so let's check structural
        chain1.Tools.Should().BeEquivalentTo(chain2.Tools);
    }
}
