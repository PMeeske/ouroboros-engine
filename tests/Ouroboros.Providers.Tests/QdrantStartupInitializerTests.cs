using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Ouroboros.Providers;
using Xunit;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class QdrantStartupInitializerTests
{
    [Fact]
    public void Ctor_WithNullRegistry_ThrowsArgumentNullException()
    {
        FluentActions.Invoking(() => new QdrantStartupInitializer(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Ctor_WithValidRegistry_DoesNotThrow()
    {
        var mockRegistry = new Mock<IQdrantCollectionRegistry>();
        FluentActions.Invoking(() => new QdrantStartupInitializer(mockRegistry.Object))
            .Should().NotThrow();
    }

    [Fact]
    public async Task StartAsync_CallsDiscoverAsync()
    {
        // Arrange
        var mockRegistry = new Mock<IQdrantCollectionRegistry>();
        mockRegistry.Setup(r => r.DiscoverAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockRegistry.Setup(r => r.GetAllMappings())
            .Returns(new Dictionary<string, string>());

        var sut = new QdrantStartupInitializer(mockRegistry.Object);

        // Act
        await sut.StartAsync(CancellationToken.None);

        // Assert
        mockRegistry.Verify(r => r.DiscoverAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartAsync_WithLogger_LogsInformation()
    {
        // Arrange
        var mockRegistry = new Mock<IQdrantCollectionRegistry>();
        mockRegistry.Setup(r => r.DiscoverAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockRegistry.Setup(r => r.GetAllMappings())
            .Returns(new Dictionary<string, string> { ["test"] = "collection_test" });

        var mockLogger = new Mock<ILogger<QdrantStartupInitializer>>();
        var sut = new QdrantStartupInitializer(mockRegistry.Object, mockLogger.Object);

        // Act
        await sut.StartAsync(CancellationToken.None);

        // Assert
        mockRegistry.Verify(r => r.DiscoverAsync(It.IsAny<CancellationToken>()), Times.Once);
        mockRegistry.Verify(r => r.GetAllMappings(), Times.Once);
    }

    [Fact]
    public async Task StartAsync_PassesCancellationToken()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var mockRegistry = new Mock<IQdrantCollectionRegistry>();
        mockRegistry.Setup(r => r.DiscoverAsync(cts.Token))
            .Returns(Task.CompletedTask);
        mockRegistry.Setup(r => r.GetAllMappings())
            .Returns(new Dictionary<string, string>());

        var sut = new QdrantStartupInitializer(mockRegistry.Object);

        // Act
        await sut.StartAsync(cts.Token);

        // Assert
        mockRegistry.Verify(r => r.DiscoverAsync(cts.Token), Times.Once);
    }

    [Fact]
    public async Task StopAsync_CompletesImmediately()
    {
        // Arrange
        var mockRegistry = new Mock<IQdrantCollectionRegistry>();
        var sut = new QdrantStartupInitializer(mockRegistry.Object);

        // Act
        var task = sut.StopAsync(CancellationToken.None);

        // Assert
        task.IsCompletedSuccessfully.Should().BeTrue();
        await task;
    }

    [Fact]
    public async Task StartAsync_WithNullLogger_DoesNotThrow()
    {
        // Arrange
        var mockRegistry = new Mock<IQdrantCollectionRegistry>();
        mockRegistry.Setup(r => r.DiscoverAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockRegistry.Setup(r => r.GetAllMappings())
            .Returns(new Dictionary<string, string>());

        var sut = new QdrantStartupInitializer(mockRegistry.Object, logger: null);

        // Act & Assert
        await FluentActions.Invoking(() => sut.StartAsync(CancellationToken.None))
            .Should().NotThrowAsync();
    }
}
