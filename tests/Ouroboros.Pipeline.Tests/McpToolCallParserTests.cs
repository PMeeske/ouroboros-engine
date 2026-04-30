namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class McpToolCallParserTests
{
    private readonly McpToolCallParser _sut = new();

    // ── XML format ───────────────────────────────────────────────────────────

    [Fact]
    public void Parse_XmlToolCall_ExtractsIntent()
    {
        const string input = """
            Sure, I'll search for that.
            <tool_call>{"name":"search","arguments":{"query":"test"}}</tool_call>
            """;

        var results = _sut.Parse(input);

        results.Should().ContainSingle();
        results[0].ToolName.Should().Be("search");
        results[0].ArgumentsJson.Should().Contain("query");
        results[0].Format.Should().Be(ToolCallFormat.XmlTag);
    }

    [Fact]
    public void Parse_MultipleXmlToolCalls_ExtractsAll()
    {
        const string input = """
            <tool_call>{"name":"tool1","arguments":{}}</tool_call>
            Some text in between.
            <tool_call>{"name":"tool2","arguments":{"x":1}}</tool_call>
            """;

        var results = _sut.Parse(input);

        results.Should().HaveCount(2);
        results[0].ToolName.Should().Be("tool1");
        results[1].ToolName.Should().Be("tool2");
    }

    [Fact]
    public void Parse_XmlToolCall_WithParameters_ExtractsArguments()
    {
        const string input = """<tool_call>{"name":"calc","parameters":{"expr":"2+2"}}</tool_call>""";

        var results = _sut.Parse(input);

        results.Should().ContainSingle();
        results[0].ToolName.Should().Be("calc");
        results[0].ArgumentsJson.Should().Contain("2+2");
    }

    // ── JSON tool_calls format ───────────────────────────────────────────────

    [Fact]
    public void Parse_JsonToolCallsArray_ExtractsIntents()
    {
        const string input = """{"tool_calls":[{"function":{"name":"search","arguments":{"q":"hello"}}}]}""";

        var results = _sut.Parse(input);

        results.Should().ContainSingle();
        results[0].ToolName.Should().Be("search");
        results[0].Format.Should().Be(ToolCallFormat.JsonToolCalls);
    }

    [Fact]
    public void Parse_JsonToolCallsArray_MultipleItems_ExtractsAll()
    {
        const string input = """
            {"tool_calls":[
                {"function":{"name":"search","arguments":{}}},
                {"function":{"name":"calculate","arguments":{"expr":"1+1"}}}
            ]}
            """;

        var results = _sut.Parse(input);

        results.Should().HaveCount(2);
        results[0].ToolName.Should().Be("search");
        results[1].ToolName.Should().Be("calculate");
    }

    [Fact]
    public void Parse_JsonToolCallsArray_DirectFormat_ExtractsIntents()
    {
        // Some models emit tool calls without the "function" wrapper
        const string input = """{"tool_calls":[{"name":"search","arguments":{"q":"test"}}]}""";

        var results = _sut.Parse(input);

        results.Should().ContainSingle();
        results[0].ToolName.Should().Be("search");
    }

    // ── Markdown code block format ───────────────────────────────────────────

    [Fact]
    public void Parse_MarkdownToolCall_ExtractsIntent()
    {
        const string input = """
            Let me call a tool:
            ```tool_call
            {"name":"weather","arguments":{"city":"Berlin"}}
            ```
            """;

        var results = _sut.Parse(input);

        results.Should().ContainSingle();
        results[0].ToolName.Should().Be("weather");
        results[0].Format.Should().Be(ToolCallFormat.MarkdownBlock);
    }

    // ── Bracket legacy format ────────────────────────────────────────────────

    [Fact]
    public void Parse_BracketToolCall_ExtractsIntent()
    {
        const string input = "Let me search: [TOOL:search query about testing]";

        var results = _sut.Parse(input);

        results.Should().ContainSingle();
        results[0].ToolName.Should().Be("search");
        results[0].ArgumentsJson.Should().Be("query about testing");
        results[0].Format.Should().Be(ToolCallFormat.BracketLegacy);
    }

    [Fact]
    public void Parse_BracketToolCall_NoArgs_ExtractsIntent()
    {
        const string input = "[TOOL:list_files]";

        var results = _sut.Parse(input);

        results.Should().ContainSingle();
        results[0].ToolName.Should().Be("list_files");
        results[0].ArgumentsJson.Should().BeEmpty();
    }

    // ── Edge cases ───────────────────────────────────────────────────────────

    [Fact]
    public void Parse_EmptyInput_ReturnsEmpty()
    {
        _sut.Parse("").Should().BeEmpty();
        _sut.Parse(null!).Should().BeEmpty();
        _sut.Parse("   ").Should().BeEmpty();
    }

    [Fact]
    public void Parse_NoToolCalls_ReturnsEmpty()
    {
        _sut.Parse("Just some regular text without any tool calls.").Should().BeEmpty();
    }

    [Fact]
    public void Parse_MalformedJson_SkipsGracefully()
    {
        const string input = "<tool_call>not valid json at all</tool_call>";

        var results = _sut.Parse(input);

        results.Should().BeEmpty();
    }

    [Fact]
    public void Parse_MixedFormats_ExtractsAll()
    {
        const string input = """
            <tool_call>{"name":"xml_tool","arguments":{}}</tool_call>
            [TOOL:bracket_tool some args]
            """;

        var results = _sut.Parse(input);

        results.Should().HaveCount(2);
        results.Should().Contain(r => r.ToolName == "xml_tool" && r.Format == ToolCallFormat.XmlTag);
        results.Should().Contain(r => r.ToolName == "bracket_tool" && r.Format == ToolCallFormat.BracketLegacy);
    }

    // ── ExtractTextSegments ──────────────────────────────────────────────────

    [Fact]
    public void ExtractTextSegments_RemovesToolCalls_KeepsText()
    {
        const string input = """
            Hello! <tool_call>{"name":"search","arguments":{}}</tool_call> How are you?
            """;

        string text = _sut.ExtractTextSegments(input);

        text.Should().Contain("Hello!");
        text.Should().Contain("How are you?");
        text.Should().NotContain("tool_call");
        text.Should().NotContain("search");
    }

    [Fact]
    public void ExtractTextSegments_EmptyInput_ReturnsEmpty()
    {
        _sut.ExtractTextSegments("").Should().BeEmpty();
        _sut.ExtractTextSegments(null!).Should().BeEmpty();
    }
}
