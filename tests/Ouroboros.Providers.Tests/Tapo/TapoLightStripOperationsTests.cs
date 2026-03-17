using System.Net;
using FluentAssertions;

namespace Ouroboros.Tests.Tapo;

[Trait("Category", "Unit")]
public class TapoLightStripOperationsTests : IDisposable
{
    private readonly Mock<HttpMessageHandler> _mockHandler;
    private readonly HttpClient _httpClient;
    private readonly TapoRestClient _client;

    public TapoLightStripOperationsTests()
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
    public async Task TurnOnAsync_WithValidDevice_ShouldCallL900Endpoint()
    {
        // Arrange
        SetupSuccessResponse();

        // Act
        var result = await _client.LightStrips.TurnOnAsync("strip1");

        // Assert
        result.IsSuccess.Should().BeTrue();
        VerifyRequestContains("/actions/l900/on");
    }

    [Fact]
    public async Task TurnOffAsync_WithValidDevice_ShouldCallL900Endpoint()
    {
        // Arrange
        SetupSuccessResponse();

        // Act
        var result = await _client.LightStrips.TurnOffAsync("strip1");

        // Assert
        result.IsSuccess.Should().BeTrue();
        VerifyRequestContains("/actions/l900/off");
    }

    [Fact]
    public async Task SetBrightnessAsync_WithLevelAbove100_ShouldReturnFailure()
    {
        // Act
        var result = await _client.LightStrips.SetBrightnessAsync("strip1", 101);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Brightness level must be between 0 and 100");
    }

    [Fact]
    public async Task SetColorAsync_WithValidColor_ShouldCallCorrectEndpoint()
    {
        // Arrange
        SetupSuccessResponse();
        var color = new Color { Red = 0, Green = 255, Blue = 0 };

        // Act
        var result = await _client.LightStrips.SetColorAsync("strip1", color);

        // Assert
        result.IsSuccess.Should().BeTrue();
        VerifyRequestContains("/actions/l900/set-color");
    }

    [Fact]
    public async Task SetHueSaturationAsync_WithHueAbove360_ShouldReturnFailure()
    {
        // Act
        var result = await _client.LightStrips.SetHueSaturationAsync("strip1", 361, 50);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Hue must be between 0 and 360");
    }

    [Fact]
    public async Task SetHueSaturationAsync_WithSaturationAbove100_ShouldReturnFailure()
    {
        // Act
        var result = await _client.LightStrips.SetHueSaturationAsync("strip1", 180, 101);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Saturation must be between 0 and 100");
    }

    [Fact]
    public async Task SetColorTemperatureAsync_WithValidTemp_ShouldCallCorrectEndpoint()
    {
        // Arrange
        SetupSuccessResponse();

        // Act
        var result = await _client.LightStrips.SetColorTemperatureAsync("strip1", 5000);

        // Assert
        result.IsSuccess.Should().BeTrue();
        VerifyRequestContains("/actions/l900/set-color-temperature");
    }

    [Fact]
    public async Task GetDeviceInfoAsync_WithValidDevice_ShouldCallCorrectEndpoint()
    {
        // Arrange
        SetupJsonResponse("""{"status":"on"}""");

        // Act
        var result = await _client.LightStrips.GetDeviceInfoAsync("strip1");

        // Assert
        result.IsSuccess.Should().BeTrue();
        VerifyRequestContains("/actions/l900/get-device-info");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task AllMethods_WithNullOrEmptyDeviceName_ShouldReturnFailure(string? deviceName)
    {
        // Act & Assert
        (await _client.LightStrips.TurnOnAsync(deviceName!)).IsFailure.Should().BeTrue();
        (await _client.LightStrips.TurnOffAsync(deviceName!)).IsFailure.Should().BeTrue();
        (await _client.LightStrips.SetBrightnessAsync(deviceName!, 50)).IsFailure.Should().BeTrue();
        (await _client.LightStrips.SetColorAsync(deviceName!, new Color { Red = 0, Green = 0, Blue = 0 })).IsFailure.Should().BeTrue();
        (await _client.LightStrips.SetHueSaturationAsync(deviceName!, 180, 50)).IsFailure.Should().BeTrue();
        (await _client.LightStrips.SetColorTemperatureAsync(deviceName!, 4000)).IsFailure.Should().BeTrue();
        (await _client.LightStrips.GetDeviceInfoAsync(deviceName!)).IsFailure.Should().BeTrue();
        (await _client.LightStrips.GetDeviceUsageAsync(deviceName!)).IsFailure.Should().BeTrue();
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
