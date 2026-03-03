using FluentAssertions;
using Ouroboros.Providers;

namespace Ouroboros.Tests.Providers;

[Trait("Category", "Unit")]
public sealed class ProviderConfigTests2
{
    [Fact]
    public void Constructor_RequiredProperties()
    {
        var config = new ProviderConfig("MyProvider", ChatEndpointType.Anthropic);

        config.Name.Should().Be("MyProvider");
        config.EndpointType.Should().Be(ChatEndpointType.Anthropic);
    }

    [Fact]
    public void Constructor_DefaultValues()
    {
        var config = new ProviderConfig("Test", ChatEndpointType.OllamaLocal);

        config.Endpoint.Should().BeNull();
        config.ApiKey.Should().BeNull();
        config.Model.Should().BeNull();
        config.Weight.Should().Be(1);
        config.Enabled.Should().BeTrue();
    }

    [Fact]
    public void Constructor_CustomValues()
    {
        var config = new ProviderConfig(
            "Custom",
            ChatEndpointType.DeepSeek,
            Endpoint: "http://custom",
            ApiKey: "key123",
            Model: "ds-v3",
            Weight: 5,
            Enabled: false);

        config.Endpoint.Should().Be("http://custom");
        config.ApiKey.Should().Be("key123");
        config.Model.Should().Be("ds-v3");
        config.Weight.Should().Be(5);
        config.Enabled.Should().BeFalse();
    }

    [Fact]
    public void RecordWith_CreatesModifiedCopy()
    {
        var original = new ProviderConfig("Test", ChatEndpointType.Anthropic);
        var modified = original with { Enabled = false };

        modified.Enabled.Should().BeFalse();
        original.Enabled.Should().BeTrue();
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var a = new ProviderConfig("Test", ChatEndpointType.OllamaLocal);
        var b = new ProviderConfig("Test", ChatEndpointType.OllamaLocal);
        a.Should().Be(b);
    }
}
