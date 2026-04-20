using System.Net;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Ouroboros.Providers.HuggingFace;
using Xunit;

namespace Ouroboros.Tests.HuggingFace;

[Trait("Category", "Unit")]
public sealed class HuggingFaceInferenceProviderTests : IDisposable
{
    private readonly Mock<HttpMessageHandler> _mockHandler = new();
    private readonly HttpClient _httpClient;
    private readonly HuggingFaceInferenceProvider _sut;

    public HuggingFaceInferenceProviderTests()
    {
        _httpClient = new HttpClient(_mockHandler.Object);
        _sut = new HuggingFaceInferenceProvider("test-token", _httpClient);
    }

    [Fact]
    public void Ctor_WithNullApiToken_ThrowsArgumentNullException()
    {
        FluentActions.Invoking(() => new HuggingFaceInferenceProvider(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Ctor_WithValidToken_SetsAuthHeader()
    {
        // Assert - the httpClient should have auth header set
        _httpClient.DefaultRequestHeaders.Authorization.Should().NotBeNull();
        _httpClient.DefaultRequestHeaders.Authorization!.Scheme.Should().Be("Bearer");
        _httpClient.DefaultRequestHeaders.Authorization!.Parameter.Should().Be("test-token");
    }

    [Fact]
    public async Task ClassifyAsync_WithSuccessResponse_ReturnsSuccess()
    {
        // Arrange
        var json = """[[{"label":"positive","score":0.95},{"label":"negative","score":0.05}]]""";
        SetupMockResponse(HttpStatusCode.OK, json);

        // Act
        var result = await _sut.ClassifyAsync("model-id", "I love this!");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Match(
            success =>
            {
                success.Should().HaveCount(2);
                success[0].Label.Should().Be("positive");
                success[0].Score.Should().BeApproximately(0.95, 0.001);
                return true;
            },
            _ => false).Should().BeTrue();
    }

    [Fact]
    public async Task ClassifyAsync_WithApiError_ReturnsFailure()
    {
        // Arrange
        SetupMockResponse(HttpStatusCode.InternalServerError, "Server Error");

        // Act
        var result = await _sut.ClassifyAsync("model-id", "test");

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ClassifyAsync_WithNullModelId_ThrowsArgumentNullException()
    {
        await FluentActions.Invoking(() => _sut.ClassifyAsync(null!, "text"))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ClassifyAsync_WithNullText_ThrowsArgumentNullException()
    {
        await FluentActions.Invoking(() => _sut.ClassifyAsync("model", null!))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ZeroShotClassifyAsync_WithSuccessResponse_ReturnsSuccess()
    {
        // Arrange
        var json = """{"labels":["positive","negative","neutral"],"scores":[0.8,0.15,0.05]}""";
        SetupMockResponse(HttpStatusCode.OK, json);

        // Act
        var result = await _sut.ZeroShotClassifyAsync(
            "facebook/bart-large-mnli",
            "I love this product",
            new List<string> { "positive", "negative", "neutral" });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Match(
            success =>
            {
                success.TopLabel.Should().Be("positive");
                success.TopScore.Should().BeApproximately(0.8, 0.001);
                success.LabelScores.Should().HaveCount(3);
                return true;
            },
            _ => false).Should().BeTrue();
    }

    [Fact]
    public async Task ZeroShotClassifyAsync_WithApiError_ReturnsFailure()
    {
        // Arrange
        SetupMockResponse(HttpStatusCode.TooManyRequests, "Rate limited");

        // Act
        var result = await _sut.ZeroShotClassifyAsync(
            "model", "text", new List<string> { "a" });

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ZeroShotClassifyAsync_WithNullArgs_Throws()
    {
        await FluentActions.Invoking(() => _sut.ZeroShotClassifyAsync(null!, "text", new List<string>()))
            .Should().ThrowAsync<ArgumentNullException>();

        await FluentActions.Invoking(() => _sut.ZeroShotClassifyAsync("model", null!, new List<string>()))
            .Should().ThrowAsync<ArgumentNullException>();

        await FluentActions.Invoking(() => _sut.ZeroShotClassifyAsync("model", "text", null!))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task EmbedAsync_WithSuccessResponse_ReturnsEmbedding()
    {
        // Arrange
        var json = "[0.1, 0.2, 0.3, 0.4]";
        SetupMockResponse(HttpStatusCode.OK, json);

        // Act
        var result = await _sut.EmbedAsync("sentence-transformers/all-MiniLM-L6-v2", "test text");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Match(
            success =>
            {
                success.Should().HaveCount(4);
                success[0].Should().BeApproximately(0.1f, 0.001f);
                return true;
            },
            _ => false).Should().BeTrue();
    }

    [Fact]
    public async Task EmbedAsync_WithApiError_ReturnsFailure()
    {
        // Arrange
        SetupMockResponse(HttpStatusCode.Unauthorized, "Unauthorized");

        // Act
        var result = await _sut.EmbedAsync("model", "text");

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task EmbedAsync_WithNullArgs_Throws()
    {
        await FluentActions.Invoking(() => _sut.EmbedAsync(null!, "text"))
            .Should().ThrowAsync<ArgumentNullException>();

        await FluentActions.Invoking(() => _sut.EmbedAsync("model", null!))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GenerateAsync_WithSuccessResponse_ReturnsText()
    {
        // Arrange
        var json = """[{"generated_text":"Hello world, this is AI!"}]""";
        SetupMockResponse(HttpStatusCode.OK, json);

        // Act
        var result = await _sut.GenerateAsync("model", "Hello");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Match(
            success =>
            {
                success.Should().Be("Hello world, this is AI!");
                return true;
            },
            _ => false).Should().BeTrue();
    }

    [Fact]
    public async Task GenerateAsync_WithObjectResponse_ReturnsText()
    {
        // Arrange
        var json = """{"generated_text":"Direct response"}""";
        SetupMockResponse(HttpStatusCode.OK, json);

        // Act
        var result = await _sut.GenerateAsync("model", "Hello");

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateAsync_WithApiError_ReturnsFailure()
    {
        // Arrange
        SetupMockResponse(HttpStatusCode.ServiceUnavailable, "Service down");

        // Act
        var result = await _sut.GenerateAsync("model", "test");

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateAsync_WithNullArgs_Throws()
    {
        await FluentActions.Invoking(() => _sut.GenerateAsync(null!, "prompt"))
            .Should().ThrowAsync<ArgumentNullException>();

        await FluentActions.Invoking(() => _sut.GenerateAsync("model", null!))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void Dispose_WithOwnedHttpClient_DisposesClient()
    {
        // Arrange - create provider without external HttpClient
        var provider = new HuggingFaceInferenceProvider("token");

        // Act & Assert
        FluentActions.Invoking(() => provider.Dispose()).Should().NotThrow();
    }

    [Fact]
    public void Dispose_WithExternalHttpClient_DoesNotDisposeClient()
    {
        // Act
        _sut.Dispose();

        // Assert - httpClient should still be usable (not disposed)
        FluentActions.Invoking(() => _httpClient.BaseAddress = new Uri("http://example.com"))
            .Should().NotThrow();
    }

    [Fact]
    public void ClassificationResult_RecordEquality()
    {
        // Arrange
        var a = new HuggingFaceInferenceProvider.ClassificationResult("positive", 0.95);
        var b = new HuggingFaceInferenceProvider.ClassificationResult("positive", 0.95);

        // Assert
        a.Should().Be(b);
        a.Label.Should().Be("positive");
        a.Score.Should().Be(0.95);
    }

    [Fact]
    public void ZeroShotResult_RecordProperties()
    {
        // Arrange
        var scores = new Dictionary<string, double> { ["happy"] = 0.9 };
        var result = new HuggingFaceInferenceProvider.ZeroShotResult("happy", 0.9, scores);

        // Assert
        result.TopLabel.Should().Be("happy");
        result.TopScore.Should().Be(0.9);
        result.LabelScores.Should().ContainKey("happy");
    }

    private void SetupMockResponse(HttpStatusCode statusCode, string content)
    {
        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
            });
    }

    public void Dispose()
    {
        _sut.Dispose();
        _httpClient.Dispose();
    }
}
