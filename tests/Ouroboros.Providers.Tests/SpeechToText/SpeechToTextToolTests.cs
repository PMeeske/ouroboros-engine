// <copyright file="SpeechToTextToolTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.SpeechToText;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Ouroboros.Core.Monads;
using Ouroboros.Providers;
using Ouroboros.Providers.SpeechToText;
using Xunit;

[Trait("Category", "Unit")]
public class SpeechToTextToolTests
{
    private readonly Mock<ISpeechToTextService> _mockService;
    private readonly SpeechToTextTool _sut;

    public SpeechToTextToolTests()
    {
        _mockService = new Mock<ISpeechToTextService>();
        _sut = new SpeechToTextTool(_mockService.Object);
    }

    [Fact]
    public void Constructor_WithNullService_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new SpeechToTextTool(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Name_ReturnsTranscribeAudio()
    {
        // Assert
        _sut.Name.Should().Be("transcribe_audio");
    }

    [Fact]
    public void Description_IsNotEmpty()
    {
        // Assert
        _sut.Description.Should().NotBeNullOrEmpty();
        _sut.Description.Should().Contain("Transcribe");
    }

    [Fact]
    public void JsonSchema_ContainsFilePathProperty()
    {
        // Assert
        _sut.JsonSchema.Should().NotBeNull();
        _sut.JsonSchema.Should().Contain("file_path");
        _sut.JsonSchema.Should().Contain("language");
        _sut.JsonSchema.Should().Contain("translate_to_english");
    }

    [Fact]
    public async Task InvokeAsync_WithEmptyFilePath_ReturnsFailure()
    {
        // Arrange
        string input = """{"file_path": ""}""";

        // Act
        var result = await _sut.InvokeAsync(input);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("file_path is required");
    }

    [Fact]
    public async Task InvokeAsync_WithMissingFilePath_ReturnsFailure()
    {
        // Arrange
        string input = """{"language": "en"}""";

        // Act
        var result = await _sut.InvokeAsync(input);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("file_path is required");
    }

    [Fact]
    public async Task InvokeAsync_WithNonExistentFile_ReturnsFailure()
    {
        // Arrange
        string fakePath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.wav");
        string input = $@"{{""file_path"": ""{fakePath.Replace("\\", "\\\\")}""}}";

        // Act
        var result = await _sut.InvokeAsync(input);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("File not found");
    }

    [Fact]
    public async Task InvokeAsync_WithValidFile_CallsTranscribeFileAsync()
    {
        // Arrange
        string tempFile = Path.GetTempFileName();
        try
        {
            var transcriptionResult = new TranscriptionResult("Hello world");
            _mockService
                .Setup(s => s.TranscribeFileAsync(tempFile, It.IsAny<TranscriptionOptions?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<TranscriptionResult, string>.Success(transcriptionResult));

            string input = $@"{{""file_path"": ""{tempFile.Replace("\\", "\\\\")}""}}";

            // Act
            var result = await _sut.InvokeAsync(input);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be("Hello world");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task InvokeAsync_WithLanguage_PassesLanguageToOptions()
    {
        // Arrange
        string tempFile = Path.GetTempFileName();
        try
        {
            TranscriptionOptions? capturedOptions = null;
            _mockService
                .Setup(s => s.TranscribeFileAsync(tempFile, It.IsAny<TranscriptionOptions?>(), It.IsAny<CancellationToken>()))
                .Callback<string, TranscriptionOptions?, CancellationToken>((_, opts, _) => capturedOptions = opts)
                .ReturnsAsync(Result<TranscriptionResult, string>.Success(new TranscriptionResult("Bonjour")));

            string input = $@"{{""file_path"": ""{tempFile.Replace("\\", "\\\\")}""  , ""language"": ""fr""}}";

            // Act
            await _sut.InvokeAsync(input);

            // Assert
            capturedOptions.Should().NotBeNull();
            capturedOptions!.Language.Should().Be("fr");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task InvokeAsync_WithTranslateTrue_CallsTranslateToEnglishAsync()
    {
        // Arrange
        string tempFile = Path.GetTempFileName();
        try
        {
            _mockService
                .Setup(s => s.TranslateToEnglishAsync(tempFile, It.IsAny<TranscriptionOptions?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<TranscriptionResult, string>.Success(new TranscriptionResult("Hello")));

            string input = $@"{{""file_path"": ""{tempFile.Replace("\\", "\\\\")}""  , ""translate_to_english"": true}}";

            // Act
            var result = await _sut.InvokeAsync(input);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be("Hello");
            _mockService.Verify(s => s.TranslateToEnglishAsync(tempFile, It.IsAny<TranscriptionOptions?>(), It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task InvokeAsync_WithAlternateTranslateKey_CallsTranslateToEnglishAsync()
    {
        // Arrange
        string tempFile = Path.GetTempFileName();
        try
        {
            _mockService
                .Setup(s => s.TranslateToEnglishAsync(tempFile, It.IsAny<TranscriptionOptions?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<TranscriptionResult, string>.Success(new TranscriptionResult("Hello")));

            string input = $@"{{""file_path"": ""{tempFile.Replace("\\", "\\\\")}""  , ""translate"": true}}";

            // Act
            var result = await _sut.InvokeAsync(input);

            // Assert
            result.IsSuccess.Should().BeTrue();
            _mockService.Verify(s => s.TranslateToEnglishAsync(It.IsAny<string>(), It.IsAny<TranscriptionOptions?>(), It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task InvokeAsync_WithAlternateFilePathKeys_ParsesCorrectly()
    {
        // Arrange - test "filePath" key
        string tempFile = Path.GetTempFileName();
        try
        {
            _mockService
                .Setup(s => s.TranscribeFileAsync(tempFile, It.IsAny<TranscriptionOptions?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<TranscriptionResult, string>.Success(new TranscriptionResult("test")));

            string input = $@"{{""filePath"": ""{tempFile.Replace("\\", "\\\\")}""}}";

            // Act
            var result = await _sut.InvokeAsync(input);

            // Assert
            result.IsSuccess.Should().BeTrue();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task InvokeAsync_WithPathKey_ParsesCorrectly()
    {
        // Arrange - test "path" key
        string tempFile = Path.GetTempFileName();
        try
        {
            _mockService
                .Setup(s => s.TranscribeFileAsync(tempFile, It.IsAny<TranscriptionOptions?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<TranscriptionResult, string>.Success(new TranscriptionResult("test")));

            string input = $@"{{""path"": ""{tempFile.Replace("\\", "\\\\")}""}}";

            // Act
            var result = await _sut.InvokeAsync(input);

            // Assert
            result.IsSuccess.Should().BeTrue();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task InvokeAsync_WithPlainTextInput_TreatsAsFilePath()
    {
        // Arrange - non-JSON input should be treated as a file path
        string fakePath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.wav");

        // Act
        var result = await _sut.InvokeAsync(fakePath);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("File not found");
    }

    [Fact]
    public async Task InvokeAsync_WithPlainTextQuoted_StripsQuotes()
    {
        // Arrange
        string tempFile = Path.GetTempFileName();
        try
        {
            _mockService
                .Setup(s => s.TranscribeFileAsync(tempFile, It.IsAny<TranscriptionOptions?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<TranscriptionResult, string>.Success(new TranscriptionResult("test")));

            string input = $"\"{tempFile}\"";

            // Act
            var result = await _sut.InvokeAsync(input);

            // Assert
            result.IsSuccess.Should().BeTrue();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task InvokeAsync_WhenServiceReturnsFailure_ReturnsFailure()
    {
        // Arrange
        string tempFile = Path.GetTempFileName();
        try
        {
            _mockService
                .Setup(s => s.TranscribeFileAsync(tempFile, It.IsAny<TranscriptionOptions?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<TranscriptionResult, string>.Failure("Service error"));

            string input = $@"{{""file_path"": ""{tempFile.Replace("\\", "\\\\")}""}}";

            // Act
            var result = await _sut.InvokeAsync(input);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Be("Service error");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task InvokeAsync_WhenServiceThrowsInvalidOperation_ReturnsFailure()
    {
        // Arrange
        string tempFile = Path.GetTempFileName();
        try
        {
            _mockService
                .Setup(s => s.TranscribeFileAsync(tempFile, It.IsAny<TranscriptionOptions?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Something broke"));

            string input = $@"{{""file_path"": ""{tempFile.Replace("\\", "\\\\")}""}}";

            // Act
            var result = await _sut.InvokeAsync(input);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Contain("Transcription failed");
            result.Error.Should().Contain("Something broke");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task InvokeAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        string tempFile = Path.GetTempFileName();
        try
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            _mockService
                .Setup(s => s.TranscribeFileAsync(tempFile, It.IsAny<TranscriptionOptions?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());

            string input = $@"{{""file_path"": ""{tempFile.Replace("\\", "\\\\")}""}}";

            // Act
            Func<Task> act = () => _sut.InvokeAsync(input, cts.Token);

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void CreateWithLocalWhisper_ReturnsValidTool()
    {
        // Act
        var tool = SpeechToTextTool.CreateWithLocalWhisper("tiny");

        // Assert
        tool.Should().NotBeNull();
        tool.Name.Should().Be("transcribe_audio");
    }
}
