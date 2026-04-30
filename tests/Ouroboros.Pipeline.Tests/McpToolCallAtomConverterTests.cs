using Ouroboros.Pipeline;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class McpToolCallAtomConverterTests
{
    // ── ToAtom ───────────────────────────────────────────────────────────────

    [Fact]
    public void ToAtom_SimpleIntent_ReturnsMeTTaExpression()
    {
        var intent = new ToolCallIntent("search", "{\"query\":\"test\"}", ToolCallFormat.XmlTag);

        string atom = McpToolCallAtomConverter.ToAtom(intent);

        atom.Should().Be("(MkToolCall \"search\" \"{\\\"query\\\":\\\"test\\\"}\")");
    }

    [Fact]
    public void ToAtom_EmptyArgs_ReturnsMeTTaExpression()
    {
        var intent = new ToolCallIntent("list_files", "{}", ToolCallFormat.NativeApi);

        string atom = McpToolCallAtomConverter.ToAtom(intent);

        atom.Should().Be("(MkToolCall \"list_files\" \"{}\")");
    }

    // ── ToAtoms ──────────────────────────────────────────────────────────────

    [Fact]
    public void ToAtoms_SingleIntent_ReturnsSingleAtom()
    {
        var intents = new[] { new ToolCallIntent("search", "{}", ToolCallFormat.XmlTag) };

        string atoms = McpToolCallAtomConverter.ToAtoms(intents);

        atoms.Should().StartWith("(MkToolCall \"search\"");
    }

    [Fact]
    public void ToAtoms_MultipleIntents_ReturnsMultipleLines()
    {
        var intents = new[]
        {
            new ToolCallIntent("tool1", "{}", ToolCallFormat.XmlTag),
            new ToolCallIntent("tool2", "{}", ToolCallFormat.XmlTag)
        };

        string atoms = McpToolCallAtomConverter.ToAtoms(intents);

        atoms.Should().Contain("tool1");
        atoms.Should().Contain("tool2");
        atoms.Split('\n').Should().HaveCount(2);
    }

    [Fact]
    public void ToAtoms_Empty_ReturnsEmpty()
    {
        McpToolCallAtomConverter.ToAtoms([]).Should().BeEmpty();
    }

    // ── ToChainAtom ──────────────────────────────────────────────────────────

    [Fact]
    public void ToChainAtom_SingleIntent_ReturnsSimpleAtom()
    {
        var intents = new[] { new ToolCallIntent("search", "{}", ToolCallFormat.XmlTag) };

        string chain = McpToolCallAtomConverter.ToChainAtom(intents);

        chain.Should().StartWith("(MkToolCall");
        chain.Should().NotContain("MkToolChain");
    }

    [Fact]
    public void ToChainAtom_TwoIntents_ReturnsChain()
    {
        var intents = new[]
        {
            new ToolCallIntent("summarize", "{}", ToolCallFormat.XmlTag),
            new ToolCallIntent("generate_code", "{}", ToolCallFormat.XmlTag)
        };

        string chain = McpToolCallAtomConverter.ToChainAtom(intents);

        chain.Should().StartWith("(MkToolChain");
        chain.Should().Contain("summarize");
        chain.Should().Contain("generate_code");
    }

    [Fact]
    public void ToChainAtom_ThreeIntents_ReturnsNestedChain()
    {
        var intents = new[]
        {
            new ToolCallIntent("a", "{}", ToolCallFormat.XmlTag),
            new ToolCallIntent("b", "{}", ToolCallFormat.XmlTag),
            new ToolCallIntent("c", "{}", ToolCallFormat.XmlTag)
        };

        string chain = McpToolCallAtomConverter.ToChainAtom(intents);

        // Should be right-associative: (MkToolChain a (MkToolChain b c))
        chain.Should().StartWith("(MkToolChain (MkToolCall \"a\"");
        chain.Should().Contain("(MkToolChain (MkToolCall \"b\"");
    }

    // ── ToPermissionCheck ────────────────────────────────────────────────────

    [Fact]
    public void ToPermissionCheck_ReadOnly_GeneratesCorrectAtom()
    {
        var intent = new ToolCallIntent("search", "{}", ToolCallFormat.XmlTag);

        string check = McpToolCallAtomConverter.ToPermissionCheck(intent, "ReadOnly");

        check.Should().Be("(ToolCallAllowed (MkToolCall \"search\" \"{}\") ReadOnly)");
    }

    [Fact]
    public void ToPermissionCheck_FullAccess_GeneratesCorrectAtom()
    {
        var intent = new ToolCallIntent("delete", "{}", ToolCallFormat.XmlTag);

        string check = McpToolCallAtomConverter.ToPermissionCheck(intent, "FullAccess");

        check.Should().Contain("FullAccess");
    }

    // ── ToFormatAtom ─────────────────────────────────────────────────────────

    [Fact]
    public void ToFormatAtom_XmlTag_ReturnsCorrectFormat()
    {
        var intent = new ToolCallIntent("search", "{}", ToolCallFormat.XmlTag);

        string atom = McpToolCallAtomConverter.ToFormatAtom(intent);

        atom.Should().Contain("XmlTagFormat");
        atom.Should().StartWith("(ParsedFrom");
    }

    [Fact]
    public void ToFormatAtom_BracketLegacy_ReturnsCorrectFormat()
    {
        var intent = new ToolCallIntent("search", "{}", ToolCallFormat.BracketLegacy);

        string atom = McpToolCallAtomConverter.ToFormatAtom(intent);

        atom.Should().Contain("BracketLegacyFormat");
    }

    // ── ToResultAtom ─────────────────────────────────────────────────────────

    [Fact]
    public void ToResultAtom_Success_ReturnsAtomWithFalseError()
    {
        string atom = McpToolCallAtomConverter.ToResultAtom("search", "found 5 results", false);

        atom.Should().Contain("MkToolResult");
        atom.Should().Contain("search");
        atom.Should().Contain("found 5 results");
        atom.Should().Contain("False");
    }

    [Fact]
    public void ToResultAtom_Error_ReturnsAtomWithTrueError()
    {
        string atom = McpToolCallAtomConverter.ToResultAtom("search", "timeout", true);

        atom.Should().Contain("True");
    }

    // ── ToRetryMutationAtom ──────────────────────────────────────────────────

    [Fact]
    public void ToRetryMutationAtom_GeneratesMutationAndOutcome()
    {
        string atoms = McpToolCallAtomConverter.ToRetryMutationAtom(
            "attempt-1", "FormatHint", 2, "MutationEvolved");

        atoms.Should().Contain("MkRetryMutation");
        atoms.Should().Contain("attempt-1");
        atoms.Should().Contain("FormatHint");
        atoms.Should().Contain("2");
        atoms.Should().Contain("HasMutationOutcome");
        atoms.Should().Contain("MutationEvolved");
    }

    // ── FromAtom (round-trip) ────────────────────────────────────────────────

    [Fact]
    public void FromAtom_ValidMkToolCall_ParsesIntent()
    {
        string atom = "(MkToolCall \"search\" \"{}\")";

        ToolCallIntent? intent = McpToolCallAtomConverter.FromAtom(atom);

        intent.Should().NotBeNull();
        intent!.ToolName.Should().Be("search");
        intent.ArgumentsJson.Should().Be("{}");
        intent.Format.Should().Be(ToolCallFormat.NativeApi);
    }

    [Fact]
    public void FromAtom_InvalidAtom_ReturnsNull()
    {
        McpToolCallAtomConverter.FromAtom("(SomethingElse)").Should().BeNull();
        McpToolCallAtomConverter.FromAtom("").Should().BeNull();
        McpToolCallAtomConverter.FromAtom(null!).Should().BeNull();
    }

    [Fact]
    public void RoundTrip_ToAtomThenFromAtom_PreservesData()
    {
        var original = new ToolCallIntent("my_tool", "{\"key\":\"value\"}", ToolCallFormat.XmlTag);

        string atom = McpToolCallAtomConverter.ToAtom(original);
        ToolCallIntent? parsed = McpToolCallAtomConverter.FromAtom(atom);

        parsed.Should().NotBeNull();
        parsed!.ToolName.Should().Be(original.ToolName);
        parsed.ArgumentsJson.Should().Be(original.ArgumentsJson);
    }
}
