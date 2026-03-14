#pragma warning disable CA2000 // Test file - ownership is managed by test lifecycle
using FluentAssertions;
using Microsoft.Extensions.AI;
using Moq;
using Ouroboros.Providers.Meai;
using IEmbeddingModel = Ouroboros.Domain.IEmbeddingModel;

namespace Ouroboros.Tests.Meai;

[Trait("Category", "Unit")]
public sealed class EmbeddingAdapterTests
{
    // -- EmbeddingGeneratorModelAdapter (MEAI -> Ouroboros) --

    [Fact]
    public void EmbeddingGeneratorModelAdapter_NullGenerator_Throws()
    {
        var act = () => new EmbeddingGeneratorModelAdapter(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task EmbeddingGeneratorModelAdapter_CreatesEmbeddings()
    {
        var genMock = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        genMock.Setup(g => g.GenerateAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<EmbeddingGenerationOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneratedEmbeddings<Embedding<float>>(
                new List<Embedding<float>> { new(new float[] { 0.1f, 0.2f, 0.3f }) }));

        var adapter = new EmbeddingGeneratorModelAdapter(genMock.Object);
        var result = await adapter.CreateEmbeddingsAsync("test text");

        result.Should().HaveCount(3);
        result[0].Should().BeApproximately(0.1f, 0.001f);
    }

    [Fact]
    public async Task EmbeddingGeneratorModelAdapter_EmptyResult_ReturnsEmptyArray()
    {
        var genMock = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        genMock.Setup(g => g.GenerateAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<EmbeddingGenerationOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneratedEmbeddings<Embedding<float>>(new List<Embedding<float>>()));

        var adapter = new EmbeddingGeneratorModelAdapter(genMock.Object);
        var result = await adapter.CreateEmbeddingsAsync("test");

        result.Should().BeEmpty();
    }

    // -- EmbeddingModelGeneratorAdapter (Ouroboros -> MEAI) --

    [Fact]
    public void EmbeddingModelGeneratorAdapter_NullModel_Throws()
    {
        var act = () => new EmbeddingModelGeneratorAdapter(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void EmbeddingModelGeneratorAdapter_Metadata_ReturnsNonNull()
    {
        var modelMock = new Mock<IEmbeddingModel>();
        var adapter = new EmbeddingModelGeneratorAdapter(modelMock.Object);

        adapter.Metadata.Should().NotBeNull();
    }

    [Fact]
    public async Task EmbeddingModelGeneratorAdapter_GeneratesEmbeddings()
    {
        var modelMock = new Mock<IEmbeddingModel>();
        modelMock.Setup(m => m.CreateEmbeddingsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.5f, 0.6f });

        var adapter = new EmbeddingModelGeneratorAdapter(modelMock.Object);
        var result = await adapter.GenerateAsync(new[] { "input1", "input2" });

        result.Should().HaveCount(2);
        result[0].Vector.ToArray().Should().BeEquivalentTo(new[] { 0.5f, 0.6f });
    }

    [Fact]
    public void EmbeddingModelGeneratorAdapter_GetService_ReturnsModel()
    {
        var modelMock = new Mock<IEmbeddingModel>();
        var adapter = new EmbeddingModelGeneratorAdapter(modelMock.Object);

        var service = adapter.GetService(typeof(IEmbeddingModel));
        service.Should().BeSameAs(modelMock.Object);
    }

    [Fact]
    public void EmbeddingModelGeneratorAdapter_GetService_Self()
    {
        var modelMock = new Mock<IEmbeddingModel>();
        var adapter = new EmbeddingModelGeneratorAdapter(modelMock.Object);

        var service = adapter.GetService(typeof(EmbeddingModelGeneratorAdapter));
        service.Should().BeSameAs(adapter);
    }

    [Fact]
    public void EmbeddingModelGeneratorAdapter_GetService_WithKey_ReturnsNull()
    {
        var modelMock = new Mock<IEmbeddingModel>();
        var adapter = new EmbeddingModelGeneratorAdapter(modelMock.Object);

        adapter.GetService(typeof(IEmbeddingModel), "key").Should().BeNull();
    }

    [Fact]
    public void EmbeddingModelGeneratorAdapter_GetService_Unknown_ReturnsNull()
    {
        var modelMock = new Mock<IEmbeddingModel>();
        var adapter = new EmbeddingModelGeneratorAdapter(modelMock.Object);

        adapter.GetService(typeof(string)).Should().BeNull();
    }

    [Fact]
    public void EmbeddingModelGeneratorAdapter_Dispose_DoesNotThrow()
    {
        var modelMock = new Mock<IEmbeddingModel>();
        var adapter = new EmbeddingModelGeneratorAdapter(modelMock.Object);

        var act = () => adapter.Dispose();
        act.Should().NotThrow();
    }
}
