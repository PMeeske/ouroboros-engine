namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class ProviderConfigTests
{
    [Fact]
    public void Ctor_RequiredOnly_SetsDefaults()
    {
        var config = new ProviderConfig("openai", ChatEndpointType.OpenAI);

        config.Name.Should().Be("openai");
        config.EndpointType.Should().Be(ChatEndpointType.OpenAI);
        config.Endpoint.Should().BeNull();
        config.ApiKey.Should().BeNull();
        config.Model.Should().BeNull();
        config.Weight.Should().Be(1);
        config.Enabled.Should().BeTrue();
    }

    [Fact]
    public void Ctor_AllProperties_ArePreserved()
    {
        var config = new ProviderConfig(
            Name: "custom",
            EndpointType: ChatEndpointType.OllamaLocal,
            Endpoint: "http://localhost:11434",
            ApiKey: "key-123",
            Model: "llama3",
            Weight: 3,
            Enabled: false);

        config.Name.Should().Be("custom");
        config.EndpointType.Should().Be(ChatEndpointType.OllamaLocal);
        config.Endpoint.Should().Be("http://localhost:11434");
        config.ApiKey.Should().Be("key-123");
        config.Model.Should().Be("llama3");
        config.Weight.Should().Be(3);
        config.Enabled.Should().BeFalse();
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var c1 = new ProviderConfig("a", ChatEndpointType.OpenAI, Weight: 2);
        var c2 = new ProviderConfig("a", ChatEndpointType.OpenAI, Weight: 2);

        c1.Should().Be(c2);
    }
}
