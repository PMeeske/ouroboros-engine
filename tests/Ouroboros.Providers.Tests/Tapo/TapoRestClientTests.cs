using System.Net;
using FluentAssertions;

namespace Ouroboros.Tests.Tapo;

[Trait("Category", "Unit")]
public class TapoRestClientTests : IDisposable
{
    private readonly Mock<HttpMessageHandler> _mockHandler;
    private readonly HttpClient _httpClient;
    private readonly TapoRestClient _sut;

    public TapoRestClientTests()
    {
        _mockHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:8123")
        };
        _sut = new TapoRestClient(_httpClient);
    }

    public void Dispose()
    {
        _sut.Dispose();
        _httpClient.Dispose();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullHttpClient_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new TapoRestClient(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithValidHttpClient_ShouldInitializeOperationProperties()
    {
        // Act & Assert
        _sut.LightBulbs.Should().NotBeNull();
        _sut.ColorLightBulbs.Should().NotBeNull();
        _sut.LightStrips.Should().NotBeNull();
        _sut.RgbicLightStrips.Should().NotBeNull();
        _sut.Plugs.Should().NotBeNull();
        _sut.EnergyPlugs.Should().NotBeNull();
        _sut.PowerStrips.Should().NotBeNull();
    }

    #endregion

    #region LoginAsync Tests

    [Fact]
    public async Task LoginAsync_WithEmptyPassword_ShouldReturnFailure()
    {
        // Act
        var result = await _sut.LoginAsync("");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Password is required");
    }

    [Fact]
    public async Task LoginAsync_WithWhitespacePassword_ShouldReturnFailure()
    {
        // Act
        var result = await _sut.LoginAsync("   ");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task LoginAsync_WithValidPassword_ShouldReturnSessionId()
    {
        // Arrange
        SetupResponse(HttpStatusCode.OK, "test-session-id", HttpMethod.Post, "/login");

        // Act
        var result = await _sut.LoginAsync("valid-password");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("test-session-id");
    }

    [Fact]
    public async Task LoginAsync_WithUnauthorizedResponse_ShouldReturnFailure()
    {
        // Arrange
        SetupResponse(HttpStatusCode.Unauthorized, "Invalid password", HttpMethod.Post, "/login");

        // Act
        var result = await _sut.LoginAsync("wrong-password");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Login failed");
    }

    #endregion

    #region GetDevicesAsync Tests

    [Fact]
    public async Task GetDevicesAsync_WhenNotAuthenticated_ShouldReturnFailure()
    {
        // Act
        var result = await _sut.GetDevicesAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Not authenticated");
    }

    [Fact]
    public async Task GetDevicesAsync_WhenAuthenticated_ShouldReturnDevices()
    {
        // Arrange
        await AuthenticateClient();
        var deviceJson = """[{"name":"bulb1","device_type":"L530","ip_addr":"192.168.1.1"}]""";
        SetupResponse(HttpStatusCode.OK, deviceJson, HttpMethod.Get, "/devices");

        // Act
        var result = await _sut.GetDevicesAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Name.Should().Be("bulb1");
    }

    #endregion

    #region GetActionsAsync Tests

    [Fact]
    public async Task GetActionsAsync_WhenNotAuthenticated_ShouldReturnFailure()
    {
        // Act
        var result = await _sut.GetActionsAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Not authenticated");
    }

    [Fact]
    public async Task GetActionsAsync_WhenAuthenticated_ShouldReturnActions()
    {
        // Arrange
        await AuthenticateClient();
        SetupResponse(HttpStatusCode.OK, """["l530/on","l530/off"]""", HttpMethod.Get, "/actions");

        // Act
        var result = await _sut.GetActionsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    #endregion

    #region RefreshSessionAsync Tests

    [Fact]
    public async Task RefreshSessionAsync_WhenNotAuthenticated_ShouldReturnFailure()
    {
        // Act
        var result = await _sut.RefreshSessionAsync("device1");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Not authenticated");
    }

    [Fact]
    public async Task RefreshSessionAsync_WithEmptyDeviceName_ShouldReturnFailure()
    {
        // Arrange
        await AuthenticateClient();

        // Act
        var result = await _sut.RefreshSessionAsync("");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Device name is required");
    }

    [Fact]
    public async Task RefreshSessionAsync_WithValidDevice_ShouldReturnSuccess()
    {
        // Arrange
        await AuthenticateClient();
        SetupResponse(HttpStatusCode.OK, "", HttpMethod.Get);

        // Act
        var result = await _sut.RefreshSessionAsync("device1");

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region ReloadConfigAsync Tests

    [Fact]
    public async Task ReloadConfigAsync_WhenNotAuthenticated_ShouldReturnFailure()
    {
        // Act
        var result = await _sut.ReloadConfigAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ReloadConfigAsync_WhenAuthenticated_ShouldReturnSuccess()
    {
        // Arrange
        await AuthenticateClient();
        SetupResponse(HttpStatusCode.OK, "", HttpMethod.Post, "/reload-config");

        // Act
        var result = await _sut.ReloadConfigAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region DiscoverDevicesAsync Tests

    [Fact]
    public async Task DiscoverDevicesAsync_WhenNotAuthenticated_ShouldReturnFailure()
    {
        // Act
        var result = await _sut.DiscoverDevicesAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task DiscoverDevicesAsync_WhenAuthenticated_ShouldReturnDiscoveredDevices()
    {
        // Arrange
        await AuthenticateClient();
        var deviceJson = """[{"name":"new-plug","device_type":"P100","ip_addr":"192.168.1.50"}]""";
        SetupResponse(HttpStatusCode.OK, deviceJson, HttpMethod.Post, "/discover");

        // Act
        var result = await _sut.DiscoverDevicesAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Name.Should().Be("new-plug");
    }

    #endregion

    #region HealthCheckAsync Tests

    [Fact]
    public async Task HealthCheckAsync_WhenServerResponds_ShouldReturnSuccess()
    {
        // Arrange
        SetupResponse(HttpStatusCode.OK, "ok", HttpMethod.Get, "/health");

        // Act
        var result = await _sut.HealthCheckAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("ok");
    }

    [Fact]
    public async Task HealthCheckAsync_WhenServerDown_ShouldReturnFailure()
    {
        // Arrange
        SetupResponse(HttpStatusCode.ServiceUnavailable, "", HttpMethod.Get, "/health");

        // Act
        var result = await _sut.HealthCheckAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region Helpers

    private async Task AuthenticateClient()
    {
        SetupResponse(HttpStatusCode.OK, "test-session", HttpMethod.Post, "/login");
        await _sut.LoginAsync("password");
        // Reset handler for next call
        _mockHandler.Reset();
    }

    private void SetupResponse(
        HttpStatusCode statusCode, string content,
        HttpMethod? method = null, string? path = null)
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
                Content = new StringContent(content)
            });
    }

    #endregion
}
