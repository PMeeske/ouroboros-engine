using FluentAssertions;
using Microsoft.Extensions.AI;
using Moq;
using Ouroboros.Providers.Meai;
using IEmbeddingModel = Ouroboros.Domain.IEmbeddingModel;

namespace Ouroboros.Tests.Meai;

[Trait("Category", "Unit")]
public sealed class EmbeddingModelGeneratorAdapterTests
{
    [Fact]
    public void Constructor_NullModel_ThrowsArgumentNullException()
    {
        // Arrange
        IEmbeddingModel model = null!;

        // Act
        var act = () => new EmbeddingModelGeneratorAdapter(model);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Metadata_HasCorrectProviderName()
    {
        // Arrange
        var mockModel = new Mock<IEmbeddingModel>();
        var sut = new EmbeddingModelGeneratorAdapter(mockModel.Object);

        // Act
        var metadata = sut.Metadata;

        // Assert
        metadata.Should().NotBeNull();
        metadata.ProviderName.Should().Be(nameof(EmbeddingModelGeneratorAdapter));
    }

    [Fact]
    public async Task GenerateAsync_ReturnsEmbeddingsFromModel()
    {
        // Arrange
        var mockModel = new Mock<IEmbeddingModel>();
        mockModel.Setup(m => m.CreateEmbeddingsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 1.0f, 2.0f });

        var sut = new EmbeddingModelGeneratorAdapter(mockModel.Object);

        // Act
        var result = await sut.GenerateAsync(new[] { "input1", "input2" });

        // Assert
        result.Should().HaveCount(2);
        result[0].Vector.ToArray().Should().BeEquivalentTo(new[] { 1.0f, 2.0f });
        result[1].Vector.ToArray().Should().BeEquivalentTo(new[] { 1.0f, 2.0f });
    }

    [Fact]
    public void GetService_IEmbeddingModelType_ReturnsModel()
    {
        // Arrange
        var mockModel = new Mock<IEmbeddingModel>();
        var sut = new EmbeddingModelGeneratorAdapter(mockModel.Object);

        // Act
        var service = sut.GetService(typeof(IEmbeddingModel));

        // Assert
        service.Should().BeSameAs(mockModel.Object);
    }

    [Fact]
    public void GetService_AdapterType_ReturnsSelf()
    {
        // Arrange
        var mockModel = new Mock<IEmbeddingModel>();
        var sut = new EmbeddingModelGeneratorAdapter(mockModel.Object);

        // Act
        var service = sut.GetService(typeof(EmbeddingModelGeneratorAdapter));

        // Assert
        service.Should().BeSameAs(sut);
    }

    [Fact]
    public void GetService_UnknownType_ReturnsNull()
    {
        // Arrange
        var mockModel = new Mock<IEmbeddingModel>();
        var sut = new EmbeddingModelGeneratorAdapter(mockModel.Object);

        // Act
        var service = sut.GetService(typeof(string));

        // Assert
        service.Should().BeNull();
    }

    [Fact]
    public void GetService_WithKey_ReturnsNull()
    {
        // Arrange
        var mockModel = new Mock<IEmbeddingModel>();
        var sut = new EmbeddingModelGeneratorAdapter(mockModel.Object);

        // Act
        var service = sut.GetService(typeof(IEmbeddingModel), "some-key");

        // Assert
        service.Should().BeNull();
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        // Arrange
        var mockModel = new Mock<IEmbeddingModel>();
        var sut = new EmbeddingModelGeneratorAdapter(mockModel.Object);

        // Act
        var act = () => sut.Dispose();

        // Assert
        act.Should().NotThrow();
    }
}
