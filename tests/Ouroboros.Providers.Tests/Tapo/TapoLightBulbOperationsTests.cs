using System.Net;
using FluentAssertions;

namespace Ouroboros.Tests.Tapo;

[Trait("Category", "Unit")]
public class TapoLightBulbOperationsTests : IDisposable
{
    private readonly Mock<HttpMessageHandler> _mockHandler;
    private readonly HttpClient _httpClient;
    private readonly TapoRestClient _client;

    public TapoLightBulbOperationsTests()
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
        var result = await _client.LightBulbs.TurnOnAsync("");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Device name is required");
    }

    [Fact]
    public async Task TurnOnAsync_WithWhitespace_ShouldReturnFailure()
    {
        // Act
        var result = await _client.LightBulbs.TurnOnAsync("   ");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task TurnOnAsync_WithValidDevice_ShouldCallCorrectEndpoint()
    {
        // Arrange
        SetupSuccessResponse();

        // Act
        var result = await _client.LightBulbs.TurnOnAsync("living-room");

        // Assert
        result.IsSuccess.Should().BeTrue();
        VerifyRequestContains("/actions/l510/on");
    }

    [Fact]
    public async Task TurnOffAsync_WithEmptyDeviceName_ShouldReturnFailure()
    {
        // Act
        var result = await _client.LightBulbs.TurnOffAsync("");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task TurnOffAsync_WithValidDevice_ShouldCallCorrectEndpoint()
    {
        // Arrange
        SetupSuccessResponse();

        // Act
        var result = await _client.LightBulbs.TurnOffAsync("bedroom");

        // Assert
        result.IsSuccess.Should().BeTrue();
        VerifyRequestContains("/actions/l510/off");
    }

    [Fact]
    public async Task SetBrightnessAsync_WithEmptyDeviceName_ShouldReturnFailure()
    {
        // Act
        var result = await _client.LightBulbs.SetBrightnessAsync("", 50);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SetBrightnessAsync_WithLevelAbove100_ShouldReturnFailure()
    {
        // Act
        var result = await _client.LightBulbs.SetBrightnessAsync("bulb1", 101);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Brightness level must be between 0 and 100");
    }

    [Fact]
    public async Task SetBrightnessAsync_WithValidLevel_ShouldCallCorrectEndpoint()
    {
        // Arrange
        SetupSuccessResponse();

        // Act
        var result = await _client.LightBulbs.SetBrightnessAsync("bulb1", 75);

        // Assert
        result.IsSuccess.Should().BeTrue();
        VerifyRequestContains("/actions/l510/set-brightness");
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)50)]
    [InlineData((byte)100)]
    public async Task SetBrightnessAsync_WithBoundaryValues_ShouldSucceed(byte level)
    {
        // Arrange
        SetupSuccessResponse();

        // Act
        var result = await _client.LightBulbs.SetBrightnessAsync("bulb1", level);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GetDeviceInfoAsync_WithEmptyDeviceName_ShouldReturnFailure()
    {
        // Act
        var result = await _client.LightBulbs.GetDeviceInfoAsync("");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task GetDeviceInfoAsync_WithValidDevice_ShouldReturnJsonDocument()
    {
        // Arrange
        SetupJsonResponse("""{"status":"on","brightness":80}""");

        // Act
        var result = await _client.LightBulbs.GetDeviceInfoAsync("bulb1");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetDeviceUsageAsync_WithEmptyDeviceName_ShouldReturnFailure()
    {
        // Act
        var result = await _client.LightBulbs.GetDeviceUsageAsync("");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task TurnOnAsync_WhenServerReturnsError_ShouldReturnFailure()
    {
        // Arrange
        SetupErrorResponse(HttpStatusCode.InternalServerError, "Server error");

        // Act
        var result = await _client.LightBulbs.TurnOnAsync("bulb1");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Action failed");
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

    private void SetupErrorResponse(HttpStatusCode statusCode, string error)
    {
        _mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(error)
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
