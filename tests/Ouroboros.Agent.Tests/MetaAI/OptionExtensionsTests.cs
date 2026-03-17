using FluentAssertions;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class OptionExtensionsTests
{
    [Fact]
    public void ToOption_NonNullValue_ReturnsSome()
    {
        string value = "hello";

        var option = value.ToOption();

        option.HasValue.Should().BeTrue();
        option.Value.Should().Be("hello");
    }

    [Fact]
    public void ToOption_NullValue_ReturnsNone()
    {
        string? value = null;

        var option = value.ToOption();

        option.HasValue.Should().BeFalse();
    }

    [Fact]
    public void ToOption_EmptyString_ReturnsSome()
    {
        string value = string.Empty;

        var option = value.ToOption();

        option.HasValue.Should().BeTrue();
        option.Value.Should().BeEmpty();
    }
}
