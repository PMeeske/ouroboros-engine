using System.Net;
using FluentAssertions;

namespace Ouroboros.Tests.Tapo;

[Trait("Category", "Unit")]
public class TapoColorLightBulbOperationsTests : IDisposable
{
    private readonly Mock<HttpMessageHandler> _mockHandler;
    private readonly HttpClient _httpClient;
    private readonly TapoRestClient _client;

    public TapoColorLightBulbOperationsTests()
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
    public async Task TurnOnAsync_WithEmptyDeviceName_ShouldReturnFailure()
    {
        // Act
        var result = await _client.ColorLightBulbs.TurnOnAsync("");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task TurnOnAsync_WithValidDevice_ShouldCallL530Endpoint()
    {
        // Arrange
        SetupSuccessResponse();

        // Act
        var result = await _client.ColorLightBulbs.TurnOnAsync("color-bulb");

        // Assert
        result.IsSuccess.Should().BeTrue();
        VerifyRequestContains("/actions/l530/on");
    }

    [Fact]
    public async Task TurnOffAsync_WithValidDevice_ShouldCallL530Endpoint()
    {
        // Arrange
        SetupSuccessResponse();

        // Act
        var result = await _client.ColorLightBulbs.TurnOffAsync("color-bulb");

        // Assert
        result.IsSuccess.Should().BeTrue();
        VerifyRequestContains("/actions/l530/off");
    }

    [Fact]
    public async Task SetBrightnessAsync_WithEmptyDeviceName_ShouldReturnFailure()
    {
        // Act
        var result = await _client.ColorLightBulbs.SetBrightnessAsync("", 50);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SetBrightnessAsync_WithLevelAbove100_ShouldReturnFailure()
    {
        // Act
        var result = await _client.ColorLightBulbs.SetBrightnessAsync("bulb1", 101);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Brightness level must be between 0 and 100");
    }

    [Fact]
    public async Task SetBrightnessAsync_WithValidLevel_ShouldSucceed()
    {
        // Arrange
        SetupSuccessResponse();

        // Act
        var result = await _client.ColorLightBulbs.SetBrightnessAsync("bulb1", 50);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task SetColorAsync_WithEmptyDeviceName_ShouldReturnFailure()
    {
        // Act
        var color = new Color { Red = 255, Green = 0, Blue = 0 };
        var result = await _client.ColorLightBulbs.SetColorAsync("", color);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SetColorAsync_WithValidColor_ShouldCallCorrectEndpoint()
    {
        // Arrange
        SetupSuccessResponse();
        var color = new Color { Red = 255, Green = 128, Blue = 0 };

        // Act
        var result = await _client.ColorLightBulbs.SetColorAsync("bulb1", color);

        // Assert
        result.IsSuccess.Should().BeTrue();
        VerifyRequestContains("/actions/l530/set-color");
    }

    [Fact]
    public async Task SetHueSaturationAsync_WithEmptyDeviceName_ShouldReturnFailure()
    {
        // Act
        var result = await _client.ColorLightBulbs.SetHueSaturationAsync("", 180, 50);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SetHueSaturationAsync_WithHueAbove360_ShouldReturnFailure()
    {
        // Act
        var result = await _client.ColorLightBulbs.SetHueSaturationAsync("bulb1", 361, 50);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Hue must be between 0 and 360");
    }

    [Fact]
    public async Task SetHueSaturationAsync_WithSaturationAbove100_ShouldReturnFailure()
    {
        // Act
        var result = await _client.ColorLightBulbs.SetHueSaturationAsync("bulb1", 180, 101);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Saturation must be between 0 and 100");
    }

    [Fact]
    public async Task SetHueSaturationAsync_WithValidValues_ShouldSucceed()
    {
        // Arrange
        SetupSuccessResponse();

        // Act
        var result = await _client.ColorLightBulbs.SetHueSaturationAsync("bulb1", 180, 50);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task SetColorTemperatureAsync_WithEmptyDeviceName_ShouldReturnFailure()
    {
        // Act
        var result = await _client.ColorLightBulbs.SetColorTemperatureAsync("", 4000);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SetColorTemperatureAsync_WithValidTemperature_ShouldSucceed()
    {
        // Arrange
        SetupSuccessResponse();

        // Act
        var result = await _client.ColorLightBulbs.SetColorTemperatureAsync("bulb1", 4000);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GetDeviceInfoAsync_WithEmptyDeviceName_ShouldReturnFailure()
    {
        // Act
        var result = await _client.ColorLightBulbs.GetDeviceInfoAsync("");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task GetDeviceUsageAsync_WithEmptyDeviceName_ShouldReturnFailure()
    {
        // Act
        var result = await _client.ColorLightBulbs.GetDeviceUsageAsync("");

        // Assert
        result.IsFailure.Should().BeTrue();
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
