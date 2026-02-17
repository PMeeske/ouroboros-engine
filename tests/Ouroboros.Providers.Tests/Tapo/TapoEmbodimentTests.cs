// <copyright file="TapoEmbodimentTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Providers.Tapo;

using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Ouroboros.Core.EmbodiedInteraction;
using Ouroboros.Providers.Tapo;
using Xunit;

/// <summary>
/// Tests for the TapoEmbodiment class.
/// </summary>
[Trait("Category", "Unit")]
public class TapoEmbodimentTests
{
    private readonly Mock<ILogger<TapoEmbodiment>> _mockLogger;
    private readonly JsonSerializerOptions _jsonOptions;

    public TapoEmbodimentTests()
    {
        _mockLogger = new Mock<ILogger<TapoEmbodiment>>();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_Succeeds()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var virtualSelf = new VirtualSelf("test-agent");

        // Act
        using var embodiment = new TapoEmbodiment(tapoClient, virtualSelf);

        // Assert
        embodiment.Should().NotBeNull();
        embodiment.IsCapturing.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithNullTapoClient_ThrowsArgumentNullException()
    {
        // Arrange
        using var virtualSelf = new VirtualSelf("test-agent");

        // Act
        Action act = () => new TapoEmbodiment(null!, virtualSelf);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("tapoClient");
    }

    [Fact]
    public void Constructor_WithNullVirtualSelf_ThrowsArgumentNullException()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);

        // Act
        Action act = () => new TapoEmbodiment(tapoClient, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("virtualSelf");
    }

    #endregion

    #region ConfigureCamera Tests

    [Fact]
    public void ConfigureCamera_WithValidConfig_Succeeds()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var virtualSelf = new VirtualSelf("test-agent");
        using var embodiment = new TapoEmbodiment(tapoClient, virtualSelf);

        var config = new TapoCameraConfig("test-camera");

        // Act
        var result = embodiment.ConfigureCamera(config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        embodiment.CameraConfig.Should().Be(config);
    }

    [Fact]
    public void ConfigureCamera_WithNullConfig_ReturnsFailure()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var virtualSelf = new VirtualSelf("test-agent");
        using var embodiment = new TapoEmbodiment(tapoClient, virtualSelf);

        // Act
        var result = embodiment.ConfigureCamera(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("required");
    }

    #endregion

    #region ConfigureVoiceOutput Tests

    [Fact]
    public void ConfigureVoiceOutput_WithValidConfig_Succeeds()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var virtualSelf = new VirtualSelf("test-agent");
        using var embodiment = new TapoEmbodiment(tapoClient, virtualSelf);

        var config = new TapoVoiceOutputConfig("test-speaker");

        // Act
        var result = embodiment.ConfigureVoiceOutput(config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        embodiment.VoiceConfig.Should().Be(config);
    }

    [Fact]
    public void ConfigureVoiceOutput_WithNullConfig_ReturnsFailure()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var virtualSelf = new VirtualSelf("test-agent");
        using var embodiment = new TapoEmbodiment(tapoClient, virtualSelf);

        // Act
        var result = embodiment.ConfigureVoiceOutput(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("required");
    }

    #endregion

    #region StartCaptureAsync Tests

    [Fact]
    public async Task StartCaptureAsync_WithoutCameraConfig_ReturnsFailure()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var virtualSelf = new VirtualSelf("test-agent");
        using var embodiment = new TapoEmbodiment(tapoClient, virtualSelf);

