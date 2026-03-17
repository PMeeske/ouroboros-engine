// <copyright file="WhisperNetServiceTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.SpeechToText;

using System;
using FluentAssertions;
using Ouroboros.Providers.SpeechToText;
using Whisper.net.Ggml;
using Xunit;

[Trait("Category", "Unit")]
public class WhisperNetServiceTests
{
    [Fact]
    public void ProviderName_ReturnsWhisperNetNative()
    {
        // Arrange
        using var sut = new WhisperNetService();

        // Assert
        sut.ProviderName.Should().Be("Whisper.net (Native)");
    }

    [Fact]
    public void SupportedFormats_ContainsOnlyWav()
    {
        // Arrange
        using var sut = new WhisperNetService();

        // Assert
        sut.SupportedFormats.Should().HaveCount(1);
        sut.SupportedFormats.Should().Contain(".wav");
    }

    [Fact]
    public void MaxFileSizeBytes_Returns500MB()
    {
        // Arrange
        using var sut = new WhisperNetService();

        // Assert
        sut.MaxFileSizeBytes.Should().Be(500 * 1024 * 1024);
    }

    [Fact]
    public void Constructor_WithDefaults_DoesNotThrow()
    {
        // Act
        Action act = () =>
        {
            using var _ = new WhisperNetService();
        };

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithCustomModelType_DoesNotThrow()
    {
        // Act
        Action act = () =>
        {
            using var _ = new WhisperNetService(GgmlType.Tiny, lazyLoad: true);
        };

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("tiny", GgmlType.Tiny)]
    [InlineData("base", GgmlType.Base)]
    [InlineData("small", GgmlType.Small)]
    [InlineData("medium", GgmlType.Medium)]
    [InlineData("large", GgmlType.LargeV1)]
    [InlineData("large-v2", GgmlType.LargeV2)]
    [InlineData("large-v3", GgmlType.LargeV3)]
    [InlineData("unknown", GgmlType.Base)]
    public void FromModelSize_MapsCorrectly(string modelSize, GgmlType _)
    {
        // Act
        using var service = WhisperNetService.FromModelSize(modelSize);

        // Assert
        service.Should().NotBeNull();
        service.ProviderName.Should().Be("Whisper.net (Native)");
    }

    [Fact]
    public async Task TranscribeFileAsync_WithNonExistentFile_ReturnsFailure()
    {
        // Arrange
        using var sut = new WhisperNetService(lazyLoad: true);
        string fakePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.wav");

        // Act
        var result = await sut.TranscribeFileAsync(fakePath);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("File not found");
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var sut = new WhisperNetService();

        // Act
        Action act = () =>
        {
            sut.Dispose();
            sut.Dispose();
        };

        // Assert
        act.Should().NotThrow();
    }
}
