using FluentAssertions;

namespace Ouroboros.Tests.Tapo;

[Trait("Category", "Unit")]
public class TapoVisionModelConfigTests
{
    [Fact]
    public void TapoVisionModelConfig_DefaultConstants_ShouldBeSet()
    {
        // Act & Assert
        TapoVisionModelConfig.DefaultVisionModel.Should().Be("devstral-small-2:24b-cloud");
        TapoVisionModelConfig.LightweightVisionModel.Should().Be("llava:7b");
        TapoVisionModelConfig.HighQualityVisionModel.Should().Be("devstral-small-2:24b-cloud");
    }

    [Fact]
    public void TapoVisionModelConfig_DefaultConstructor_ShouldSetDefaults()
    {
        // Arrange & Act
        var config = new TapoVisionModelConfig();

        // Assert
        config.VisionModel.Should().Be(TapoVisionModelConfig.DefaultVisionModel);
        config.RequestTimeout.Should().Be(TimeSpan.FromSeconds(120));
        config.EnableObjectDetection.Should().BeTrue();
        config.EnableFaceDetection.Should().BeTrue();
        config.EnableSceneClassification.Should().BeTrue();
        config.MaxObjectsPerFrame.Should().Be(20);
        config.ConfidenceThreshold.Should().Be(0.5);
    }

    [Fact]
    public void TapoVisionModelConfig_CreateDefault_ShouldReturnDefaultValues()
    {
        // Act
        var config = TapoVisionModelConfig.CreateDefault();

        // Assert
        config.VisionModel.Should().Be(TapoVisionModelConfig.DefaultVisionModel);
        config.RequestTimeout.Should().Be(TimeSpan.FromSeconds(120));
        config.MaxObjectsPerFrame.Should().Be(20);
        config.ConfidenceThreshold.Should().Be(0.5);
    }

    [Fact]
    public void TapoVisionModelConfig_CreateLightweight_ShouldUseLightweightSettings()
    {
        // Act
        var config = TapoVisionModelConfig.CreateLightweight();

        // Assert
        config.VisionModel.Should().Be(TapoVisionModelConfig.LightweightVisionModel);
        config.RequestTimeout.Should().Be(TimeSpan.FromSeconds(60));
        config.MaxObjectsPerFrame.Should().Be(10);
    }

    [Fact]
    public void TapoVisionModelConfig_CreateHighQuality_ShouldUseHighQualitySettings()
    {
        // Act
        var config = TapoVisionModelConfig.CreateHighQuality();

        // Assert
        config.VisionModel.Should().Be(TapoVisionModelConfig.HighQualityVisionModel);
        config.RequestTimeout.Should().Be(TimeSpan.FromSeconds(180));
        config.MaxObjectsPerFrame.Should().Be(50);
        config.ConfidenceThreshold.Should().Be(0.3);
    }

    [Fact]
    public void TapoVisionModelConfig_With_ShouldCreateModifiedCopy()
    {
        // Arrange
        var original = TapoVisionModelConfig.CreateDefault();

        // Act
        var modified = original with
        {
            VisionModel = "custom-model:v1",
            MaxObjectsPerFrame = 100
        };

        // Assert
        modified.VisionModel.Should().Be("custom-model:v1");
        modified.MaxObjectsPerFrame.Should().Be(100);
        original.VisionModel.Should().Be(TapoVisionModelConfig.DefaultVisionModel);
    }

    [Fact]
    public void TapoVisionModelConfig_EnableFlags_ShouldBeConfigurable()
    {
        // Arrange & Act
        var config = new TapoVisionModelConfig
        {
            EnableObjectDetection = false,
            EnableFaceDetection = false,
            EnableSceneClassification = false
        };

        // Assert
        config.EnableObjectDetection.Should().BeFalse();
        config.EnableFaceDetection.Should().BeFalse();
        config.EnableSceneClassification.Should().BeFalse();
    }

    [Fact]
    public void TapoVisionModelConfig_Equality_SameValues_ShouldBeEqual()
    {
        // Arrange
        var a = TapoVisionModelConfig.CreateDefault();
        var b = TapoVisionModelConfig.CreateDefault();

        // Act & Assert
        a.Should().Be(b);
    }

    [Fact]
    public void TapoVisionModelConfig_Equality_DifferentValues_ShouldNotBeEqual()
    {
        // Arrange
        var a = TapoVisionModelConfig.CreateDefault();
        var b = TapoVisionModelConfig.CreateLightweight();

        // Act & Assert
        a.Should().NotBe(b);
    }
}
