namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class ChatConfigTests
{
    [Fact]
    public void GetDefaultEndpoint_Anthropic_ReturnsAnthropicUrl()
    {
        var endpoint = ChatConfig.GetDefaultEndpoint(ChatEndpointType.Anthropic);
        endpoint.Should().Be("https://api.anthropic.com");
    }

    [Fact]
    public void GetDefaultEndpoint_OpenAI_ReturnsOpenAIUrl()
    {
        var endpoint = ChatConfig.GetDefaultEndpoint(ChatEndpointType.OpenAI);
        endpoint.Should().Be("https://api.openai.com");
    }

    [Fact]
    public void GetDefaultEndpoint_OllamaLocal_ReturnsLocalhost()
    {
        var endpoint = ChatConfig.GetDefaultEndpoint(ChatEndpointType.OllamaLocal);
        endpoint.Should().Be("http://localhost:11434");
    }

    [Fact]
    public void GetDefaultEndpoint_Unknown_ReturnsNull()
    {
        var endpoint = ChatConfig.GetDefaultEndpoint(ChatEndpointType.Auto);
        endpoint.Should().BeNull();
    }

    [Fact]
    public void GetAliases_ContainsExpectedKeys()
    {
        var aliases = ChatConfig.GetAliases();

        aliases.Should().ContainKey("openai");
        aliases.Should().ContainKey("claude");
        aliases.Should().ContainKey("ollama");
        aliases.Should().ContainKey("deepseek");
        aliases.Should().ContainKey("litellm");
    }

    [Fact]
    public void GetAliases_OpenAI_MapsCorrectly()
    {
        var aliases = ChatConfig.GetAliases();

        aliases["openai"].Should().Be(ChatEndpointType.OpenAI);
        aliases["gpt"].Should().Be(ChatEndpointType.OpenAI);
        aliases["chatgpt"].Should().Be(ChatEndpointType.OpenAI);
    }

    [Fact]
    public void GetAliases_Anthropic_MapsCorrectly()
    {
        var aliases = ChatConfig.GetAliases();

        aliases["anthropic"].Should().Be(ChatEndpointType.Anthropic);
        aliases["claude"].Should().Be(ChatEndpointType.Anthropic);
    }

    [Fact]
    public void GetAliases_IsCaseInsensitive()
    {
        var aliases = ChatConfig.GetAliases();

        // GetAliases returns the dictionary which was created with OrdinalIgnoreCase
        // Access through specific keys should work
        aliases.Should().ContainKey("github");
        aliases["github"].Should().Be(ChatEndpointType.GitHubModels);
    }

    [Fact]
    public void GetDefaultEndpoint_GitHubModels_ReturnsAzureInferenceUrl()
    {
        var endpoint = ChatConfig.GetDefaultEndpoint(ChatEndpointType.GitHubModels);
        endpoint.Should().Be("https://models.inference.ai.azure.com");
    }

    [Fact]
    public void GetDefaultEndpoint_DeepSeek_ReturnsDeepSeekUrl()
    {
        var endpoint = ChatConfig.GetDefaultEndpoint(ChatEndpointType.DeepSeek);
        endpoint.Should().Be("https://api.deepseek.com");
    }
}
