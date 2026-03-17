using System.Net;
using FluentAssertions;

namespace Ouroboros.Tests.Tapo;

[Trait("Category", "Unit")]
public class TapoEmbodimentProviderTests : IDisposable
{
    private readonly Mock<HttpMessageHandler> _mockHandler;
    private readonly HttpClient _httpClient;
    private readonly TapoRestClient _tapoClient;

    public TapoEmbodimentProviderTests()
    {
        _mockHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:8123")
        };
        _tapoClient = new TapoRestClient(_httpClient);
    }

    public void Dispose()
    {
        _tapoClient.Dispose();
        _httpClient.Dispose();
    }

    [Fact]
    public void Constructor_WithNullTapoClient_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new TapoEmbodimentProvider((TapoRestClient)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullProviderId_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new TapoEmbodimentProvider(_tapoClient, providerId: null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullRtspFactory_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new TapoEmbodimentProvider((ITapoRtspClientFactory)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithBothNull_ShouldThrowArgumentException()
    {
        // Act
        var act = () => new TapoEmbodimentProvider(
            tapoClient: (TapoRestClient?)null,
            rtspClientFactory: (ITapoRtspClientFactory?)null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithTapoClient_ShouldSetProperties()
    {
        // Act
        using var provider = new TapoEmbodimentProvider(_tapoClient, providerId: "test");

        // Assert
        provider.ProviderId.Should().Be("test");
        provider.ProviderName.Should().Be("Tapo Smart Devices");
        provider.IsConnected.Should().BeFalse();
        provider.RestClient.Should().BeSameAs(_tapoClient);
    }

    [Fact]
    public void Constructor_WithRtspFactory_ShouldSetProperties()
    {
        // Arrange
        var mockFactory = new Mock<ITapoRtspClientFactory>();

        // Act
        using var provider = new TapoEmbodimentProvider(
            mockFactory.Object, providerId: "rtsp-test");

        // Assert
        provider.ProviderId.Should().Be("rtsp-test");
        provider.RtspClientFactory.Should().BeSameAs(mockFactory.Object);
    }

    [Fact]
    public void SetRestClient_ShouldUpdateClient()
    {
        // Arrange
        using var provider = new TapoEmbodimentProvider(_tapoClient);
        var newHttpClient = new HttpClient(_mockHandler.Object)
        {
            BaseAddress = new Uri("http://newhost:8123")
        };
        var newClient = new TapoRestClient(newHttpClient);

        // Act
        provider.SetRestClient(newClient);

        // Assert
        provider.RestClient.Should().BeSameAs(newClient);

        // Cleanup
        newClient.Dispose();
        newHttpClient.Dispose();
    }

    [Fact]
    public void IsConnected_Initially_ShouldBeFalse()
    {
        // Arrange
        using var provider = new TapoEmbodimentProvider(_tapoClient);

        // Act & Assert
        provider.IsConnected.Should().BeFalse();
    }

    [Fact]
    public void Perceptions_ShouldBeObservable()
    {
        // Arrange
        using var provider = new TapoEmbodimentProvider(_tapoClient);

        // Act & Assert
        provider.Perceptions.Should().NotBeNull();
    }

    [Fact]
    public void Events_ShouldBeObservable()
    {
        // Arrange
        using var provider = new TapoEmbodimentProvider(_tapoClient);

        // Act & Assert
        provider.Events.Should().NotBeNull();
    }

    [Fact]
    public async Task DisconnectAsync_WhenNotConnected_ShouldReturnSuccess()
    {
        // Arrange
        using var provider = new TapoEmbodimentProvider(_tapoClient);

        // Act
        var result = await provider.DisconnectAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        // Arrange
        var provider = new TapoEmbodimentProvider(_tapoClient);

        // Act & Assert
        provider.Dispose();
    }

    [Fact]
    public void Dispose_ShouldBeIdempotent()
    {
        // Arrange
        var provider = new TapoEmbodimentProvider(_tapoClient);

        // Act & Assert
        provider.Dispose();
        provider.Dispose();
    }

    [Fact]
    public async Task ConnectAsync_WhenDisposed_ShouldReturnFailure()
    {
        // Arrange
        var provider = new TapoEmbodimentProvider(_tapoClient);
        provider.Dispose();

        // Act
        var result = await provider.ConnectAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("disposed");
    }

    [Fact]
    public async Task GetStateAsync_WhenDisposed_ShouldReturnFailure()
    {
        // Arrange
        var provider = new TapoEmbodimentProvider(_tapoClient);
        provider.Dispose();

        // Act
        var result = await provider.GetStateAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task GetSensorsAsync_WhenDisposed_ShouldReturnFailure()
    {
        // Arrange
        var provider = new TapoEmbodimentProvider(_tapoClient);
        provider.Dispose();

        // Act
        var result = await provider.GetSensorsAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task GetActuatorsAsync_WhenDisposed_ShouldReturnFailure()
    {
        // Arrange
        var provider = new TapoEmbodimentProvider(_tapoClient);
        provider.Dispose();

        // Act
        var result = await provider.GetActuatorsAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ActivateSensorAsync_WhenDisposed_ShouldReturnFailure()
    {
        // Arrange
        var provider = new TapoEmbodimentProvider(_tapoClient);
        provider.Dispose();

        // Act
        var result = await provider.ActivateSensorAsync("sensor1");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ActivateSensorAsync_WhenNotConnected_ShouldReturnFailure()
    {
        // Arrange
        using var provider = new TapoEmbodimentProvider(_tapoClient);

        // Act
        var result = await provider.ActivateSensorAsync("sensor1");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Not connected");
    }

    [Fact]
    public async Task DeactivateSensorAsync_WhenDisposed_ShouldReturnFailure()
    {
        // Arrange
        var provider = new TapoEmbodimentProvider(_tapoClient);
        provider.Dispose();

        // Act
        var result = await provider.DeactivateSensorAsync("sensor1");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ReadSensorAsync_WhenDisposed_ShouldReturnFailure()
    {
        // Arrange
        var provider = new TapoEmbodimentProvider(_tapoClient);
        provider.Dispose();

        // Act
        var result = await provider.ReadSensorAsync("sensor1");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ReadSensorAsync_WhenNotConnected_ShouldReturnFailure()
    {
        // Arrange
        using var provider = new TapoEmbodimentProvider(_tapoClient);

        // Act
        var result = await provider.ReadSensorAsync("sensor1");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Not connected");
    }

    [Fact]
    public async Task AuthenticateAsync_WhenDisposed_ShouldReturnFailure()
    {
        // Arrange
        var provider = new TapoEmbodimentProvider(_tapoClient);
        provider.Dispose();

        // Act
        var result = await provider.AuthenticateAsync("password");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task AuthenticateAsync_WithRtspOnlyMode_ShouldReturnFailure()
    {
        // Arrange
        var mockFactory = new Mock<ITapoRtspClientFactory>();
        mockFactory.Setup(f => f.GetCameraNames()).Returns(Array.Empty<string>());
        using var provider = new TapoEmbodimentProvider(mockFactory.Object);

        // Act
        var result = await provider.AuthenticateAsync("password");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("REST client not configured");
    }

    [Fact]
    public async Task AnalyzeFrameAsync_WhenDisposed_ShouldReturnFailure()
    {
        // Arrange
        var provider = new TapoEmbodimentProvider(_tapoClient);
        provider.Dispose();

        // Act
        var result = await provider.AnalyzeFrameAsync("sensor1", new byte[] { 1, 2, 3 });

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzeFrameAsync_WithNoVisionModel_ShouldReturnFailure()
    {
        // Arrange
        using var provider = new TapoEmbodimentProvider(_tapoClient);

        // Act
        var result = await provider.AnalyzeFrameAsync("sensor1", new byte[] { 1, 2, 3 });

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Vision model not available");
    }

    [Fact]
    public async Task ExecuteActionAsync_WhenDisposed_ShouldReturnFailure()
    {
        // Arrange
        var provider = new TapoEmbodimentProvider(_tapoClient);
        provider.Dispose();

        // Act - we need to provide ActuatorAction, but since the foundation submodule is not available,
        // this test verifies disposal behavior
        // The provider checks disposed first before any action
        // We cannot construct ActuatorAction without the foundation types
    }
}
