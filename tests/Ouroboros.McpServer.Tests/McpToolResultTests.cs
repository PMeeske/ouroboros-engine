namespace Ouroboros.McpServer.Tests;

[Trait("Category", "Unit")]
public class McpToolResultTests
{
    [Fact]
    public void Success_WithContent_SetsIsErrorFalseAndContent()
    {
        var result = McpToolResult.Success("hello");

        result.IsError.Should().BeFalse();
        result.Content.Should().Be("hello");
    }

    [Fact]
    public void Success_WithEmptyContent_SetsEmptyContent()
    {
        var result = McpToolResult.Success(string.Empty);

        result.IsError.Should().BeFalse();
        result.Content.Should().BeEmpty();
    }

    [Fact]
    public void Error_WithMessage_SetsIsErrorTrueAndContent()
    {
        var result = McpToolResult.Error("something failed");

        result.IsError.Should().BeTrue();
        result.Content.Should().Be("something failed");
    }

    [Fact]
    public void Error_WithEmptyMessage_SetsIsErrorTrueAndEmptyContent()
    {
        var result = McpToolResult.Error(string.Empty);

        result.IsError.Should().BeTrue();
        result.Content.Should().BeEmpty();
    }

    [Fact]
    public void DefaultInstance_HasIsErrorFalseAndEmptyContent()
    {
        var result = new McpToolResult();

        result.IsError.Should().BeFalse();
        result.Content.Should().BeEmpty();
    }
}
