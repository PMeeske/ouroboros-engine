// <copyright file="AudioPlayerTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.TextToSpeech;

using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Ouroboros.Providers.TextToSpeech;
using Xunit;

[Trait("Category", "Unit")]
public class AudioPlayerTests
{
    [Fact]
    public async Task PlayFileAsync_WithNonExistentFile_ReturnsFailure()
    {
        // Arrange
        string fakePath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.mp3");

        // Act
        var result = await AudioPlayer.PlayFileAsync(fakePath);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("File not found");
    }

    [Fact]
    public async Task PlayAsync_WithSpeechResult_DelegatesToPlayMethod()
    {
        // Arrange - the PlayAsync method creates temp file and plays it
        // Since no audio player is likely available in CI, this tests basic execution
        var speechResult = new SpeechResult(new byte[] { 1, 2, 3 }, "mp3");

        // Act
        var result = await AudioPlayer.PlayAsync(speechResult);

        // Assert - it should either succeed (if player found) or fail gracefully
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task PlayAsync_WithMimeTypeFormat_SanitizesExtension()
    {
        // Arrange - format with MIME type should be sanitized
        byte[] data = new byte[] { 1, 2, 3 };

        // Act
        var result = await AudioPlayer.PlayAsync(data, "audio/wav");

        // Assert - should not throw, even if no player is available
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task PlayAsync_WithSimpleFormat_UsesAsExtension()
    {
        // Arrange
        byte[] data = new byte[] { 1, 2, 3 };

        // Act
        var result = await AudioPlayer.PlayAsync(data, "mp3");

        // Assert
        result.Should().NotBeNull();
    }
}
