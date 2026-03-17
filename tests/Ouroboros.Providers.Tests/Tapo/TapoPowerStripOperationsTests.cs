using System.Net;
using FluentAssertions;

namespace Ouroboros.Tests.Tapo;

[Trait("Category", "Unit")]
public class TapoPowerStripOperationsTests : IDisposable
{
    private readonly Mock<HttpMessageHandler> _mockHandler;
    private readonly HttpClient _httpClient;
    private readonly TapoRestClient _client;

    public TapoPowerStripOperationsTests()
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
    public async Task GetDeviceInfoAsync_WithEmptyDeviceName_ShouldReturnFailure()
    {
        // Act
        var result = await _client.PowerStrips.GetDeviceInfoAsync("");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Device name is required");
    }

    [Fact]
    public async Task GetDeviceInfoAsync_WithValidDevice_ShouldCallP300Endpoint()
    {
        // Arrange
        SetupJsonResponse("""{"children":[]}""");

        // Act
        var result = await _client.PowerStrips.GetDeviceInfoAsync("power-strip1");

        // Assert
        result.IsSuccess.Should().BeTrue();
        VerifyRequestContains("/actions/p300/get-device-info");
    }

    [Fact]
    public async Task GetChildDeviceListAsync_WithEmptyDeviceName_ShouldReturnFailure()
    {
        // Act
        var result = await _client.PowerStrips.GetChildDeviceListAsync("");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task GetChildDeviceListAsync_WithValidDevice_ShouldCallCorrectEndpoint()
    {
        // Arrange
        SetupJsonResponse("""{"child_device_list":[{"id":"child1"}]}""");

        // Act
        var result = await _client.PowerStrips.GetChildDeviceListAsync("power-strip1");

        // Assert
        result.IsSuccess.Should().BeTrue();
        VerifyRequestContains("/actions/p300/get-child-device-list");
    }

    [Fact]
    public async Task GetDeviceInfoAsync_WhenServerError_ShouldReturnFailure()
    {
        // Arrange
        _mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Content = new StringContent("Server error")
            });

        // Act
        var result = await _client.PowerStrips.GetDeviceInfoAsync("strip1");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetChildDeviceListAsync_WithNullOrWhitespace_ShouldReturnFailure(string? deviceName)
    {
        // Act
        var result = await _client.PowerStrips.GetChildDeviceListAsync(deviceName!);

        // Assert
        result.IsFailure.Should().BeTrue();
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
