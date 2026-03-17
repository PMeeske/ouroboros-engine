using System.Net;
using FluentAssertions;

namespace Ouroboros.Tests.Tapo;

[Trait("Category", "Unit")]
public class TapoRgbicLightStripOperationsTests : IDisposable
{
    private readonly Mock<HttpMessageHandler> _mockHandler;
    private readonly HttpClient _httpClient;
    private readonly TapoRestClient _client;

    public TapoRgbicLightStripOperationsTests()
    {
        _mockHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:8123")
        };
        _client = new TapoRestClient(_httpClient);
    }

    public void Dispose()
    {
        _client.Dispose();
        _httpClient.Dispose();
    }

    [Fact]
    public async Task TurnOnAsync_WithValidDevice_ShouldCallL920Endpoint()
    {
        // Arrange
        SetupSuccessResponse();

        // Act
        var result = await _client.RgbicLightStrips.TurnOnAsync("rgbic-strip");

        // Assert
        result.IsSuccess.Should().BeTrue();
        VerifyRequestContains("/actions/l920/on");
    }

    [Fact]
    public async Task TurnOffAsync_WithValidDevice_ShouldCallL920Endpoint()
    {
        // Arrange
        SetupSuccessResponse();

        // Act
        var result = await _client.RgbicLightStrips.TurnOffAsync("rgbic-strip");

        // Assert
        result.IsSuccess.Should().BeTrue();
        VerifyRequestContains("/actions/l920/off");
    }

    [Fact]
    public async Task SetBrightnessAsync_WithLevelAbove100_ShouldReturnFailure()
    {
        // Act
        var result = await _client.RgbicLightStrips.SetBrightnessAsync("strip1", 101);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SetColorAsync_WithValidColor_ShouldCallL920Endpoint()
    {
        // Arrange
        SetupSuccessResponse();

        // Act
        var result = await _client.RgbicLightStrips.SetColorAsync(
            "strip1", new Color { Red = 255, Green = 0, Blue = 128 });

        // Assert
        result.IsSuccess.Should().BeTrue();
        VerifyRequestContains("/actions/l920/set-color");
    }

    [Fact]
    public async Task SetHueSaturationAsync_WithHueAbove360_ShouldReturnFailure()
    {
        // Act
        var result = await _client.RgbicLightStrips.SetHueSaturationAsync("strip1", 361, 50);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SetHueSaturationAsync_WithSaturationAbove100_ShouldReturnFailure()
    {
        // Act
        var result = await _client.RgbicLightStrips.SetHueSaturationAsync("strip1", 180, 101);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SetColorTemperatureAsync_WithValidDevice_ShouldCallL920Endpoint()
    {
        // Arrange
        SetupSuccessResponse();

        // Act
        var result = await _client.RgbicLightStrips.SetColorTemperatureAsync("strip1", 6500);

        // Assert
        result.IsSuccess.Should().BeTrue();
        VerifyRequestContains("/actions/l920/set-color-temperature");
    }

    [Fact]
    public async Task SetLightingEffectAsync_WithEmptyDeviceName_ShouldReturnFailure()
    {
        // Act
        var result = await _client.RgbicLightStrips.SetLightingEffectAsync(
            "", LightingEffectPreset.Rainbow);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SetLightingEffectAsync_WithValidPreset_ShouldCallCorrectEndpoint()
    {
        // Arrange
        SetupSuccessResponse();

        // Act
        var result = await _client.RgbicLightStrips.SetLightingEffectAsync(
            "strip1", LightingEffectPreset.Aurora);

        // Assert
        result.IsSuccess.Should().BeTrue();
        VerifyRequestContains("/actions/l920/set-lighting-effect");
    }

    [Theory]
    [InlineData(LightingEffectPreset.Aurora)]
    [InlineData(LightingEffectPreset.Rainbow)]
    [InlineData(LightingEffectPreset.Christmas)]
    [InlineData(LightingEffectPreset.Ocean)]
    public async Task SetLightingEffectAsync_WithVariousPresets_ShouldSucceed(LightingEffectPreset preset)
    {
        // Arrange
        SetupSuccessResponse();

        // Act
        var result = await _client.RgbicLightStrips.SetLightingEffectAsync("strip1", preset);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GetDeviceInfoAsync_WithValidDevice_ShouldCallL920Endpoint()
    {
        // Arrange
        SetupJsonResponse("""{"status":"on"}""");

        // Act
        var result = await _client.RgbicLightStrips.GetDeviceInfoAsync("strip1");

        // Assert
        result.IsSuccess.Should().BeTrue();
        VerifyRequestContains("/actions/l920/get-device-info");
    }

    [Fact]
    public async Task GetDeviceUsageAsync_WithValidDevice_ShouldCallL920Endpoint()
    {
        // Arrange
        SetupJsonResponse("""{"usage_hours":120}""");

        // Act
        var result = await _client.RgbicLightStrips.GetDeviceUsageAsync("strip1");

        // Assert
        result.IsSuccess.Should().BeTrue();
        VerifyRequestContains("/actions/l920/get-device-usage");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task AllMethods_WithNullOrEmptyDeviceName_ShouldReturnFailure(string? deviceName)
    {
        // Act & Assert
        (await _client.RgbicLightStrips.TurnOnAsync(deviceName!)).IsFailure.Should().BeTrue();
        (await _client.RgbicLightStrips.TurnOffAsync(deviceName!)).IsFailure.Should().BeTrue();
        (await _client.RgbicLightStrips.SetBrightnessAsync(deviceName!, 50)).IsFailure.Should().BeTrue();
        (await _client.RgbicLightStrips.SetLightingEffectAsync(deviceName!, LightingEffectPreset.Rainbow)).IsFailure.Should().BeTrue();
        (await _client.RgbicLightStrips.GetDeviceInfoAsync(deviceName!)).IsFailure.Should().BeTrue();
        (await _client.RgbicLightStrips.GetDeviceUsageAsync(deviceName!)).IsFailure.Should().BeTrue();
    }

    private void SetupSuccessResponse()
    {
        _mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("")
            });
    }

    private void SetupJsonResponse(string json)
    {
        _mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            });
    }

    private void VerifyRequestContains(string urlPart)
    {
        _mockHandler
            .Protected()
            .Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(r =>
                    r.RequestUri != null && r.RequestUri.ToString().Contains(urlPart)),
                ItExpr.IsAny<CancellationToken>());
    }
}
