// <copyright file="WhisperSpeechToTextServiceTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.SpeechToText;

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Ouroboros.Providers.SpeechToText;
using Xunit;

[Trait("Category", "Unit")]
public class WhisperSpeechToTextServiceTests : IDisposable
{
    private readonly Mock<HttpMessageHandler> _mockHandler;
    private readonly HttpClient _httpClient;
    private readonly WhisperSpeechToTextService _sut;

    public WhisperSpeechToTextServiceTests()
    {
        _mockHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHandler.Object);
        _sut = new WhisperSpeechToTextService("test-api-key", httpClient: _httpClient);
    }

    public void Dispose()
    {
        _sut.Dispose();
        _httpClient.Dispose();
    }

    [Fact]
    public void Constructor_WithNullApiKey_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new WhisperSpeechToTextService(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ProviderName_ReturnsOpenAIWhisper()
    {
        // Assert
        _sut.ProviderName.Should().Be("OpenAI Whisper");
    }

    [Fact]
    public void SupportedFormats_ContainsExpectedFormats()
    {
        // Assert
        _sut.SupportedFormats.Should().Contain(".mp3");
        _sut.SupportedFormats.Should().Contain(".wav");
        _sut.SupportedFormats.Should().Contain(".flac");
        _sut.SupportedFormats.Should().Contain(".m4a");
        _sut.SupportedFormats.Should().Contain(".ogg");
        _sut.SupportedFormats.Should().Contain(".webm");
    }

    [Fact]
    public void MaxFileSizeBytes_Returns25MB()
    {
        // Assert
        _sut.MaxFileSizeBytes.Should().Be(25 * 1024 * 1024);
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
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task TranscribeFileAsync_WithTooLargeFile_ReturnsFailure()
    {
        // Arrange - create a sparse temp file that appears to be large
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.wav");
        using (var fs = File.Create(tempFile))
        {
            fs.SetLength(26 * 1024 * 1024); // 26MB > 25MB limit
        }

        try
        {
            // Act
            var result = await _sut.TranscribeFileAsync(tempFile);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Contain("File too large");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task TranscribeStreamAsync_WithSuccessfulResponse_ReturnsTranscription()
    {
        // Arrange
        string responseJson = """
        {
            "text": "Hello world",
            "language": "en",
            "duration": 2.5,
            "segments": [
                {"id": 0, "start": 0.0, "end": 2.5, "text": "Hello world"}
            ]
        }
        """;

        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson)
            });

        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });

        // Act
        var result = await _sut.TranscribeStreamAsync(stream, "test.wav");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Text.Should().Be("Hello world");
        result.Value.Language.Should().Be("en");
    }

    [Fact]
    public async Task TranscribeStreamAsync_WithApiError_ReturnsFailure()
    {
        // Arrange
        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Unauthorized,
                Content = new StringContent("Invalid API key")
            });

        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });

        // Act
        var result = await _sut.TranscribeStreamAsync(stream, "test.wav");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Whisper API error");
        result.Error.Should().Contain("Unauthorized");
    }

    [Fact]
    public async Task TranscribeStreamAsync_WithTextFormat_ReturnsPlainText()
    {
        // Arrange
        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("Hello world")
            });

        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var options = new TranscriptionOptions(ResponseFormat: "text");

        // Act
        var result = await _sut.TranscribeStreamAsync(stream, "test.wav", options);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Text.Should().Be("Hello world");
    }

    [Fact]
    public async Task TranscribeBytesAsync_DelegatesToTranscribeStream()
    {
        // Arrange
        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("""{"text": "test", "language": "en"}""")
            });

        // Act
        var result = await _sut.TranscribeBytesAsync(new byte[] { 1, 2, 3 }, "test.wav");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Text.Should().Be("test");
    }

    [Fact]
    public async Task TranslateToEnglishAsync_WithNonExistentFile_ReturnsFailure()
    {
        // Arrange
        string fakePath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.wav");

        // Act
        var result = await _sut.TranslateToEnglishAsync(fakePath);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("File not found");
    }

    [Fact]
    public async Task IsAvailableAsync_WithValidKey_ReturnsSuccessBasedOnApiResponse()
    {
        // Arrange
        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK
            });

        // Act
        var available = await _sut.IsAvailableAsync();

        // Assert
        available.Should().BeTrue();
    }

    [Fact]
    public async Task IsAvailableAsync_WithHttpError_ReturnsFalse()
    {
        // Arrange
        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        var available = await _sut.IsAvailableAsync();

        // Assert
        available.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithCustomEndpointAndModel_DoesNotThrow()
    {
        // Act
        Action act = () =>
        {
            using var svc = new WhisperSpeechToTextService(
                "key",
                endpoint: "https://custom.api.com/v1",
                model: "whisper-2");
        };

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var svc = new WhisperSpeechToTextService("key");

        // Act
        Action act = () =>
        {
            svc.Dispose();
            svc.Dispose();
        };

        // Assert
        act.Should().NotThrow();
    }
}
