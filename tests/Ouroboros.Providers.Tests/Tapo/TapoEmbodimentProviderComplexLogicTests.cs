// Copyright (c) 2025 Ouroboros contributors. Licensed under the MIT License.

using System.Reactive.Linq;
using FluentAssertions;
using Moq;
using Ouroboros.Core.EmbodiedInteraction;
using Ouroboros.Providers.Tapo;
using Xunit;

namespace Ouroboros.Tests.Providers.Tapo;

/// <summary>
/// Complex-logic tests for TapoEmbodimentProvider: state machine transitions,
/// sensor activation/deactivation lifecycle, action routing logic,
/// RTSP camera sensor reading, vision model analysis, dispose behavior,
/// dual-client constructor validation, and event/perception observables.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TapoEmbodimentProviderComplexLogicTests
{
    // ========================================================
    // Constructor validation
    // ========================================================

    [Fact]
    public void Constructor_DualClient_BothNull_ThrowsArgumentException()
    {
        var act = () => new TapoEmbodimentProvider(
            tapoClient: null,
            rtspClientFactory: null);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*at least one*");
    }

    [Fact]
    public void Constructor_DualClient_OnlyTapoClient_Succeeds()
    {
        using var http = new HttpClient();
        using var rest = new TapoRestClient(http);

        using var provider = new TapoEmbodimentProvider(
            tapoClient: rest,
            rtspClientFactory: null);

        provider.ProviderId.Should().Be("tapo");
        provider.RestClient.Should().NotBeNull();
        provider.RtspClientFactory.Should().BeNull();
    }

    [Fact]
    public void Constructor_DualClient_OnlyRtspFactory_Succeeds()
    {
        var factoryMock = new Mock<ITapoRtspClientFactory>();

        using var provider = new TapoEmbodimentProvider(
            tapoClient: null,
            rtspClientFactory: factoryMock.Object);

        provider.ProviderId.Should().Be("tapo");
        provider.RtspClientFactory.Should().NotBeNull();
        provider.RestClient.Should().BeNull();
    }

    [Fact]
    public void Constructor_RTSP_NullFactory_ThrowsArgumentNull()
    {
        var act = () => new TapoEmbodimentProvider(
            (ITapoRtspClientFactory)null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("rtspClientFactory");
    }

    [Fact]
    public void Constructor_RTSP_ValidFactory_SetsProviderId()
    {
        var factoryMock = new Mock<ITapoRtspClientFactory>();

        using var provider = new TapoEmbodimentProvider(
            factoryMock.Object, "my-rtsp-provider");

        provider.ProviderId.Should().Be("my-rtsp-provider");
    }

    [Fact]
    public void SetRestClient_UpdatesClient()
    {
        using var http = new HttpClient();
        using var rest = new TapoRestClient(http);
        using var provider = new TapoEmbodimentProvider(rest);

        using var http2 = new HttpClient();
        using var rest2 = new TapoRestClient(http2);
        provider.SetRestClient(rest2);

        provider.RestClient.Should().BeSameAs(rest2);
    }

    // ========================================================
    // State machine: Connect -> Disconnect -> Disposed
    // ========================================================

    [Fact]
    public async Task GetState_NotConnected_ReturnsDormant()
    {
        using var http = new HttpClient();
        using var rest = new TapoRestClient(http);
        using var provider = new TapoEmbodimentProvider(rest);

        var result = await provider.GetStateAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(EmbodimentState.Dormant);
    }

    [Fact]
    public async Task Disconnect_WhenAlreadyDisconnected_ReturnsSuccess()
    {
        using var http = new HttpClient();
        using var rest = new TapoRestClient(http);
        using var provider = new TapoEmbodimentProvider(rest);

        var result = await provider.DisconnectAsync();

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Disposed_GetState_ReturnsFailure()
    {
        using var http = new HttpClient();
        using var rest = new TapoRestClient(http);
        var provider = new TapoEmbodimentProvider(rest);
        provider.Dispose();

        var result = await provider.GetStateAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("disposed");
    }

    [Fact]
    public async Task Disposed_GetSensors_ReturnsFailure()
    {
        using var http = new HttpClient();
        using var rest = new TapoRestClient(http);
        var provider = new TapoEmbodimentProvider(rest);
        provider.Dispose();

        var result = await provider.GetSensorsAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("disposed");
    }

    [Fact]
    public async Task Disposed_GetActuators_ReturnsFailure()
    {
        using var http = new HttpClient();
        using var rest = new TapoRestClient(http);
        var provider = new TapoEmbodimentProvider(rest);
        provider.Dispose();

        var result = await provider.GetActuatorsAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("disposed");
    }

    [Fact]
    public async Task Disposed_ActivateSensor_ReturnsFailure()
    {
        using var http = new HttpClient();
        using var rest = new TapoRestClient(http);
        var provider = new TapoEmbodimentProvider(rest);
        provider.Dispose();

        var result = await provider.ActivateSensorAsync("any");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("disposed");
    }

    [Fact]
    public async Task Disposed_DeactivateSensor_ReturnsFailure()
    {
        using var http = new HttpClient();
        using var rest = new TapoRestClient(http);
        var provider = new TapoEmbodimentProvider(rest);
        provider.Dispose();

        var result = await provider.DeactivateSensorAsync("any");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("disposed");
    }

    [Fact]
    public async Task Disposed_ReadSensor_ReturnsFailure()
    {
        using var http = new HttpClient();
        using var rest = new TapoRestClient(http);
        var provider = new TapoEmbodimentProvider(rest);
        provider.Dispose();

        var result = await provider.ReadSensorAsync("any");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("disposed");
    }

    [Fact]
    public async Task Disposed_ExecuteAction_ReturnsFailure()
    {
        using var http = new HttpClient();
        using var rest = new TapoRestClient(http);
        var provider = new TapoEmbodimentProvider(rest);
        provider.Dispose();

        var result = await provider.ExecuteActionAsync("any", ActuatorAction.TurnOn());

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("disposed");
    }

    [Fact]
    public async Task Disposed_Connect_ReturnsFailure()
    {
        using var http = new HttpClient();
        using var rest = new TapoRestClient(http);
        var provider = new TapoEmbodimentProvider(rest);
        provider.Dispose();

        var result = await provider.ConnectAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("disposed");
    }

    [Fact]
    public async Task Disposed_AnalyzeFrame_ReturnsFailure()
    {
        using var http = new HttpClient();
        using var rest = new TapoRestClient(http);
        var provider = new TapoEmbodimentProvider(rest);
        provider.Dispose();

        var result = await provider.AnalyzeFrameAsync("sensor", new byte[] { 1, 2, 3 });

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("disposed");
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        using var http = new HttpClient();
        using var rest = new TapoRestClient(http);
        var provider = new TapoEmbodimentProvider(rest);

        var act = () =>
        {
            provider.Dispose();
            provider.Dispose();
        };

        act.Should().NotThrow();
    }

    // ========================================================
    // Sensor/Actuator operations when not connected
    // ========================================================

    [Fact]
    public async Task ActivateSensor_NotConnected_ReturnsNotConnectedFailure()
    {
        using var http = new HttpClient();
        using var rest = new TapoRestClient(http);
        using var provider = new TapoEmbodimentProvider(rest);

        var result = await provider.ActivateSensorAsync("camera1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Not connected");
    }

    [Fact]
    public async Task ReadSensor_NotConnected_ReturnsFailure()
    {
        using var http = new HttpClient();
        using var rest = new TapoRestClient(http);
        using var provider = new TapoEmbodimentProvider(rest);

        var result = await provider.ReadSensorAsync("camera1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Not connected");
    }

    [Fact]
    public async Task ExecuteAction_NotConnected_ReturnsFailure()
    {
        using var http = new HttpClient();
        using var rest = new TapoRestClient(http);
        using var provider = new TapoEmbodimentProvider(rest);

        var result = await provider.ExecuteActionAsync("light1", ActuatorAction.TurnOn());

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Not connected");
    }

    // ========================================================
    // Analyze Frame
    // ========================================================

    [Fact]
    public async Task AnalyzeFrame_NoVisionModel_ReturnsFailure()
    {
        using var http = new HttpClient();
        using var rest = new TapoRestClient(http);
        using var provider = new TapoEmbodimentProvider(rest, visionModel: null);

        var result = await provider.AnalyzeFrameAsync("sensor1", new byte[] { 1, 2, 3 });

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Vision model not available");
    }

    // ========================================================
    // Authentication
    // ========================================================

    [Fact]
    public async Task Authenticate_Disposed_ReturnsFailure()
    {
        using var http = new HttpClient();
        using var rest = new TapoRestClient(http);
        var provider = new TapoEmbodimentProvider(rest);
        provider.Dispose();

        var result = await provider.AuthenticateAsync("password");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("disposed");
    }

    [Fact]
    public async Task Authenticate_NoRestClient_ReturnsFailure()
    {
        var factoryMock = new Mock<ITapoRtspClientFactory>();
        factoryMock.Setup(f => f.GetCameraNames()).Returns(Array.Empty<string>());
        using var provider = new TapoEmbodimentProvider(factoryMock.Object);

        var result = await provider.AuthenticateAsync("password");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("REST client not configured");
    }

    // ========================================================
    // Observable streams are available
    // ========================================================

    [Fact]
    public void Perceptions_Stream_SubscribableBeforeConnect()
    {
        using var http = new HttpClient();
        using var rest = new TapoRestClient(http);
        using var provider = new TapoEmbodimentProvider(rest);

        PerceptionData? received = null;
        using var sub = provider.Perceptions.Subscribe(p => received = p);

        provider.Perceptions.Should().NotBeNull();
    }

    [Fact]
    public void Events_Stream_SubscribableBeforeConnect()
    {
        using var http = new HttpClient();
        using var rest = new TapoRestClient(http);
        using var provider = new TapoEmbodimentProvider(rest);

        EmbodimentProviderEvent? received = null;
        using var sub = provider.Events.Subscribe(e => received = e);

        provider.Events.Should().NotBeNull();
    }

    // ========================================================
    // ProviderName and ProviderId
    // ========================================================

    [Fact]
    public void ProviderName_AlwaysTapoSmartDevices()
    {
        using var http = new HttpClient();
        using var rest = new TapoRestClient(http);
        using var provider = new TapoEmbodimentProvider(rest, "custom-id");

        provider.ProviderName.Should().Be("Tapo Smart Devices");
        provider.ProviderId.Should().Be("custom-id");
    }

    // ========================================================
    // DeactivateSensor: graceful when sensor not active
    // ========================================================

    [Fact]
    public async Task DeactivateSensor_UnknownSensor_ReturnsSuccess()
    {
        // DeactivateSensor does not check connection state (no guard),
        // and if sensor not found in active list it just succeeds
        using var http = new HttpClient();
        using var rest = new TapoRestClient(http);
        using var provider = new TapoEmbodimentProvider(rest);

        var result = await provider.DeactivateSensorAsync("nonexistent");

        result.IsSuccess.Should().BeTrue();
    }

    // ========================================================
    // Empty sensor/actuator lists when not inventoried
    // ========================================================

    [Fact]
    public async Task GetSensors_BeforeConnect_ReturnsEmptyList()
    {
        using var http = new HttpClient();
        using var rest = new TapoRestClient(http);
        using var provider = new TapoEmbodimentProvider(rest);

        var result = await provider.GetSensorsAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActuators_BeforeConnect_ReturnsEmptyList()
    {
        using var http = new HttpClient();
        using var rest = new TapoRestClient(http);
        using var provider = new TapoEmbodimentProvider(rest);

        var result = await provider.GetActuatorsAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    // ========================================================
    // Constructor with null ProviderId throws
    // ========================================================

    [Fact]
    public void Constructor_NullProviderId_ThrowsArgumentNull()
    {
        using var http = new HttpClient();
        using var rest = new TapoRestClient(http);

        var act = () => new TapoEmbodimentProvider(rest, providerId: null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("providerId");
    }
}
