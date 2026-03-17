// <copyright file="TextToSpeechToolTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.TextToSpeech;

using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Ouroboros.Core.Monads;
using Ouroboros.Providers;
using Ouroboros.Providers.TextToSpeech;
using Xunit;

[Trait("Category", "Unit")]
public class TextToSpeechToolTests
{
    private readonly Mock<ITextToSpeechService> _mockService;
    private readonly TextToSpeechTool _sut;

    public TextToSpeechToolTests()
    {
        _mockService = new Mock<ITextToSpeechService>();
        _sut = new TextToSpeechTool(_mockService.Object);
    }

    [Fact]
    public void Constructor_WithNullService_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new TextToSpeechTool(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Name_ReturnsTextToSpeech()
    {
        // Assert
        _sut.Name.Should().Be("text_to_speech");
    }

    [Fact]
    public void Description_IsNotEmpty()
    {
        // Assert
        _sut.Description.Should().NotBeNullOrEmpty();
        _sut.Description.Should().Contain("speech");
    }

    [Fact]
    public void JsonSchema_ContainsExpectedProperties()
    {
        // Assert
        _sut.JsonSchema.Should().NotBeNull();
        _sut.JsonSchema.Should().Contain("text");
        _sut.JsonSchema.Should().Contain("output_path");
        _sut.JsonSchema.Should().Contain("voice");
        _sut.JsonSchema.Should().Contain("speed");
    }

    [Fact]
    public async Task InvokeAsync_WithMissingText_ReturnsFailure()
    {
        // Arrange
        string input = """{"output_path": "/tmp/output.mp3"}""";

        // Act
        var result = await _sut.InvokeAsync(input);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("text is required");
    }

    [Fact]
    public async Task InvokeAsync_WithMissingOutputPath_ReturnsFailure()
    {
        // Arrange
        string input = """{"text": "Hello world"}""";

        // Act
        var result = await _sut.InvokeAsync(input);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("output_path is required");
    }

    [Fact]
    public async Task InvokeAsync_WithValidInput_CallsSynthesizeToFileAsync()
    {
        // Arrange
        string outputPath = "/tmp/test_output.mp3";
        _mockService
            .Setup(s => s.SynthesizeToFileAsync(
                "Hello world",
                outputPath,
                It.IsAny<TextToSpeechOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string, string>.Success(outputPath));

        string input = $@"{{""text"": ""Hello world"", ""output_path"": ""{outputPath}""}}";

        // Act
        var result = await _sut.InvokeAsync(input);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("Audio saved to");
        result.Value.Should().Contain(outputPath);
    }

    [Fact]
    public async Task InvokeAsync_WithAlternateOutputPathKey_ParsesCorrectly()
    {
        // Arrange - test "outputPath" key
        string outputPath = "/tmp/test_output.mp3";
        _mockService
            .Setup(s => s.SynthesizeToFileAsync(
                "Hello",
                outputPath,
                It.IsAny<TextToSpeechOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string, string>.Success(outputPath));

        string input = $@"{{""text"": ""Hello"", ""outputPath"": ""{outputPath}""}}";

        // Act
        var result = await _sut.InvokeAsync(input);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WithPathKey_ParsesCorrectly()
    {
        // Arrange - test "path" key
        string outputPath = "/tmp/test_output.mp3";
        _mockService
            .Setup(s => s.SynthesizeToFileAsync(
                "Hello",
                outputPath,
                It.IsAny<TextToSpeechOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string, string>.Success(outputPath));

        string input = $@"{{""text"": ""Hello"", ""path"": ""{outputPath}""}}";

        // Act
        var result = await _sut.InvokeAsync(input);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("alloy", TtsVoice.Alloy)]
    [InlineData("echo", TtsVoice.Echo)]
    [InlineData("fable", TtsVoice.Fable)]
    [InlineData("onyx", TtsVoice.Onyx)]
    [InlineData("nova", TtsVoice.Nova)]
    [InlineData("shimmer", TtsVoice.Shimmer)]
    public async Task InvokeAsync_WithVoice_PassesCorrectVoice(string voiceName, TtsVoice expectedVoice)
    {
        // Arrange
        TextToSpeechOptions? capturedOptions = null;
        _mockService
            .Setup(s => s.SynthesizeToFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<TextToSpeechOptions?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, TextToSpeechOptions?, CancellationToken>((_, _, opts, _) => capturedOptions = opts)
            .ReturnsAsync(Result<string, string>.Success("/tmp/out.mp3"));

        string input = $@"{{""text"": ""test"", ""output_path"": ""/tmp/out.mp3"", ""voice"": ""{voiceName}""}}";

        // Act
        await _sut.InvokeAsync(input);

        // Assert
        capturedOptions.Should().NotBeNull();
        capturedOptions!.Voice.Should().Be(expectedVoice);
    }

    [Fact]
    public async Task InvokeAsync_WithUnknownVoice_DefaultsToAlloy()
    {
        // Arrange
        TextToSpeechOptions? capturedOptions = null;
        _mockService
            .Setup(s => s.SynthesizeToFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<TextToSpeechOptions?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, TextToSpeechOptions?, CancellationToken>((_, _, opts, _) => capturedOptions = opts)
            .ReturnsAsync(Result<string, string>.Success("/tmp/out.mp3"));

        string input = """{"text": "test", "output_path": "/tmp/out.mp3", "voice": "unknown_voice"}""";

        // Act
        await _sut.InvokeAsync(input);

        // Assert
        capturedOptions.Should().NotBeNull();
        capturedOptions!.Voice.Should().Be(TtsVoice.Alloy);
    }

    [Fact]
    public async Task InvokeAsync_WithNoVoice_DefaultsToAlloy()
    {
        // Arrange
        TextToSpeechOptions? capturedOptions = null;
        _mockService
            .Setup(s => s.SynthesizeToFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<TextToSpeechOptions?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, TextToSpeechOptions?, CancellationToken>((_, _, opts, _) => capturedOptions = opts)
            .ReturnsAsync(Result<string, string>.Success("/tmp/out.mp3"));

        string input = """{"text": "test", "output_path": "/tmp/out.mp3"}""";

        // Act
        await _sut.InvokeAsync(input);

        // Assert
        capturedOptions.Should().NotBeNull();
        capturedOptions!.Voice.Should().Be(TtsVoice.Alloy);
    }

    [Fact]
    public async Task InvokeAsync_WithSpeed_PassesSpeedToOptions()
    {
        // Arrange
        TextToSpeechOptions? capturedOptions = null;
        _mockService
            .Setup(s => s.SynthesizeToFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<TextToSpeechOptions?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, TextToSpeechOptions?, CancellationToken>((_, _, opts, _) => capturedOptions = opts)
            .ReturnsAsync(Result<string, string>.Success("/tmp/out.mp3"));

        string input = """{"text": "test", "output_path": "/tmp/out.mp3", "speed": 1.5}""";

        // Act
        await _sut.InvokeAsync(input);

        // Assert
        capturedOptions.Should().NotBeNull();
        capturedOptions!.Speed.Should().BeApproximately(1.5, 0.01);
    }

    [Fact]
    public async Task InvokeAsync_WithDefaultSpeed_UsesOnePointZero()
    {
        // Arrange
        TextToSpeechOptions? capturedOptions = null;
        _mockService
            .Setup(s => s.SynthesizeToFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<TextToSpeechOptions?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, TextToSpeechOptions?, CancellationToken>((_, _, opts, _) => capturedOptions = opts)
            .ReturnsAsync(Result<string, string>.Success("/tmp/out.mp3"));

        string input = """{"text": "test", "output_path": "/tmp/out.mp3"}""";

        // Act
        await _sut.InvokeAsync(input);

        // Assert
        capturedOptions.Should().NotBeNull();
        capturedOptions!.Speed.Should().BeApproximately(1.0, 0.01);
    }

    [Fact]
    public async Task InvokeAsync_WhenServiceReturnsFailure_ReturnsFailure()
    {
        // Arrange
        _mockService
            .Setup(s => s.SynthesizeToFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<TextToSpeechOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string, string>.Failure("Service error"));

        string input = """{"text": "test", "output_path": "/tmp/out.mp3"}""";

        // Act
        var result = await _sut.InvokeAsync(input);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Service error");
    }

    [Fact]
    public async Task InvokeAsync_WithPlainTextInput_ParsesTextButMissingOutputPath()
    {
        // Arrange - non-JSON input
        string input = "Hello world";

        // Act
        var result = await _sut.InvokeAsync(input);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("output_path is required");
    }

    [Fact]
    public async Task InvokeAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockService
            .Setup(s => s.SynthesizeToFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<TextToSpeechOptions?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        string input = """{"text": "test", "output_path": "/tmp/out.mp3"}""";

        // Act
        Func<Task> act = () => _sut.InvokeAsync(input, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task InvokeAsync_WhenServiceThrows_ReturnsFailure()
    {
        // Arrange
        _mockService
            .Setup(s => s.SynthesizeToFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<TextToSpeechOptions?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Something broke"));

        string input = """{"text": "test", "output_path": "/tmp/out.mp3"}""";

        // Act
        var result = await _sut.InvokeAsync(input);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Speech synthesis failed");
    }
}
