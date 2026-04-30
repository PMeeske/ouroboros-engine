using Ouroboros.Providers.Meai;

namespace Ouroboros.Tests.Meai;

[Trait("Category", "Unit")]
public sealed class ThinkingAIContentTests
{
    [Fact]
    public void Constructor_SetsText()
    {
        var content = new ThinkingAIContent("thinking text");
        content.Text.Should().Be("thinking text");
    }

    [Fact]
    public void ToString_ReturnsText()
    {
        var content = new ThinkingAIContent("hello");
        content.ToString().Should().Be("hello");
    }

    [Fact]
    public void Constructor_EmptyText_Accepted()
    {
        var content = new ThinkingAIContent("");
        content.Text.Should().BeEmpty();
    }
}
