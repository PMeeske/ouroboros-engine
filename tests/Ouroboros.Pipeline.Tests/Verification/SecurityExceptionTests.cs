using FluentAssertions;
using Ouroboros.Pipeline.Verification;

namespace Ouroboros.Tests.Verification;

[Trait("Category", "Unit")]
public class SecurityExceptionTests
{
    [Fact]
    public void Constructor_WithMessageOnly_SetsMessage()
    {
        // Arrange & Act
        var ex = new SecurityException("test message");

        // Assert
        ex.Message.Should().Be("test message");
        ex.ViolatingAction.Should().BeNull();
        ex.Context.Should().BeNull();
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithMessageActionAndContext_SetsAllProperties()
    {
        // Arrange & Act
        var ex = new SecurityException("violation occurred", "write", SafeContext.ReadOnly);

        // Assert
        ex.Message.Should().Be("violation occurred");
        ex.ViolatingAction.Should().Be("write");
        ex.Context.Should().Be(SafeContext.ReadOnly);
    }

    [Fact]
    public void Constructor_WithMessageActionAndFullAccessContext_SetsContext()
    {
        // Arrange & Act
        var ex = new SecurityException("msg", "delete", SafeContext.FullAccess);

        // Assert
        ex.Context.Should().Be(SafeContext.FullAccess);
    }

    [Fact]
    public void Constructor_WithMessageAndInnerException_SetsInnerException()
    {
        // Arrange
        var inner = new InvalidOperationException("inner error");

        // Act
        var ex = new SecurityException("outer message", inner);

        // Assert
        ex.Message.Should().Be("outer message");
        ex.InnerException.Should().BeSameAs(inner);
        ex.ViolatingAction.Should().BeNull();
        ex.Context.Should().BeNull();
    }

    [Fact]
    public void IsException_InheritsFromException()
    {
        // Arrange & Act
        var ex = new SecurityException("test");

        // Assert
        ex.Should().BeAssignableTo<Exception>();
    }

    [Fact]
    public void Constructor_WithEmptyMessage_DoesNotThrow()
    {
        // Arrange & Act
        var act = () => new SecurityException(string.Empty);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithEmptyViolatingAction_SetsProperty()
    {
        // Arrange & Act
        var ex = new SecurityException("msg", string.Empty, SafeContext.ReadOnly);

        // Assert
        ex.ViolatingAction.Should().BeEmpty();
    }
}
