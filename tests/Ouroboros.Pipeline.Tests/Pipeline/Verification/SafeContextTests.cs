namespace Ouroboros.Tests.Pipeline.Verification;

using Ouroboros.Pipeline.Verification;

[Trait("Category", "Unit")]
public class SafeContextTests
{
    [Fact]
    public void ReadOnly_HasExpectedValue()
    {
        ((int)SafeContext.ReadOnly).Should().Be(0);
    }

    [Fact]
    public void FullAccess_HasExpectedValue()
    {
        ((int)SafeContext.FullAccess).Should().Be(1);
    }

    [Fact]
    public void EnumHasExpectedCount()
    {
        Enum.GetValues<SafeContext>().Should().HaveCount(2);
    }
}
