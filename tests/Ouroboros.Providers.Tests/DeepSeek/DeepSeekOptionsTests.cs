using DeepSeekOpts = Ouroboros.Providers.DeepSeek.DeepSeekOptions;

namespace Ouroboros.Tests.DeepSeek;

[Trait("Category", "Unit")]
public sealed class DeepSeekOptionsTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var options = new DeepSeekOpts();

        options.UseLocal.Should().BeTrue();
        options.LocalEndpoint.Should().Be("http://localhost:11434");
        options.CloudEndpoint.Should().BeNull();
        options.ApiKey.Should().BeNull();
        options.DefaultModel.Should().BeNull();
        options.ReasoningModel.Should().Be("deepseek-r1:32b");
    }

    [Fact]
    public void EffectiveDefaultModel_WhenLocal_ReturnsLocal8b()
    {
        var options = new DeepSeekOpts(UseLocal: true);
        options.EffectiveDefaultModel.Should().Be("deepseek-r1:8b");
    }

    [Fact]
    public void EffectiveDefaultModel_WhenCloud_ReturnsCloud32b()
    {
        var options = new DeepSeekOpts(UseLocal: false);
        options.EffectiveDefaultModel.Should().Be("deepseek-r1:32b");
    }

    [Fact]
    public void EffectiveDefaultModel_WhenExplicitModel_ReturnsExplicit()
    {
        var options = new DeepSeekOpts(DefaultModel: "custom-model");
        options.EffectiveDefaultModel.Should().Be("custom-model");
    }

    [Fact]
    public void CustomValues_ArePreserved()
    {
        var options = new DeepSeekOpts(
            UseLocal: false,
            LocalEndpoint: "http://custom:11434",
            CloudEndpoint: "https://cloud.example.com",
            ApiKey: "test-key",
            DefaultModel: "my-model",
            ReasoningModel: "my-reasoning");

        options.UseLocal.Should().BeFalse();
        options.LocalEndpoint.Should().Be("http://custom:11434");
        options.CloudEndpoint.Should().Be("https://cloud.example.com");
        options.ApiKey.Should().Be("test-key");
        options.DefaultModel.Should().Be("my-model");
        options.ReasoningModel.Should().Be("my-reasoning");
    }
}
