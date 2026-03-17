// <copyright file="LocalWindowsTtsServiceTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.TextToSpeech;

using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using FluentAssertions;
using Ouroboros.Providers.TextToSpeech;
using Xunit;

[Trait("Category", "Unit")]
public class LocalWindowsTtsServiceTests
{
    [Fact]
    public void ProviderName_ReturnsExpectedValue()
    {
        // Arrange
        var sut = new LocalWindowsTtsService();

        // Assert
        sut.ProviderName.Should().Be("Windows SAPI (Local)");
    }

    [Fact]
    public void AvailableVoices_ContainsExpectedVoices()
    {
        // Arrange
        var sut = new LocalWindowsTtsService();

        // Assert
        sut.AvailableVoices.Should().Contain("Microsoft David");
        sut.AvailableVoices.Should().Contain("Microsoft Zira");
    }

    [Fact]
    public void SupportedFormats_ContainsWav()
    {
        // Arrange
        var sut = new LocalWindowsTtsService();

        // Assert
        sut.SupportedFormats.Should().Contain("wav");
    }

    [Fact]
    public void MaxInputLength_Returns32000()
    {
        // Arrange
        var sut = new LocalWindowsTtsService();

        // Assert
        sut.MaxInputLength.Should().Be(32000);
    }

    [Fact]
    public void IsAvailable_ReturnsBasedOnPlatform()
    {
        // Act
        bool result = LocalWindowsTtsService.IsAvailable();

        // Assert
        result.Should().Be(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
    }

    [Fact]
    public async Task IsAvailableAsync_ReturnsBasedOnPlatform()
    {
        // Arrange
        var sut = new LocalWindowsTtsService();

        // Act
        bool result = await sut.IsAvailableAsync();

        // Assert
        result.Should().Be(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
    }

    [Fact]
    public void Constructor_WithDefaultParameters_DoesNotThrow()
    {
        // Act
        Action act = () => new LocalWindowsTtsService();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithCustomParameters_ClampsRate()
    {
        // Act - rate out of range should be clamped, not throw
        Action act = () => new LocalWindowsTtsService(rate: -15, volume: 150);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithCustomVoice_DoesNotThrow()
    {
        // Act
        Action act = () => new LocalWindowsTtsService(voiceName: "Microsoft David");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task SynthesizeAsync_WithEmptyText_ReturnsFailure()
    {
        // Arrange
        var sut = new LocalWindowsTtsService();

        // Act
        var result = await sut.SynthesizeAsync("");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "Text cannot be empty"
            : "Windows TTS only available on Windows");
    }

    [Fact]
    public async Task SynthesizeAsync_OnNonWindows_ReturnsFailure()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return; // Skip on Windows - it would actually try to synthesize
        }

        // Arrange
        var sut = new LocalWindowsTtsService();

        // Act
        var result = await sut.SynthesizeAsync("Hello world");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Windows TTS only available on Windows");
    }

    [Fact]
    public async Task SynthesizeToFileAsync_OnNonWindows_ReturnsFailure()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return; // Skip on Windows
        }

        // Arrange
        var sut = new LocalWindowsTtsService();

        // Act
        var result = await sut.SynthesizeToFileAsync("Hello", "/tmp/test.wav");

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task SynthesizeToStreamAsync_OnNonWindows_ReturnsFailure()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return; // Skip on Windows
        }

        // Arrange
        var sut = new LocalWindowsTtsService();
        using var stream = new System.IO.MemoryStream();

        // Act
        var result = await sut.SynthesizeToStreamAsync("Hello", stream);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task SpeakWithToneAsync_OnNonWindows_ReturnsFailure()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return; // Skip on Windows
        }

        // Act
        var result = await LocalWindowsTtsService.SpeakWithToneAsync("Hello", 0, 100);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Windows TTS only available on Windows");
    }

    [Fact]
    public async Task ListVoicesAsync_OnNonWindows_ReturnsFailure()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return; // Skip on Windows
        }

        // Act
        var result = await LocalWindowsTtsService.ListVoicesAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Windows TTS only available on Windows");
    }
}
