namespace Ouroboros.Tests.Pipeline.Verification;

using Ouroboros.Pipeline.Verification;

[Trait("Category", "Unit")]
public class SafeContextExtensionsTests
{
    [Fact]
    public void ToMeTTaAtom_ReadOnly_ReturnsReadOnly()
    {
        SafeContext.ReadOnly.ToMeTTaAtom().Should().Be("ReadOnly");
    }

    [Fact]
    public void ToMeTTaAtom_FullAccess_ReturnsFullAccess()
    {
        SafeContext.FullAccess.ToMeTTaAtom().Should().Be("FullAccess");
    }

    [Fact]
    public void ToMeTTaAtom_InvalidValue_ThrowsArgumentOutOfRange()
    {
        var act = () => ((SafeContext)99).ToMeTTaAtom();
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
