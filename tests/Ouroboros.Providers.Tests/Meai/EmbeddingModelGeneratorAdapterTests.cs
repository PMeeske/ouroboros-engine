using FluentAssertions;
using Microsoft.Extensions.AI;
using Moq;
using Ouroboros.Domain;
using Ouroboros.Providers.Meai;
using Xunit;

namespace Ouroboros.Tests.Meai;

[Trait("Category", "Unit")]
public sealed class EmbeddingModelGeneratorAdapterTests : IDisposable
{
    private readonly Mock<IEmbeddingModel> _mockModel;
    private readonly EmbeddingModelGeneratorAdapter _sut;

    public EmbeddingModelGeneratorAdapterTests()
    {
        _mockModel = new Mock<IEmbeddingModel>();
        _sut = new EmbeddingModelGeneratorAdapter(_mockModel.Object);
    }

    [Fact]
    public void Ctor_WithNullModel_ThrowsArgumentNullException()
    {
        FluentActions.Invoking(() => new EmbeddingModelGeneratorAdapter(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Metadata_ReturnsValidMetadata()
    {
        _sut.Metadata.Should().NotBeNull();
        _sut.Metadata.ProviderName.Should().Be(nameof(EmbeddingModelGeneratorAdapter));
    }

    [Fact]
    public async Task GenerateAsync_WithSingleInput_ReturnsEmbedding()
    {
        // Arrange
        var expectedVector = new float[] { 0.1f, 0.2f, 0.3f };
        _mockModel
            .Setup(m => m.CreateEmbeddingsAsync("hello", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedVector);

        // Act
        var result = await _sut.GenerateAsync(new[] { "hello" });

        // Assert
        result.Should().HaveCount(1);
        result[0].Vector.ToArray().Should().BeEquivalentTo(expectedVector);
    }

    [Fact]
    public async Task GenerateAsync_WithMultipleInputs_ReturnsMultipleEmbeddings()
    {
        // Arrange
        var vector1 = new float[] { 0.1f, 0.2f };
        var vector2 = new float[] { 0.3f, 0.4f };

        _mockModel
            .Setup(m => m.CreateEmbeddingsAsync("a", It.IsAny<CancellationToken>()))
            .ReturnsAsync(vector1);
        _mockModel
            .Setup(m => m.CreateEmbeddingsAsync("b", It.IsAny<CancellationToken>()))
            .ReturnsAsync(vector2);

        // Act
        var result = await _sut.GenerateAsync(new[] { "a", "b" });

        // Assert
        result.Should().HaveCount(2);
        result[0].Vector.ToArray().Should().BeEquivalentTo(vector1);
        result[1].Vector.ToArray().Should().BeEquivalentTo(vector2);
    }

    [Fact]
    public async Task GenerateAsync_WithEmptyInputs_ReturnsEmptyResult()
    {
        // Act
        var result = await _sut.GenerateAsync(Array.Empty<string>());

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetService_WithIEmbeddingModelType_ReturnsInnerModel()
    {
        // Act
        var result = _sut.GetService(typeof(IEmbeddingModel));

        // Assert
        result.Should().BeSameAs(_mockModel.Object);
    }

    [Fact]
    public void GetService_WithOwnType_ReturnsSelf()
    {
        // Act
        var result = _sut.GetService(typeof(EmbeddingModelGeneratorAdapter));

        // Assert
        result.Should().BeSameAs(_sut);
    }

    [Fact]
    public void GetService_WithKey_ReturnsNull()
    {
        // Act
        var result = _sut.GetService(typeof(IEmbeddingModel), "some-key");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetService_WithUnknownType_ReturnsNull()
    {
        // Act
        var result = _sut.GetService(typeof(string));

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        FluentActions.Invoking(() => _sut.Dispose()).Should().NotThrow();
    }

    public void Dispose()
    {
        _sut.Dispose();
    }
}
