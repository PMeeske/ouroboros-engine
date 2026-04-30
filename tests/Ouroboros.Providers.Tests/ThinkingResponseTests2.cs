namespace Ouroboros.Tests.Providers;

[Trait("Category", "Unit")]
public sealed class ThinkingResponseTests2
{
    [Fact]
    public void HasThinking_WithThinking_ReturnsTrue()
    {
        var response = new ThinkingResponse("thoughts", "content");
        response.HasThinking.Should().BeTrue();
    }

    [Fact]
    public void HasThinking_NullThinking_ReturnsFalse()
    {
        var response = new ThinkingResponse(null, "content");
        response.HasThinking.Should().BeFalse();
    }

    [Fact]
    public void HasThinking_EmptyThinking_ReturnsFalse()
    {
        var response = new ThinkingResponse("", "content");
        response.HasThinking.Should().BeFalse();
    }

    [Fact]
    public void ToFormattedString_NoThinking_ReturnsContentOnly()
    {
        var response = new ThinkingResponse(null, "my content");
        response.ToFormattedString().Should().Be("my content");
    }

    [Fact]
    public void ToFormattedString_WithThinking_IncludesBoth()
    {
        var response = new ThinkingResponse("my thought", "my content");
        var formatted = response.ToFormattedString();

        formatted.Should().Contain("my thought");
        formatted.Should().Contain("my content");
    }

    [Fact]
    public void ToFormattedString_CustomPrefixes()
    {
        var response = new ThinkingResponse("thought", "content");
        var formatted = response.ToFormattedString("T:", " C:");

        formatted.Should().Be("T:thought C:content");
    }

    [Fact]
    public void FromRawText_Null_ReturnsEmptyContent()
    {
        var response = ThinkingResponse.FromRawText(null!);
        response.Content.Should().BeEmpty();
        response.HasThinking.Should().BeFalse();
    }

    [Fact]
    public void FromRawText_Empty_ReturnsEmptyContent()
    {
        var response = ThinkingResponse.FromRawText("");
        response.Content.Should().BeEmpty();
        response.HasThinking.Should().BeFalse();
    }

    [Fact]
    public void FromRawText_WithThinkTag_ExtractsThinking()
    {
        var raw = "<think>my thinking process</think>The final answer";
        var response = ThinkingResponse.FromRawText(raw);

        response.Thinking.Should().Be("my thinking process");
        response.Content.Should().Be("The final answer");
    }

    [Fact]
    public void FromRawText_WithThinkingTag_ExtractsThinking()
    {
        var raw = "<thinking>deep thought</thinking>The result";
        var response = ThinkingResponse.FromRawText(raw);

        response.Thinking.Should().Be("deep thought");
        response.Content.Should().Be("The result");
    }

    [Fact]
    public void FromRawText_NoTags_ReturnsAsContent()
    {
        var raw = "Just a plain response with no tags";
        var response = ThinkingResponse.FromRawText(raw);

        response.Thinking.Should().BeNull();
        response.Content.Should().Be(raw);
    }

    [Fact]
    public void FromRawText_CaseInsensitiveTags()
    {
        var raw = "<THINK>upper case thought</THINK>Result";
        var response = ThinkingResponse.FromRawText(raw);

        response.Thinking.Should().Be("upper case thought");
    }

    [Fact]
    public void TokenCounts_Nullable_DefaultToNull()
    {
        var response = new ThinkingResponse("thought", "content");
        response.ThinkingTokens.Should().BeNull();
        response.ContentTokens.Should().BeNull();
    }

    [Fact]
    public void TokenCounts_WhenProvided_AreSet()
    {
        var response = new ThinkingResponse("thought", "content", ThinkingTokens: 100, ContentTokens: 50);
        response.ThinkingTokens.Should().Be(100);
        response.ContentTokens.Should().Be(50);
    }

    [Fact]
    public void RecordWith_CreatesModifiedCopy()
    {
        var original = new ThinkingResponse("thought", "content");
        var modified = original with { Content = "new content" };

        modified.Content.Should().Be("new content");
        modified.Thinking.Should().Be("thought");
        original.Content.Should().Be("content");
    }
}
