using FluentAssertions;
using Ouroboros.Pipeline.Verification;

namespace Ouroboros.Tests.Verification;

[Trait("Category", "Unit")]
public class FileSystemActionTests
{
    [Fact]
    public void Constructor_WithOperationOnly_SetsProperties()
    {
        // Arrange & Act
        var action = new FileSystemAction("read");

        // Assert
        action.Operation.Should().Be("read");
        action.Path.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithOperationAndPath_SetsProperties()
    {
        // Arrange & Act
        var action = new FileSystemAction("write", "/tmp/file.txt");

        // Assert
        action.Operation.Should().Be("write");
        action.Path.Should().Be("/tmp/file.txt");
    }

    [Fact]
    public void ToMeTTaAtom_ReturnsCorrectFormat()
    {
        // Arrange
        var action = new FileSystemAction("delete", "/tmp/file.txt");

        // Act
        var atom = action.ToMeTTaAtom();

        // Assert
        atom.Should().Be("(FileSystemAction \"delete\")");
    }

    [Fact]
    public void ToMeTTaAtom_DoesNotIncludePath()
    {
        // Arrange
        var action = new FileSystemAction("read", "/some/path");

        // Act
        var atom = action.ToMeTTaAtom();

        // Assert
        atom.Should().NotContain("/some/path");
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        // Arrange
        var action1 = new FileSystemAction("read", "/tmp");
        var action2 = new FileSystemAction("read", "/tmp");

        // Assert
        action1.Should().Be(action2);
    }

    [Fact]
    public void Equality_DifferentOperations_AreNotEqual()
    {
        // Arrange
        var action1 = new FileSystemAction("read");
        var action2 = new FileSystemAction("write");

        // Assert
        action1.Should().NotBe(action2);
    }

    [Fact]
    public void IsPlanAction_InheritsFromPlanAction()
    {
        // Arrange & Act
        var action = new FileSystemAction("read");

        // Assert
        action.Should().BeAssignableTo<PlanAction>();
    }
}

[Trait("Category", "Unit")]
public class NetworkActionTests
{
    [Fact]
    public void Constructor_WithOperationOnly_SetsProperties()
    {
        // Arrange & Act
        var action = new NetworkAction("get");

        // Assert
        action.Operation.Should().Be("get");
        action.Endpoint.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithOperationAndEndpoint_SetsProperties()
    {
        // Arrange & Act
        var action = new NetworkAction("post", "https://api.example.com");

        // Assert
        action.Operation.Should().Be("post");
        action.Endpoint.Should().Be("https://api.example.com");
    }

    [Fact]
    public void ToMeTTaAtom_ReturnsCorrectFormat()
    {
        // Arrange
        var action = new NetworkAction("get");

        // Act
        var atom = action.ToMeTTaAtom();

        // Assert
        atom.Should().Be("(NetworkAction \"get\")");
    }

    [Fact]
    public void ToMeTTaAtom_DoesNotIncludeEndpoint()
    {
        // Arrange
        var action = new NetworkAction("post", "https://api.example.com");

        // Act
        var atom = action.ToMeTTaAtom();

        // Assert
        atom.Should().NotContain("https://api.example.com");
    }

    [Fact]
    public void IsPlanAction_InheritsFromPlanAction()
    {
        // Arrange & Act
        var action = new NetworkAction("connect");

        // Assert
        action.Should().BeAssignableTo<PlanAction>();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        // Arrange
        var a1 = new NetworkAction("get", "https://x.com");
        var a2 = new NetworkAction("get", "https://x.com");

        // Assert
        a1.Should().Be(a2);
    }
}

[Trait("Category", "Unit")]
public class ToolActionTests
{
    [Fact]
    public void Constructor_WithToolNameOnly_SetsProperties()
    {
        // Arrange & Act
        var action = new ToolAction("calculator");

        // Assert
        action.ToolName.Should().Be("calculator");
        action.Arguments.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithToolNameAndArguments_SetsProperties()
    {
        // Arrange & Act
        var action = new ToolAction("search", "query=test");

        // Assert
        action.ToolName.Should().Be("search");
        action.Arguments.Should().Be("query=test");
    }

    [Fact]
    public void ToMeTTaAtom_ReturnsCorrectFormat()
    {
        // Arrange
        var action = new ToolAction("calculator");

        // Act
        var atom = action.ToMeTTaAtom();

        // Assert
        atom.Should().Be("(ToolAction \"calculator\")");
    }

    [Fact]
    public void ToMeTTaAtom_DoesNotIncludeArguments()
    {
        // Arrange
        var action = new ToolAction("search", "query=test");

        // Act
        var atom = action.ToMeTTaAtom();

        // Assert
        atom.Should().NotContain("query=test");
    }

    [Fact]
    public void IsPlanAction_InheritsFromPlanAction()
    {
        // Arrange & Act
        var action = new ToolAction("tool");

        // Assert
        action.Should().BeAssignableTo<PlanAction>();
    }

    [Fact]
    public void Equality_DifferentArguments_AreNotEqual()
    {
        // Arrange
        var a1 = new ToolAction("tool", "arg1");
        var a2 = new ToolAction("tool", "arg2");

        // Assert
        a1.Should().NotBe(a2);
    }
}
