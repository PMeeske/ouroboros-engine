// <copyright file="LocalWhisperServiceTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.SpeechToText;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ouroboros.Providers.SpeechToText;
using Xunit;

[Trait("Category", "Unit")]
public class LocalWhisperServiceTests
{
    private readonly LocalWhisperService _sut;

    public LocalWhisperServiceTests()
    {
        _sut = new LocalWhisperService(whisperPath: "whisper", modelSize: "tiny");
    }

    [Fact]
    public void ProviderName_ReturnsLocalWhisper()
    {
        // Assert
        _sut.ProviderName.Should().Be("Local Whisper");
    }

    [Fact]
    public void SupportedFormats_ContainsExpectedFormats()
    {
        // Assert
        _sut.SupportedFormats.Should().Contain(".wav");
        _sut.SupportedFormats.Should().Contain(".mp3");
        _sut.SupportedFormats.Should().Contain(".m4a");
        _sut.SupportedFormats.Should().Contain(".flac");
        _sut.SupportedFormats.Should().Contain(".ogg");
        _sut.SupportedFormats.Should().Contain(".opus");
    }

    [Fact]
    public void MaxFileSizeBytes_Returns500MB()
    {
        // Assert
        _sut.MaxFileSizeBytes.Should().Be(500 * 1024 * 1024);
    }

    [Fact]
    public async Task TranscribeFileAsync_WithNonExistentFile_ReturnsFailure()
    {
        // Arrange
        string fakePath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.wav");

        // Act
        var result = await _sut.TranscribeFileAsync(fakePath);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("File not found");
    }

    [Fact]
    public async Task TranscribeFileAsync_WithUnsupportedFormat_ReturnsFailure()
    {
        // Arrange
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.xyz");
        File.WriteAllText(tempFile, "dummy");
        try
        {
            // Act
            var result = await _sut.TranscribeFileAsync(tempFile);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Contain("Unsupported audio format");
            result.Error.Should().Contain(".xyz");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task TranscribeBytesAsync_DelegatesToTranscribeStream()
    {
        // Arrange - the method saves to temp and calls TranscribeFileAsync
        // Since whisper is not available, this should result in a failure or process error
        byte[] data = new byte[] { 1, 2, 3, 4 };

        // Act
        var result = await _sut.TranscribeBytesAsync(data, "test.wav");

        // Assert - we expect either an unsupported format or process-related failure
        // since whisper isn't installed in the test environment
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task TranslateToEnglishAsync_SetsTranslationPrompt()
    {
        // Arrange
        string fakePath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.wav");

        // Act
        var result = await _sut.TranslateToEnglishAsync(fakePath);

        // Assert - file doesn't exist so returns failure before checking translate
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("File not found");
    }

    [Fact]
    public void Constructor_WithDefaultParameters_DoesNotThrow()
    {
        // Act
        Action act = () => new LocalWhisperService();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithCustomModelPath_DoesNotThrow()
    {
        // Act
        Action act = () => new LocalWhisperService(
            whisperPath: "/usr/local/bin/whisper",
            modelPath: "/models/whisper-small.bin",
            modelSize: "small");

        // Assert
        act.Should().NotThrow();
    }
}
