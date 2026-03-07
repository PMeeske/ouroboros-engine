namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class OllamaVisionModelTests
{
    [Fact]
    public void DefaultModel_IsQwen3Vl()
    {
        OllamaVisionModel.DefaultModel.Should().Be("devstral-small-2:24b-cloud");
    }

    [Fact]
    public void LightweightModel_IsLlava7b()
    {
        OllamaVisionModel.LightweightModel.Should().Be("llava:7b");
    }

    [Fact]
    public void Ctor_Defaults_SetsCorrectProperties()
    {
        var model = new OllamaVisionModel();

        model.ModelName.Should().Be(OllamaVisionModel.DefaultModel);
        model.SupportsStreaming.Should().BeFalse();
    }

    [Fact]
    public void Ctor_CustomModel_SetsModelName()
    {
        var model = new OllamaVisionModel(model: "llava:7b");

        model.ModelName.Should().Be("llava:7b");
    }

    [Fact]
    public void Ctor_CustomEndpoint_DoesNotThrow()
    {
        var model = new OllamaVisionModel(endpoint: "http://custom-host:11434");

        model.Should().NotBeNull();
    }

    [Fact]
    public void Ctor_WithTimeout_DoesNotThrow()
    {
        var model = new OllamaVisionModel(timeout: TimeSpan.FromSeconds(60));

        model.Should().NotBeNull();
    }
}
