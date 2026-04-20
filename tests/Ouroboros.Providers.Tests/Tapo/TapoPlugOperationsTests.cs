using System.Net;
using FluentAssertions;

namespace Ouroboros.Tests.Tapo;

[Trait("Category", "Unit")]
public class TapoPlugOperationsTests : IDisposable
{
    private readonly Mock<HttpMessageHandler> _mockHandler;
    private readonly HttpClient _httpClient;
    private readonly TapoRestClient _client;

    public TapoPlugOperationsTests()
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
        var result = await _client.Plugs.TurnOnAsync("");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task TurnOnAsync_WithValidDevice_ShouldCallP100Endpoint()
    {
        // Arrange
        SetupSuccessResponse();

        // Act
        var result = await _client.Plugs.TurnOnAsync("plug1");

        // Assert
        result.IsSuccess.Should().BeTrue();
        VerifyRequestContains("/actions/p100/on");
    }

    [Fact]
    public async Task TurnOffAsync_WithEmptyDeviceName_ShouldReturnFailure()
    {
        // Act
        var result = await _client.Plugs.TurnOffAsync("");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task TurnOffAsync_WithValidDevice_ShouldCallP100Endpoint()
    {
        // Arrange
        SetupSuccessResponse();

        // Act
        var result = await _client.Plugs.TurnOffAsync("plug1");

        // Assert
        result.IsSuccess.Should().BeTrue();
        VerifyRequestContains("/actions/p100/off");
    }

    [Fact]
    public async Task GetDeviceInfoAsync_WithEmptyDeviceName_ShouldReturnFailure()
    {
        // Act
        var result = await _client.Plugs.GetDeviceInfoAsync("");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task GetDeviceUsageAsync_WithEmptyDeviceName_ShouldReturnFailure()
    {
        // Act
        var result = await _client.Plugs.GetDeviceUsageAsync("");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task TurnOnAsync_WhenServerError_ShouldReturnFailure()
    {
        // Arrange
        SetupErrorResponse(HttpStatusCode.InternalServerError);

        // Act
        var result = await _client.Plugs.TurnOnAsync("plug1");

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

    private void SetupErrorResponse(HttpStatusCode statusCode)
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
                Content = new StringContent("Error")
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
