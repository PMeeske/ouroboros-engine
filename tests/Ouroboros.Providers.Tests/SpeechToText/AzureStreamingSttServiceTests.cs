// <copyright file="AzureStreamingSttServiceTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.SpeechToText;

using System;
using System.Threading.Tasks;
using FluentAssertions;
using Ouroboros.Providers.SpeechToText;
using Xunit;

[Trait("Category", "Unit")]
public class AzureStreamingSttServiceTests
{
    [Fact]
    public void ProviderName_ReturnsExpectedValue()
    {
        // Arrange
        using var sut = new AzureStreamingSttService("fake-key", "eastus");

        // Assert
        sut.ProviderName.Should().Be("Azure Speech (Streaming)");
    }

    [Fact]
    public void SupportedFormats_ContainsExpectedFormats()
    {
        // Arrange
        using var sut = new AzureStreamingSttService("fake-key", "eastus");

        // Assert
        sut.SupportedFormats.Should().Contain(".wav");
        sut.SupportedFormats.Should().Contain(".pcm");
    }

    [Fact]
    public void MaxFileSizeBytes_Returns500MB()
    {
        // Arrange
        using var sut = new AzureStreamingSttService("fake-key", "eastus");

        // Assert
        sut.MaxFileSizeBytes.Should().Be(500 * 1024 * 1024);
    }

    [Fact]
    public void SupportsStreaming_ReturnsTrue()
    {
        // Arrange
        using var sut = new AzureStreamingSttService("fake-key", "eastus");

        // Assert
        sut.SupportsStreaming.Should().BeTrue();
    }

    [Fact]
    public void SupportsVoiceActivityDetection_ReturnsTrue()
    {
        // Arrange
        using var sut = new AzureStreamingSttService("fake-key", "eastus");

        // Assert
        sut.SupportsVoiceActivityDetection.Should().BeTrue();
    }

    [Fact]
    public async Task IsAvailableAsync_WithNonEmptyKey_ReturnsTrue()
    {
        // Arrange
        using var sut = new AzureStreamingSttService("test-key", "eastus");

        // Act
        bool available = await sut.IsAvailableAsync();

        // Assert
        available.Should().BeTrue();
    }

    [Fact]
    public async Task IsAvailableAsync_WithEmptyKey_ReturnsFalse()
    {
        // Arrange
        using var sut = new AzureStreamingSttService("", "eastus");

        // Act
        bool available = await sut.IsAvailableAsync();

        // Assert
        available.Should().BeFalse();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var sut = new AzureStreamingSttService("fake-key", "eastus");

        // Act
        Action act = () =>
        {
            sut.Dispose();
            sut.Dispose();
        };

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithCustomLanguage_DoesNotThrow()
    {
        // Act
        Action act = () =>
        {
            using var _ = new AzureStreamingSttService("fake-key", "westeurope", "de-DE");
        };

        // Assert
        act.Should().NotThrow();
    }
}