        // Act
        var result = await embodiment.StartCaptureAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Camera not configured");
    }

    [Fact]
    public async Task StartCaptureAsync_WithCameraConfigButNotAuthenticated_ReturnsFailure()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var virtualSelf = new VirtualSelf("test-agent");
        using var embodiment = new TapoEmbodiment(tapoClient, virtualSelf);

        var config = new TapoCameraConfig("test-camera");
        embodiment.ConfigureCamera(config);

        // Act
        var result = await embodiment.StartCaptureAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
        // Should fail because not authenticated with Tapo REST API
    }

    #endregion

    #region StopCaptureAsync Tests

    [Fact]
    public async Task StopCaptureAsync_WhenNotCapturing_ReturnsSuccess()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var virtualSelf = new VirtualSelf("test-agent");
        using var embodiment = new TapoEmbodiment(tapoClient, virtualSelf);

        // Act
        var result = await embodiment.StopCaptureAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region ProcessVideoFrameAsync Tests

    [Fact]
    public async Task ProcessVideoFrameAsync_WhenNotCapturing_ReturnsFailure()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var virtualSelf = new VirtualSelf("test-agent");
        using var embodiment = new TapoEmbodiment(tapoClient, virtualSelf);

        var frameData = new byte[100];

        // Act
        var result = await embodiment.ProcessVideoFrameAsync(frameData, 640, 480, 1);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Not capturing");
    }

    #endregion

    #region SpeakAsync Tests

    [Fact]
    public async Task SpeakAsync_WithoutVoiceConfig_ReturnsFailure()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var virtualSelf = new VirtualSelf("test-agent");
        using var embodiment = new TapoEmbodiment(tapoClient, virtualSelf);

        // Act
        var result = await embodiment.SpeakAsync("Hello, world!");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Voice output not configured");
    }

    [Fact]
    public async Task SpeakAsync_WithoutTtsModel_ReturnsFailure()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var virtualSelf = new VirtualSelf("test-agent");
        using var embodiment = new TapoEmbodiment(tapoClient, virtualSelf);

        var voiceConfig = new TapoVoiceOutputConfig("test-speaker");
        embodiment.ConfigureVoiceOutput(voiceConfig);

        // Act
        var result = await embodiment.SpeakAsync("Hello, world!");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("TTS model not available");
    }

    [Fact]
    public async Task SpeakAsync_WithEmptyText_ReturnsFailure()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var virtualSelf = new VirtualSelf("test-agent");
        var mockTtsModel = new MockTtsModel();
        using var embodiment = new TapoEmbodiment(tapoClient, virtualSelf, ttsModel: mockTtsModel);

        var voiceConfig = new TapoVoiceOutputConfig("test-speaker");
        embodiment.ConfigureVoiceOutput(voiceConfig);

        // Act
        var result = await embodiment.SpeakAsync("");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty");
    }

    #endregion

    #region CreateBodySchema Tests

    [Fact]
    public void CreateBodySchema_WithoutConfig_ReturnsBasicSchema()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var virtualSelf = new VirtualSelf("test-agent");
        using var embodiment = new TapoEmbodiment(tapoClient, virtualSelf);

        // Act
        var schema = embodiment.CreateBodySchema();

        // Assert
        schema.Should().NotBeNull();
        schema.HasCapability(Capability.Reasoning).Should().BeTrue();
        schema.HasCapability(Capability.Remembering).Should().BeTrue();
    }

    [Fact]
    public void CreateBodySchema_WithCameraConfig_IncludesVisualSensor()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var virtualSelf = new VirtualSelf("test-agent");
        using var embodiment = new TapoEmbodiment(tapoClient, virtualSelf);

        var cameraConfig = new TapoCameraConfig("living-room-camera", EnableAudio: true);
        embodiment.ConfigureCamera(cameraConfig);

        // Act
        var schema = embodiment.CreateBodySchema();

        // Assert
        schema.Should().NotBeNull();
        schema.Sensors.Should().ContainKey("tapo-camera-living-room-camera");
        schema.Sensors.Should().ContainKey("tapo-mic-living-room-camera");
        schema.HasCapability(Capability.Seeing).Should().BeTrue();
        schema.HasCapability(Capability.Hearing).Should().BeTrue();
    }

    [Fact]
    public void CreateBodySchema_WithVoiceConfig_IncludesVoiceActuator()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var virtualSelf = new VirtualSelf("test-agent");
        var mockTtsModel = new MockTtsModel();
        using var embodiment = new TapoEmbodiment(tapoClient, virtualSelf, ttsModel: mockTtsModel);

        var voiceConfig = new TapoVoiceOutputConfig("living-room-speaker");
        embodiment.ConfigureVoiceOutput(voiceConfig);

        // Act
        var schema = embodiment.CreateBodySchema();

        // Assert
        schema.Should().NotBeNull();
        schema.Actuators.Should().ContainKey("tapo-speaker-living-room-speaker");
        schema.HasCapability(Capability.Speaking).Should().BeTrue();
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_DisposesResources()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var virtualSelf = new VirtualSelf("test-agent");
        var embodiment = new TapoEmbodiment(tapoClient, virtualSelf);

        // Act
        embodiment.Dispose();

        // Assert - disposed embodiment should return failures
        var configResult = embodiment.ConfigureCamera(new TapoCameraConfig("test"));
        configResult.IsFailure.Should().BeTrue();
        configResult.Error.Should().Contain("disposed");
    }

    #endregion

    #region Observable Tests

    [Fact]
    public void VideoFrames_Observable_IsAvailable()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var virtualSelf = new VirtualSelf("test-agent");
        using var embodiment = new TapoEmbodiment(tapoClient, virtualSelf);

        // Assert
        embodiment.VideoFrames.Should().NotBeNull();
    }

    [Fact]
    public void AudioChunks_Observable_IsAvailable()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var virtualSelf = new VirtualSelf("test-agent");
        using var embodiment = new TapoEmbodiment(tapoClient, virtualSelf);

        // Assert
        embodiment.AudioChunks.Should().NotBeNull();
    }

    [Fact]
    public void VisionResults_Observable_IsAvailable()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var virtualSelf = new VirtualSelf("test-agent");
        using var embodiment = new TapoEmbodiment(tapoClient, virtualSelf);

        // Assert
        embodiment.VisionResults.Should().NotBeNull();
    }

    #endregion
}