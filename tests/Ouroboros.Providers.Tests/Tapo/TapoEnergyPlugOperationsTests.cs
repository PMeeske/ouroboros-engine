using System.Net;
using FluentAssertions;

namespace Ouroboros.Tests.Tapo;

[Trait("Category", "Unit")]
public class TapoEnergyPlugOperationsTests : IDisposable
{
    private readonly Mock<HttpMessageHandler> _mockHandler;
    private readonly HttpClient _httpClient;
    private readonly TapoRestClient _client;

    public TapoEnergyPlugOperationsTests()
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
        var result = await _client.EnergyPlugs.TurnOnAsync("");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task TurnOnAsync_WithValidDevice_ShouldCallP110Endpoint()
    {
        // Arrange
        SetupSuccessResponse();

        // Act
        var result = await _client.EnergyPlugs.TurnOnAsync("energy-plug1");

        // Assert
        result.IsSuccess.Should().BeTrue();
        VerifyRequestContains("/actions/p110/on");
    }

    [Fact]
    public async Task TurnOffAsync_WithValidDevice_ShouldCallP110Endpoint()
    {
        // Arrange
        SetupSuccessResponse();

        // Act
        var result = await _client.EnergyPlugs.TurnOffAsync("energy-plug1");

        // Assert
        result.IsSuccess.Should().BeTrue();
        VerifyRequestContains("/actions/p110/off");
    }

    [Fact]
    public async Task GetEnergyUsageAsync_WithEmptyDeviceName_ShouldReturnFailure()
    {
        // Act
        var result = await _client.EnergyPlugs.GetEnergyUsageAsync("");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task GetEnergyUsageAsync_WithValidDevice_ShouldCallCorrectEndpoint()
    {
        // Arrange
        SetupJsonResponse("""{"current_power":150}""");

        // Act
        var result = await _client.EnergyPlugs.GetEnergyUsageAsync("plug1");

        // Assert
        result.IsSuccess.Should().BeTrue();
        VerifyRequestContains("/actions/p110/get-energy-usage");
    }

    [Fact]
    public async Task GetCurrentPowerAsync_WithEmptyDeviceName_ShouldReturnFailure()
    {
        // Act
        var result = await _client.EnergyPlugs.GetCurrentPowerAsync("");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task GetCurrentPowerAsync_WithValidDevice_ShouldCallCorrectEndpoint()
    {
        // Arrange
        SetupJsonResponse("""{"power_mw":12345}""");

        // Act
        var result = await _client.EnergyPlugs.GetCurrentPowerAsync("plug1");

        // Assert
        result.IsSuccess.Should().BeTrue();
        VerifyRequestContains("/actions/p110/get-current-power");
    }

    [Fact]
    public async Task GetHourlyEnergyDataAsync_WithEmptyDeviceName_ShouldReturnFailure()
    {
        // Act
        var result = await _client.EnergyPlugs.GetHourlyEnergyDataAsync("", new DateOnly(2026, 3, 1));

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task GetHourlyEnergyDataAsync_WithValidParams_ShouldIncludeStartDate()
    {
        // Arrange
        SetupJsonResponse("""{"data":[]}""");

        // Act
        var result = await _client.EnergyPlugs.GetHourlyEnergyDataAsync(
            "plug1", new DateOnly(2026, 3, 1));

        // Assert
        result.IsSuccess.Should().BeTrue();
        VerifyRequestContains("start_date=2026-03-01");
    }

    [Fact]
    public async Task GetHourlyEnergyDataAsync_WithEndDate_ShouldIncludeEndDate()
    {
        // Arrange
        SetupJsonResponse("""{"data":[]}""");

        // Act
        var result = await _client.EnergyPlugs.GetHourlyEnergyDataAsync(
            "plug1", new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 15));

        // Assert
        result.IsSuccess.Should().BeTrue();
        VerifyRequestContains("end_date=2026-03-15");
    }

    [Fact]
    public async Task GetDailyEnergyDataAsync_WithEmptyDeviceName_ShouldReturnFailure()
    {
        // Act
        var result = await _client.EnergyPlugs.GetDailyEnergyDataAsync("", new DateOnly(2026, 3, 1));

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task GetDailyEnergyDataAsync_WithValidParams_ShouldCallCorrectEndpoint()
    {
        // Arrange
        SetupJsonResponse("""{"data":[]}""");

        // Act
        var result = await _client.EnergyPlugs.GetDailyEnergyDataAsync(
            "plug1", new DateOnly(2026, 3, 1));

        // Assert
        result.IsSuccess.Should().BeTrue();
        VerifyRequestContains("/actions/p110/get-daily-energy-data");
    }

    [Fact]
    public async Task GetMonthlyEnergyDataAsync_WithEmptyDeviceName_ShouldReturnFailure()
    {
        // Act
        var result = await _client.EnergyPlugs.GetMonthlyEnergyDataAsync("", new DateOnly(2026, 1, 1));

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task GetMonthlyEnergyDataAsync_WithValidParams_ShouldCallCorrectEndpoint()
    {
        // Arrange
        SetupJsonResponse("""{"data":[]}""");

        // Act
        var result = await _client.EnergyPlugs.GetMonthlyEnergyDataAsync(
            "plug1", new DateOnly(2026, 1, 1));

        // Assert
        result.IsSuccess.Should().BeTrue();
        VerifyRequestContains("/actions/p110/get-monthly-energy-data");
    }

    [Fact]
    public async Task GetDeviceInfoAsync_WithEmptyDeviceName_ShouldReturnFailure()
    {
        // Act
        var result = await _client.EnergyPlugs.GetDeviceInfoAsync("");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task GetDeviceUsageAsync_WithEmptyDeviceName_ShouldReturnFailure()
    {
        // Act
        var result = await _client.EnergyPlugs.GetDeviceUsageAsync("");

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
