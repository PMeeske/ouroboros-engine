using FluentAssertions;
using Ouroboros.Pipeline.Verification;

namespace Ouroboros.Tests.Verification;

[Trait("Category", "Unit")]
public class SafeContextTests
{
    [Fact]
    public void ReadOnly_HasExpectedValue()
    {
        // Arrange & Act
        var context = SafeContext.ReadOnly;

        // Assert
        context.Should().Be(SafeContext.ReadOnly);
        ((int)context).Should().Be(0);
    }

    [Fact]
    public void FullAccess_HasExpectedValue()
    {
        // Arrange & Act
        var context = SafeContext.FullAccess;

        // Assert
        context.Should().Be(SafeContext.FullAccess);
        ((int)context).Should().Be(1);
    }

    [Fact]
    public void Enum_HasExactlyTwoValues()
    {
        // Arrange & Act
        var values = Enum.GetValues<SafeContext>();

        // Assert
        values.Should().HaveCount(2);
        values.Should().Contain(SafeContext.ReadOnly);
        values.Should().Contain(SafeContext.FullAccess);
    }
}
