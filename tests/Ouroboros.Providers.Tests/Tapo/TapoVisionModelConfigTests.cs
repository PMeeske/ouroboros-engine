namespace Ouroboros.Tests.Providers.Tapo;

/// <summary>
/// Tests for TapoVisionModelConfig.
/// </summary>
[Trait("Category", "Unit")]
public class TapoVisionModelConfigTests
{
    [Fact]
    public void CreateDefault_ReturnsConfigWithDefaultVisionModel()
    {
        // Act
        var config = TapoVisionModelConfig.CreateDefault();

        // Assert
        config.VisionModel.Should().Be(TapoVisionModelConfig.DefaultVisionModel);
        config.VisionModel.Should().Be("qwen3-vl:235b-cloud");
    }

    [Fact]
    public void CreateLightweight_ReturnsConfigWithLightweightVisionModel()
    {
        // Act
        var config = TapoVisionModelConfig.CreateLightweight();

        // Assert
        config.VisionModel.Should().Be(TapoVisionModelConfig.LightweightVisionModel);
        config.VisionModel.Should().Be("llava:7b");
    }

    [Fact]
    public void CreateHighQuality_ReturnsConfigWithHighQualityVisionModel()
    {
        // Act
        var config = TapoVisionModelConfig.CreateHighQuality();

        // Assert
        config.VisionModel.Should().Be(TapoVisionModelConfig.HighQualityVisionModel);
        config.VisionModel.Should().Be("qwen3-vl:235b-cloud");
    }

    [Fact]
    public void DefaultConfig_HasExpectedDefaults()
    {
        // Act
        var config = TapoVisionModelConfig.CreateDefault();

        // Assert
        config.OllamaEndpoint.Should().Be("http://localhost:11434");
        config.RequestTimeout.Should().Be(TimeSpan.FromSeconds(120));
        config.EnableObjectDetection.Should().BeTrue();
        config.EnableFaceDetection.Should().BeTrue();
        config.EnableSceneClassification.Should().BeTrue();
        config.MaxObjectsPerFrame.Should().Be(20);
        config.ConfidenceThreshold.Should().Be(0.5);
    }
}