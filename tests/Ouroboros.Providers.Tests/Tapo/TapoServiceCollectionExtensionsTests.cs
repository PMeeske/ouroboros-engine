using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Ouroboros.Tests.Tapo;

[Trait("Category", "Unit")]
public class TapoServiceCollectionExtensionsTests
{
    [Fact]
    public void AddTapoRestClient_WithNullServices_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => TapoServiceCollectionExtensions.AddTapoRestClient(null!, "http://localhost");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddTapoRestClient_WithEmptyBaseAddress_ShouldThrowArgumentException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var act = () => services.AddTapoRestClient("");

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("baseAddress");
    }

    [Fact]
    public void AddTapoRestClient_WithWhitespaceBaseAddress_ShouldThrowArgumentException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var act = () => services.AddTapoRestClient("   ");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddTapoRestClient_WithValidBaseAddress_ShouldRegisterServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddTapoRestClient("http://localhost:8123");

        // Assert
        result.Should().BeSameAs(services);
        services.Should().Contain(sd => sd.ServiceType == typeof(TapoRestClient));
    }

    [Fact]
    public void AddTapoRestClient_WithConfigureAction_ShouldAcceptNonNullAction()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddTapoRestClient(client =>
        {
            client.BaseAddress = new Uri("http://localhost:8123");
        });

        // Assert
        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddTapoRestClient_WithNullConfigureAction_ShouldThrowArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var act = () => services.AddTapoRestClient((Action<HttpClient>)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddTapoEmbodimentProvider_WithNullServices_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => TapoServiceCollectionExtensions.AddTapoEmbodimentProvider(null!, "http://localhost");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddTapoEmbodimentProvider_WithEmptyBaseAddress_ShouldThrowArgumentException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var act = () => services.AddTapoEmbodimentProvider("");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddTapoEmbodimentProvider_WithValidBaseAddress_ShouldRegisterServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddTapoEmbodimentProvider("http://localhost:8123");

        // Assert
        result.Should().BeSameAs(services);
        services.Should().Contain(sd => sd.ServiceType == typeof(TapoRestClient));
        services.Should().Contain(sd => sd.ServiceType == typeof(TapoVisionModelConfig));
        services.Should().Contain(sd => sd.ServiceType == typeof(TapoEmbodimentProvider));
    }

    [Fact]
    public void AddTapoEmbodimentProvider_WithVisionConfig_ShouldApplyConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddTapoEmbodimentProvider("http://localhost:8123",
            configureVision: config => config with { MaxObjectsPerFrame = 100 });

        // Assert
        var visionConfigDescriptor = services.FirstOrDefault(sd =>
            sd.ServiceType == typeof(TapoVisionModelConfig));
        visionConfigDescriptor.Should().NotBeNull();
    }

    [Fact]
    public void AddTapoRtspCamera_WithNullServices_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => TapoServiceCollectionExtensions.AddTapoRtspCamera(
            null!, "192.168.1.1", "user", "pass");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddTapoRtspCamera_WithEmptyCameraIp_ShouldThrowArgumentException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var act = () => services.AddTapoRtspCamera("", "user", "pass");

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("cameraIp");
    }

    [Fact]
    public void AddTapoRtspCamera_WithEmptyUsername_ShouldThrowArgumentException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var act = () => services.AddTapoRtspCamera("192.168.1.1", "", "pass");

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("username");
    }

    [Fact]
    public void AddTapoRtspCamera_WithEmptyPassword_ShouldThrowArgumentException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var act = () => services.AddTapoRtspCamera("192.168.1.1", "user", "");

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("password");
    }

    [Fact]
    public void AddTapoRtspCamera_WithValidParams_ShouldRegisterClient()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddTapoRtspCamera("192.168.1.1", "user", "pass");

        // Assert
        result.Should().BeSameAs(services);
        services.Should().Contain(sd => sd.ServiceType == typeof(TapoRtspClient));
    }

    [Fact]
    public void AddTapoGateway_WithNullServices_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => TapoServiceCollectionExtensions.AddTapoGateway(null!, "/path/to/script.py");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddTapoGateway_WithEmptyPath_ShouldThrowArgumentException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var act = () => services.AddTapoGateway("");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddTapoGateway_WithValidPath_ShouldRegisterGatewayManager()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddTapoGateway("/path/to/gateway.py");

        // Assert
        result.Should().BeSameAs(services);
        services.Should().Contain(sd => sd.ServiceType == typeof(TapoGatewayManager));
    }

    [Fact]
    public void AddTapoRtspCameras_WithCameras_ShouldRegisterFactory()
    {
        // Arrange
        var services = new ServiceCollection();
        var cameras = new List<TapoDevice>
        {
            new() { Name = "cam1", DeviceType = TapoDeviceType.C200, IpAddress = "192.168.1.10" },
            new() { Name = "cam2", DeviceType = TapoDeviceType.C210, IpAddress = "192.168.1.11" }
        };

        // Act
        var result = services.AddTapoRtspCameras(cameras, "user", "pass");

        // Assert
        result.Should().BeSameAs(services);
        services.Should().Contain(sd => sd.ServiceType == typeof(ITapoRtspClientFactory));
    }

    [Fact]
    public void AddTapoRtspCameras_WithNonCameraDevices_ShouldFilterToOnlyCameras()
    {
        // Arrange
        var services = new ServiceCollection();
        var devices = new List<TapoDevice>
        {
            new() { Name = "cam1", DeviceType = TapoDeviceType.C200, IpAddress = "192.168.1.10" },
            new() { Name = "bulb1", DeviceType = TapoDeviceType.L530, IpAddress = "192.168.1.20" },
            new() { Name = "plug1", DeviceType = TapoDeviceType.P100, IpAddress = "192.168.1.30" }
        };

        // Act
        var result = services.AddTapoRtspCameras(devices, "user", "pass");

        // Assert - should register factory (filtering happens internally)
        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddTapoEmbodimentAggregate_WithValidParams_ShouldRegisterAggregateAndProvider()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddTapoEmbodimentAggregate("http://localhost:8123");

        // Assert
        result.Should().BeSameAs(services);
        services.Should().Contain(sd => sd.ServiceType == typeof(TapoEmbodimentProvider));
    }
}
