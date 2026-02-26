namespace Ouroboros.Tests.Pipeline.Verification;

using Ouroboros.Pipeline.Verification;

[Trait("Category", "Unit")]
public class SecurityExceptionTests
{
    [Fact]
    public void Constructor_WithMessage_SetsMessage()
    {
        var ex = new SecurityException("access denied");
        ex.Message.Should().Be("access denied");
        ex.ViolatingAction.Should().BeNull();
        ex.Context.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithActionAndContext_SetsAllProperties()
    {
        var ex = new SecurityException("blocked", "write", SafeContext.ReadOnly);

        ex.Message.Should().Be("blocked");
        ex.ViolatingAction.Should().Be("write");
        ex.Context.Should().Be(SafeContext.ReadOnly);
    }

    [Fact]
    public void Constructor_WithInnerException_SetsInnerException()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new SecurityException("outer", inner);

        ex.Message.Should().Be("outer");
        ex.InnerException.Should().Be(inner);
    }

    [Fact]
    public void InheritsFromException()
    {
        var ex = new SecurityException("test");
        ex.Should().BeAssignableTo<Exception>();
    }
}
