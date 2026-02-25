// <copyright file="QdrantHealthCheckProviderTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Database.HealthCheck;

using FluentAssertions;
using Ouroboros.Core.Infrastructure.HealthCheck;
using Xunit;

/// <summary>
/// Unit tests for QdrantHealthCheckProvider.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "Database")]
public sealed class QdrantHealthCheckProviderTests : IDisposable
{
    private QdrantHealthCheckProvider? _provider;

    public void Dispose()
    {
        _provider?.Dispose();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidEndpoint_SetsComponentName()
    {
        // Arrange & Act
        _provider = new QdrantHealthCheckProvider("http://localhost:6334");

        // Assert
        _provider.ComponentName.Should().Be("Qdrant");
    }

    [Fact]
    public void Constructor_WithNullEndpoint_ThrowsArgumentNullException()
    {
        // Act & Assert
        var action = () => new QdrantHealthCheckProvider(null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(30)]
    public void Constructor_WithCustomTimeout_AcceptsValue(int timeoutSeconds)
    {
        // Act - should not throw
        _provider = new QdrantHealthCheckProvider("http://localhost:6334", timeoutSeconds);

        // Assert
        _provider.Should().NotBeNull();
    }

    #endregion

    #region CheckHealthAsync Tests

    [Fact]
    public async Task CheckHealthAsync_WithUnreachableEndpoint_ReturnsUnhealthy()
    {
        // Arrange - use a port that won't be listening
        _provider = new QdrantHealthCheckProvider("http://localhost:59999", timeoutSeconds: 1);

        // Act
        var result = await _provider.CheckHealthAsync();

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.ComponentName.Should().Be("Qdrant");
        result.ResponseTime.Should().BeGreaterThan(-1);
        result.Details.Should().ContainKey("endpoint");
    }

    [Fact]
    public async Task CheckHealthAsync_WithCancellation_ReturnsUnhealthy()
    {
        // Arrange
        _provider = new QdrantHealthCheckProvider("http://localhost:6334", timeoutSeconds: 30);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await _provider.CheckHealthAsync(cts.Token);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CheckHealthAsync_ResultHasTimestamp()
    {
        // Arrange
        _provider = new QdrantHealthCheckProvider("http://localhost:59999", timeoutSeconds: 1);
        var beforeCheck = DateTime.UtcNow.AddSeconds(-1);

        // Act
        var result = await _provider.CheckHealthAsync();

        var afterCheck = DateTime.UtcNow.AddSeconds(1);

        // Assert
        result.Timestamp.Should().BeAfter(beforeCheck);
        result.Timestamp.Should().BeBefore(afterCheck);
    }

    [Fact]
    public async Task CheckHealthAsync_IncludesEndpointInDetails()
    {
        // Arrange
        var endpoint = "http://test-qdrant:6334";
        _provider = new QdrantHealthCheckProvider(endpoint, timeoutSeconds: 1);

        // Act
        var result = await _provider.CheckHealthAsync();

        // Assert
        result.Details.Should().ContainKey("endpoint");
        result.Details["endpoint"].Should().Be(endpoint);
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        _provider = new QdrantHealthCheckProvider("http://localhost:6334");

        // Act & Assert - should not throw
        _provider.Dispose();
        var action = () => _provider.Dispose();
        action.Should().NotThrow();
    }

    #endregion
}