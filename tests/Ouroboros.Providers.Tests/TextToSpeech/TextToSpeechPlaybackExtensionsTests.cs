// <copyright file="TextToSpeechPlaybackExtensionsTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.TextToSpeech;

using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Ouroboros.Core.Monads;
using Ouroboros.Providers.TextToSpeech;
using Xunit;

[Trait("Category", "Unit")]
public class TextToSpeechPlaybackExtensionsTests
{
    [Fact]
    public async Task SpeakAsync_WhenSynthesisFails_ReturnsFailure()
    {
        // Arrange
        var mockService = new Mock<ITextToSpeechService>();
        mockService
            .Setup(s => s.SynthesizeAsync(
                It.IsAny<string>(),
                It.IsAny<TextToSpeechOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SpeechResult, string>.Failure("Synthesis failed"));

        // Act
        var result = await mockService.Object.SpeakAsync("Hello world");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Synthesis failed");
    }

    [Fact]
    public async Task SpeakAsync_WithNullError_ReturnsDefaultMessage()
    {
        // Arrange
        var mockService = new Mock<ITextToSpeechService>();
        mockService
            .Setup(s => s.SynthesizeAsync(
                It.IsAny<string>(),
                It.IsAny<TextToSpeechOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SpeechResult, string>.Failure(null!));

        // Act
        var result = await mockService.Object.SpeakAsync("Hello world");

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task SpeakAsync_PassesOptionsToService()
    {
        // Arrange
        var mockService = new Mock<ITextToSpeechService>();
        var options = new TextToSpeechOptions(Voice: TtsVoice.Nova, Speed: 1.5);
        mockService
            .Setup(s => s.SynthesizeAsync(
                "Hello",
                options,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SpeechResult, string>.Failure("not implemented"));

        // Act
        await mockService.Object.SpeakAsync("Hello", options);

        // Assert
        mockService.Verify(s => s.SynthesizeAsync("Hello", options, It.IsAny<CancellationToken>()), Times.Once);
    }
}
