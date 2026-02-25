using Ouroboros.Core.EmbodiedInteraction;

namespace Ouroboros.Tests.Providers.Tapo;

/// <summary>
/// Tests for TapoEmbodimentProvider (repository pattern implementation).
/// </summary>
[Trait("Category", "Unit")]
public class TapoEmbodimentProviderTests
{
    [Fact]
    public void Constructor_WithValidParameters_Succeeds()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);

        // Act
        using var provider = new TapoEmbodimentProvider(tapoClient);

        // Assert
        provider.Should().NotBeNull();
        provider.ProviderId.Should().Be("tapo");
        provider.ProviderName.Should().Be("Tapo Smart Devices");
        provider.IsConnected.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithCustomProviderId_UsesProvidedId()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);

        // Act
        using var provider = new TapoEmbodimentProvider(tapoClient, "custom-tapo");

        // Assert
        provider.ProviderId.Should().Be("custom-tapo");
    }

    [Fact]
    public void Constructor_WithNullTapoClient_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new TapoEmbodimentProvider((TapoRestClient)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("tapoClient");
    }

    [Fact]
    public async Task GetStateAsync_WhenNotConnected_ReturnsDormant()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var provider = new TapoEmbodimentProvider(tapoClient);

        // Act
        var result = await provider.GetStateAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(EmbodimentState.Dormant);
    }

    [Fact]
    public async Task GetSensorsAsync_WhenNotConnected_ReturnsEmptyList()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var provider = new TapoEmbodimentProvider(tapoClient);

        // Act
        var result = await provider.GetSensorsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActuatorsAsync_WhenNotConnected_ReturnsEmptyList()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var provider = new TapoEmbodimentProvider(tapoClient);

        // Act
        var result = await provider.GetActuatorsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task DisconnectAsync_WhenNotConnected_ReturnsSuccess()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var provider = new TapoEmbodimentProvider(tapoClient);

        // Act
        var result = await provider.DisconnectAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ActivateSensorAsync_WhenNotConnected_ReturnsFailure()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var provider = new TapoEmbodimentProvider(tapoClient);

        // Act
        var result = await provider.ActivateSensorAsync("camera1");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Not connected");
    }

    [Fact]
    public async Task ExecuteActionAsync_WhenNotConnected_ReturnsFailure()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var provider = new TapoEmbodimentProvider(tapoClient);

        var action = ActuatorAction.TurnOn();

        // Act
        var result = await provider.ExecuteActionAsync("light1", action);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Not connected");
    }

    [Fact]
    public void Perceptions_Observable_IsAvailable()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var provider = new TapoEmbodimentProvider(tapoClient);

        // Assert
        provider.Perceptions.Should().NotBeNull();
    }

    [Fact]
    public void Events_Observable_IsAvailable()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var provider = new TapoEmbodimentProvider(tapoClient);

        // Assert
        provider.Events.Should().NotBeNull();
    }

    [Fact]
    public async Task Dispose_DisposesResources()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        var provider = new TapoEmbodimentProvider(tapoClient);

        // Act
        provider.Dispose();

        // Assert - disposed provider should return failures
        var result = await provider.GetStateAsync();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("disposed");
    }
}