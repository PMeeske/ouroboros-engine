using System.Reflection;
using Ouroboros.Pipeline.Reasoning;

namespace Ouroboros.Tests.Pipeline.Reasoning;

[Trait("Category", "Unit")]
public sealed class NoOpEmbeddingModelTests
{
    [Fact]
    public async Task CreateEmbeddingsAsync_ReturnsEmptyArray()
    {
        // Arrange
        // NoOpEmbeddingModel is internal, so we create it via reflection or through a type that uses it.
        // Since it implements IEmbeddingModel, we can instantiate it via reflection.
        var type = typeof(PromptTemplate).Assembly.GetType("Ouroboros.Pipeline.Reasoning.NoOpEmbeddingModel");
        type.Should().NotBeNull("NoOpEmbeddingModel should exist in the assembly");
        var model = (IEmbeddingModel)Activator.CreateInstance(type!)!;

        // Act
        float[] result = await model.CreateEmbeddingsAsync("test input");

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_WithEmptyString_ReturnsEmptyArray()
    {
        // Arrange
        var type = typeof(PromptTemplate).Assembly.GetType("Ouroboros.Pipeline.Reasoning.NoOpEmbeddingModel");
        var model = (IEmbeddingModel)Activator.CreateInstance(type!)!;

        // Act
        float[] result = await model.CreateEmbeddingsAsync(string.Empty);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_WithCancellationToken_ReturnsEmptyArray()
    {
        // Arrange
        var type = typeof(PromptTemplate).Assembly.GetType("Ouroboros.Pipeline.Reasoning.NoOpEmbeddingModel");
        var model = (IEmbeddingModel)Activator.CreateInstance(type!)!;
        using var cts = new CancellationTokenSource();

        // Act
        float[] result = await model.CreateEmbeddingsAsync("test", cts.Token);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_MultipleCalls_AlwaysReturnsEmptyArray()
    {
        // Arrange
        var type = typeof(PromptTemplate).Assembly.GetType("Ouroboros.Pipeline.Reasoning.NoOpEmbeddingModel");
        var model = (IEmbeddingModel)Activator.CreateInstance(type!)!;

        // Act
        float[] result1 = await model.CreateEmbeddingsAsync("input 1");
        float[] result2 = await model.CreateEmbeddingsAsync("input 2");
        float[] result3 = await model.CreateEmbeddingsAsync("different input");

        // Assert
        result1.Should().BeEmpty();
        result2.Should().BeEmpty();
        result3.Should().BeEmpty();
    }
}
