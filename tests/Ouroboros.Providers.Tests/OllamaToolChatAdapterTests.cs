using Ouroboros.Providers.Resilience;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class OllamaToolChatAdapterTests
{
    // ── Construction ─────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        var registry = new ToolRegistry();
        var parser = new McpToolCallParser();

        var adapter = new OllamaToolChatAdapter(
            "http://localhost:11434",
            "mistral:latest",
            registry,
            parser);

        adapter.Should().NotBeNull();
        adapter.Dispose();
    }

    [Fact]
    public void Constructor_WithApiKey_CreatesInstance()
    {
        var registry = new ToolRegistry();
        var parser = new McpToolCallParser();

        var adapter = new OllamaToolChatAdapter(
            "https://cloud.ollama.com",
            "mistral:latest",
            registry,
            parser,
            apiKey: "test-key");

        adapter.Should().NotBeNull();
        adapter.Dispose();
    }

    [Fact]
    public void Constructor_NullEndpoint_Throws()
    {
        var act = () => new OllamaToolChatAdapter(
            (string)null!,
            "model",
            new ToolRegistry(),
            new McpToolCallParser());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_NullTools_Throws()
    {
        var act = () => new OllamaToolChatAdapter(
            "http://localhost:11434",
            "model",
            null!,
            new McpToolCallParser());

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullParser_Throws()
    {
        var act = () => new OllamaToolChatAdapter(
            "http://localhost:11434",
            "model",
            new ToolRegistry(),
            null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithRetryPolicy_AcceptsPolicy()
    {
        var policy = EvolutionaryRetryPolicyBuilder.ForToolCallsWithDefaults().Build();

        var adapter = new OllamaToolChatAdapter(
            "http://localhost:11434",
            "mistral:latest",
            new ToolRegistry(),
            new McpToolCallParser(),
            retryPolicy: policy);

        adapter.Should().NotBeNull();
        adapter.Dispose();
    }

    // ── IChatClientBridge ────────────────────────────────────────────────────

    [Fact]
    public void GetChatClient_ReturnsOllamaApiClient()
    {
        var adapter = new OllamaToolChatAdapter(
            "http://localhost:11434",
            "model",
            new ToolRegistry(),
            new McpToolCallParser());

        var chatClient = adapter.GetChatClient();

        chatClient.Should().NotBeNull();
        chatClient.Should().BeAssignableTo<Microsoft.Extensions.AI.IChatClient>();
        adapter.Dispose();
    }

    // ── ICostAwareChatModel ─────────────────────────────────────────────────

    [Fact]
    public void CostTracker_IsCreatedByDefault()
    {
        using var adapter = new OllamaToolChatAdapter(
            "http://localhost:11434",
            "model",
            new ToolRegistry(),
            new McpToolCallParser());

        adapter.CostTracker.Should().NotBeNull();
    }

    [Fact]
    public void CostTracker_UsesProvidedTracker()
    {
        var tracker = new LlmCostTracker("test-model");
        using var adapter = new OllamaToolChatAdapter(
            "http://localhost:11434",
            "model",
            new ToolRegistry(),
            new McpToolCallParser(),
            costTracker: tracker);

        adapter.CostTracker.Should().BeSameAs(tracker);
    }

    // ── NeuralPathway ───────────────────────────────────────────────────────

    [Fact]
    public void Pathway_NullByDefault()
    {
        using var adapter = new OllamaToolChatAdapter(
            "http://localhost:11434",
            "model",
            new ToolRegistry(),
            new McpToolCallParser());

        adapter.Pathway.Should().BeNull();
    }

    // ── ToolDefinitionSlim ───────────────────────────────────────────────────

    [Fact]
    public void ToolDefinitionSlim_Record_PreservesProperties()
    {
        var def = new ToolDefinitionSlim("search", "Search the web", "{\"type\":\"object\"}");

        def.Name.Should().Be("search");
        def.Description.Should().Be("Search the web");
        def.JsonSchema.Should().Be("{\"type\":\"object\"}");
    }

    [Fact]
    public void ToolCallContext_Clone_CreatesDeepCopy()
    {
        var original = new ToolCallContext
        {
            Prompt = "test",
            Tools = [new ToolDefinitionSlim("t1", "d1", null)],
            Temperature = 0.5f,
            Generation = 2
        };
        original.History.Add(new MutationHistoryEntry("s", 1, new Exception(), DateTime.UtcNow));

        var clone = original.Clone();

        clone.Should().NotBeSameAs(original);
        clone.Prompt.Should().Be(original.Prompt);
        clone.Tools.Should().HaveCount(1);
        clone.Temperature.Should().Be(0.5f);
        clone.Generation.Should().Be(2);
        clone.History.Should().HaveCount(1);
    }
}
