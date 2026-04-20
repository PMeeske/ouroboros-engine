using FluentAssertions;
using Ouroboros.Pipeline.Verification;

namespace Ouroboros.Tests.Verification;

[Trait("Category", "Unit")]
public class SafeContextExtensionsTests
{
    [Fact]
    public void ToMeTTaAtom_ReadOnly_ReturnsReadOnly()
    {
        // Arrange
        var context = SafeContext.ReadOnly;

        // Act
        var atom = context.ToMeTTaAtom();

        // Assert
        atom.Should().Be("ReadOnly");
    }

    [Fact]
    public void ToMeTTaAtom_FullAccess_ReturnsFullAccess()
    {
        // Arrange
        var context = SafeContext.FullAccess;

        // Act
        var atom = context.ToMeTTaAtom();

        // Assert
        atom.Should().Be("FullAccess");
    }

    [Fact]
    public void ToMeTTaAtom_InvalidContext_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var context = (SafeContext)999;

        // Act
        var act = () => context.ToMeTTaAtom();

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be("context");
    }
}
