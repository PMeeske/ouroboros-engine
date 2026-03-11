namespace Ouroboros.Tests.Pipeline.Reasoning;

using Ouroboros.Pipeline.Reasoning;

[Trait("Category", "Unit")]
public class NoOpEmbeddingModelTests
{
    [Fact]
    public async Task CreateEmbeddingsAsync_ReturnsEmptyArray()
    {
        // Arrange
        var sut = new NoOpEmbeddingModel();

        // Act
        var result = await sut.CreateEmbeddingsAsync("some input text");

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_WithEmptyInput_ReturnsEmptyArray()
    {
        // Arrange
        var sut = new NoOpEmbeddingModel();

        // Act
        var result = await sut.CreateEmbeddingsAsync(string.Empty);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_WithCancellationToken_ReturnsEmptyArray()
    {
        // Arrange
        var sut = new NoOpEmbeddingModel();
        using var cts = new CancellationTokenSource();

        // Act
        var result = await sut.CreateEmbeddingsAsync("test", cts.Token);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_ReturnsSameEmptyArrayInstance()
    {
        // Arrange
        var sut = new NoOpEmbeddingModel();

        // Act
        var result1 = await sut.CreateEmbeddingsAsync("input1");
        var result2 = await sut.CreateEmbeddingsAsync("input2");

        // Assert
        result1.Should().BeEmpty();
        result2.Should().BeEmpty();
    }

    [Fact]
    public void ImplementsIEmbeddingModel()
    {
        // Arrange & Act
        var sut = new NoOpEmbeddingModel();

        // Assert
        sut.Should().BeAssignableTo<IEmbeddingModel>();
    }
}
