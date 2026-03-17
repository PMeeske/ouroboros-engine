using FluentAssertions;

namespace Ouroboros.Tests.Tapo;

[Trait("Category", "Unit")]
public class TapoCameraPtzClientTests
{
    [Fact]
    public void Constructor_WithNullCameraIp_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new TapoCameraPtzClient(null!, "user", "pass");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullUsername_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new TapoCameraPtzClient("192.168.1.1", null!, "pass");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullPassword_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new TapoCameraPtzClient("192.168.1.1", "user", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithValidParams_ShouldSetProperties()
    {
        // Act
        using var client = new TapoCameraPtzClient("192.168.1.100", "admin", "secret");

        // Assert
        client.CameraIp.Should().Be("192.168.1.100");
        client.Capabilities.Should().Be(PtzCapabilities.Default);
    }

    [Fact]
    public async Task StopAsync_WhenDisposed_ShouldReturnFailure()
    {
        // Arrange
        var client = new TapoCameraPtzClient("192.168.1.100", "admin", "secret");
        client.Dispose();

        // Act
        var result = await client.StopAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("disposed");
    }

    [Fact]
    public async Task GoToHomeAsync_WhenDisposed_ShouldReturnFailure()
    {
        // Arrange
        var client = new TapoCameraPtzClient("192.168.1.100", "admin", "secret");
        client.Dispose();

        // Act
        var result = await client.GoToHomeAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SetPresetAsync_WhenDisposed_ShouldReturnFailure()
    {
        // Arrange
        var client = new TapoCameraPtzClient("192.168.1.100", "admin", "secret");
        client.Dispose();

        // Act
        var result = await client.SetPresetAsync("preset1");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task GoToPresetAsync_WhenDisposed_ShouldReturnFailure()
    {
        // Arrange
        var client = new TapoCameraPtzClient("192.168.1.100", "admin", "secret");
        client.Dispose();

        // Act
        var result = await client.GoToPresetAsync("1");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task PatrolSweepAsync_WhenDisposed_ShouldReturnFailure()
    {
        // Arrange
        var client = new TapoCameraPtzClient("192.168.1.100", "admin", "secret");
        client.Dispose();

        // Act
        var result = await client.PatrolSweepAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_WhenDisposed_ShouldReturnFailure()
    {
        // Arrange
        var client = new TapoCameraPtzClient("192.168.1.100", "admin", "secret");
        client.Dispose();

        // Act
        var result = await client.InitializeAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Dispose_ShouldBeIdempotent()
    {
        // Arrange
        var client = new TapoCameraPtzClient("192.168.1.100", "admin", "secret");

        // Act & Assert - double dispose should not throw
        client.Dispose();
        client.Dispose();
    }

    [Fact]
    public void Capabilities_Default_ShouldBeSetInitially()
    {
        // Arrange
        using var client = new TapoCameraPtzClient("192.168.1.100", "admin", "secret");

        // Act
        var capabilities = client.Capabilities;

        // Assert
        capabilities.CanPan.Should().BeTrue();
        capabilities.CanTilt.Should().BeTrue();
        capabilities.CanZoom.Should().BeFalse();
        capabilities.SupportsContinuousMove.Should().BeTrue();
        capabilities.SupportsPresets.Should().BeTrue();
        capabilities.MaxPresets.Should().Be(8);
    }
}
