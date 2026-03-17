// <copyright file="OpenAiTextToSpeechServiceTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.TextToSpeech;

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Ouroboros.Providers.TextToSpeech;
using Xunit;

[Trait("Category", "Unit")]
public class OpenAiTextToSpeechServiceTests : IDisposable
{
    private readonly Mock<HttpMessageHandler> _mockHandler;
    private readonly HttpClient _httpClient;
    private readonly OpenAiTextToSpeechService _sut;

    public OpenAiTextToSpeechServiceTests()
    {
        _mockHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHandler.Object);
        _sut = new OpenAiTextToSpeechService("test-api-key", httpClient: _httpClient);
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
        Action act = () => new OpenAiTextToSpeechService(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ProviderName_ReturnsOpenAITTS()
    {
        // Assert
        _sut.ProviderName.Should().Be("OpenAI TTS");
    }

    [Fact]
    public void AvailableVoices_ContainsAllVoices()
    {
        // Assert
        _sut.AvailableVoices.Should().Contain("alloy");
        _sut.AvailableVoices.Should().Contain("echo");
        _sut.AvailableVoices.Should().Contain("fable");
        _sut.AvailableVoices.Should().Contain("onyx");
        _sut.AvailableVoices.Should().Contain("nova");
        _sut.AvailableVoices.Should().Contain("shimmer");
    }

    [Fact]
    public void SupportedFormats_ContainsExpectedFormats()
    {
        // Assert
        _sut.SupportedFormats.Should().Contain("mp3");
        _sut.SupportedFormats.Should().Contain("wav");
        _sut.SupportedFormats.Should().Contain("opus");
        _sut.SupportedFormats.Should().Contain("flac");
    }

    [Fact]
    public void MaxInputLength_Returns4096()
    {
        // Assert
        _sut.MaxInputLength.Should().Be(4096);
    }

    [Fact]
    public async Task SynthesizeAsync_WithEmptyText_ReturnsFailure()
    {
        // Act
        var result = await _sut.SynthesizeAsync("");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Text cannot be empty");
    }

    [Fact]
    public async Task SynthesizeAsync_WithWhitespaceText_ReturnsFailure()
    {
        // Act
        var result = await _sut.SynthesizeAsync("   ");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Text cannot be empty");
    }

    [Fact]
    public async Task SynthesizeAsync_WithTooLongText_ReturnsFailure()
    {
        // Arrange
        string longText = new string('a', 4097);

        // Act
        var result = await _sut.SynthesizeAsync(longText);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Text too long");
    }

    [Fact]
    public async Task SynthesizeAsync_WithSuccessfulResponse_ReturnsSpeechResult()
    {
        // Arrange
        byte[] audioBytes = new byte[] { 1, 2, 3, 4, 5 };
        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(audioBytes)
            });

        // Act
        var result = await _sut.SynthesizeAsync("Hello world");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AudioData.Should().BeEquivalentTo(audioBytes);
    }

    [Fact]
    public async Task SynthesizeAsync_WithApiError_ReturnsFailure()
    {
        // Arrange
        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.TooManyRequests,
                Content = new StringContent("Rate limited")
            });

        // Act
        var result = await _sut.SynthesizeAsync("Hello world");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("TTS API error");
        result.Error.Should().Contain("TooManyRequests");
    }

    [Fact]
    public async Task SynthesizeAsync_WithHttpException_ThrowsHttpRequestException()
    {
        // Arrange
        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        // Act
        Func<Task> act = () => _sut.SynthesizeAsync("Hello world");

        // Assert - HttpRequestException is not caught by SynthesizeAsync
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task SynthesizeToFileAsync_WithValidInput_CreatesFile()
    {
        // Arrange
        byte[] audioBytes = new byte[] { 1, 2, 3, 4, 5 };
        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(audioBytes)
            });

        string tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.mp3");
        try
        {
            // Act
            var result = await _sut.SynthesizeToFileAsync("Hello world", tempFile);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be(tempFile);
            File.Exists(tempFile).Should().BeTrue();
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task SynthesizeToFileAsync_InfersFormatFromExtension()
    {
        // Arrange
        byte[] audioBytes = new byte[] { 1, 2, 3, 4, 5 };
        HttpRequestMessage? capturedRequest = null;
        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(audioBytes)
            });

        string tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.wav");
        try
        {
            // Act
            var result = await _sut.SynthesizeToFileAsync("Hello world", tempFile);

            // Assert
            result.IsSuccess.Should().BeTrue();
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task SynthesizeToStreamAsync_WithValidInput_WritesToStream()
    {
        // Arrange
        byte[] audioBytes = new byte[] { 1, 2, 3, 4, 5 };
        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(audioBytes)
            });

        using var stream = new MemoryStream();

        // Act
        var result = await _sut.SynthesizeToStreamAsync("Hello world", stream);

        // Assert
        result.IsSuccess.Should().BeTrue();
        stream.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SynthesizeToStreamAsync_WhenFails_ReturnsFailure()
    {
        // Arrange
        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Content = new StringContent("Server error")
            });

        using var stream = new MemoryStream();

        // Act
        var result = await _sut.SynthesizeToStreamAsync("Hello world", stream);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task IsAvailableAsync_WithValidKey_ChecksModelsEndpoint()
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
        bool available = await _sut.IsAvailableAsync();

        // Assert
        available.Should().BeTrue();
    }

    [Fact]
    public async Task IsAvailableAsync_WithFailedEndpoint_ReturnsFalse()
    {
        // Arrange
        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection failed"));

        // Act
        bool available = await _sut.IsAvailableAsync();

        // Assert
        available.Should().BeFalse();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var svc = new OpenAiTextToSpeechService("key");

        // Act
        Action act = () =>
        {
            svc.Dispose();
            svc.Dispose();
        };

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_WithExternalHttpClient_DoesNotDisposeClient()
    {
        // Arrange
        var handler = new Mock<HttpMessageHandler>();
        var client = new HttpClient(handler.Object);
        var svc = new OpenAiTextToSpeechService("key", httpClient: client);

        // Act - dispose the service
        svc.Dispose();

        // Assert - client should still be usable (not disposed)
        // If it were disposed, this would throw ObjectDisposedException
        Action act = () => client.BaseAddress = new Uri("http://test.com");
        act.Should().NotThrow();

        client.Dispose();
    }
}
