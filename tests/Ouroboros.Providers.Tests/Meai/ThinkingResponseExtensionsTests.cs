using Microsoft.Extensions.AI;
using Ouroboros.Providers.Meai;

namespace Ouroboros.Tests.Meai;

[Trait("Category", "Unit")]
public sealed class ThinkingResponseExtensionsTests
{
    [Fact]
    public void ToThinkingResponse_WithThinkingContent_ExtractsCorrectly()
    {
        var response = new ChatResponse(
        [
            new ChatMessage(ChatRole.Assistant,
            [
                new ThinkingAIContent("my thoughts"),
                new TextContent("final answer")
            ])
        ]);

        var result = response.ToThinkingResponse();

        result.Thinking.Should().Be("my thoughts");
        result.Content.Should().Be("final answer");
        result.HasThinking.Should().BeTrue();
    }

    [Fact]
    public void ToThinkingResponse_NoThinking_ReturnsNullThinking()
    {
        var response = new ChatResponse(
        [
            new ChatMessage(ChatRole.Assistant, "just content")
        ]);

        var result = response.ToThinkingResponse();

        result.Thinking.Should().BeNull();
        result.Content.Should().Be("just content");
        result.HasThinking.Should().BeFalse();
    }

    [Fact]
    public void HasThinkingContent_WithThinking_ReturnsTrue()
    {
        var update = new ChatResponseUpdate
        {
            Contents = [new ThinkingAIContent("thought")]
        };

        update.HasThinkingContent().Should().BeTrue();
    }

    [Fact]
    public void HasThinkingContent_WithoutThinking_ReturnsFalse()
    {
        var update = new ChatResponseUpdate
        {
            Contents = [new TextContent("text")]
        };

        update.HasThinkingContent().Should().BeFalse();
    }

    [Fact]
    public void GetThinkingText_WithThinking_ReturnsText()
    {
        var update = new ChatResponseUpdate
        {
            Contents = [new ThinkingAIContent("thought text")]
        };

        update.GetThinkingText().Should().Be("thought text");
    }

    [Fact]
    public void GetThinkingText_WithoutThinking_ReturnsNull()
    {
        var update = new ChatResponseUpdate
        {
            Contents = [new TextContent("regular")]
        };

        update.GetThinkingText().Should().BeNull();
    }

    [Fact]
    public void ToThinkingResponse_MultipleMessages_ConcatenatesContent()
    {
        var response = new ChatResponse(
        [
            new ChatMessage(ChatRole.Assistant, [new TextContent("part1")]),
            new ChatMessage(ChatRole.Assistant, [new TextContent("part2")])
        ]);

        var result = response.ToThinkingResponse();

        result.Content.Should().Be("part1part2");
    }
}
