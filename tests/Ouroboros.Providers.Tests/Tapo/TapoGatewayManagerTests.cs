using FluentAssertions;

namespace Ouroboros.Tests.Tapo;

[Trait("Category", "Unit")]
public class TapoGatewayManagerTests
{
    [Fact]
    public void Constructor_WithNullPath_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new TapoGatewayManager(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithValidPath_ShouldSetProperties()
    {
        // Act
        var manager = new TapoGatewayManager("/path/to/script.py");

        // Assert
        manager.IsRunning.Should().BeFalse();
        manager.Port.Should().Be(0);
    }

    [Fact]
    public void BaseUrl_ShouldReflectPort()
    {
        // Arrange
        var manager = new TapoGatewayManager("/path/to/script.py");

        // Act & Assert (port is 0 until started)
        manager.BaseUrl.Should().Be("http://127.0.0.1:0");
    }

    [Fact]
    public void IsRunning_WhenNoProcessStarted_ShouldReturnFalse()
    {
        // Arrange
        var manager = new TapoGatewayManager("/path/to/script.py");

        // Act & Assert
        manager.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task StartAsync_WhenScriptDoesNotExist_ShouldReturnFalse()
    {
        // Arrange
        var manager = new TapoGatewayManager("/nonexistent/path/script.py");

        // Act
        var result = await manager.StartAsync("user", "pass", "server_pass", 9999);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task StopAsync_WhenNotRunning_ShouldCompleteWithoutError()
    {
        // Arrange
        var manager = new TapoGatewayManager("/path/to/script.py");

        // Act & Assert - should not throw
        await manager.StopAsync();
    }

    [Fact]
    public async Task DisposeAsync_ShouldCallStop()
    {
        // Arrange
        var manager = new TapoGatewayManager("/path/to/script.py");

        // Act & Assert - should not throw
        await manager.DisposeAsync();
    }

    [Fact]
    public async Task WaitForHealthAsync_WhenNoProcess_ShouldTimeoutAndReturnFalse()
    {
        // Arrange
        var manager = new TapoGatewayManager("/path/to/script.py");

        // Act - use a very short timeout
        var result = await manager.WaitForHealthAsync(TimeSpan.FromMilliseconds(100));

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task WaitForHealthAsync_WithCancellation_ShouldRespectToken()
    {
        // Arrange
        var manager = new TapoGatewayManager("/path/to/script.py");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        var act = () => manager.WaitForHealthAsync(TimeSpan.FromSeconds(10), cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
