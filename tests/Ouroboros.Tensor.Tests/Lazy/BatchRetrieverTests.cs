// <copyright file="BatchRetrieverTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Lazy;

[Trait("Category", "Unit")]
public sealed class BatchRetrieverTests
{
    private static readonly float[] SampleVector = { 1f, 2f, 3f };

    [Fact]
    public async Task FetchAsync_EmptyHandles_ReturnsEmptyList()
    {
        // Arrange
        var store = Substitute.For<IHandleAwareVectorStore>();
        var retriever = new BatchRetriever(store);

        // Act
        var result = await retriever.FetchAsync(Array.Empty<VectorHandle>());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchAsync_SingleHandle_FetchesSuccessfully()
    {
        // Arrange
        var handle = new VectorHandle("qdrant", "docs", "vec-1", 3);
        var store = Substitute.For<IHandleAwareVectorStore>();

        store.FetchBatchAsync(Arg.Any<IEnumerable<VectorHandle>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<(VectorHandle, float[])>, string>.Success(
                new[] { (handle, SampleVector) }));

        var retriever = new BatchRetriever(store);

        // Act
        var result = await retriever.FetchAsync(new[] { handle });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Handle.Should().Be(handle);
        result.Value[0].Vector.Should().Equal(SampleVector);
    }

    [Fact]
    public async Task FetchAsync_MultipleHandles_ReturnsAllResults()
    {
        // Arrange
        var h1 = new VectorHandle("qdrant", "docs", "v1", 3);
        var h2 = new VectorHandle("qdrant", "docs", "v2", 3);
        var store = Substitute.For<IHandleAwareVectorStore>();

        store.FetchBatchAsync(Arg.Any<IEnumerable<VectorHandle>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<(VectorHandle, float[])>, string>.Success(
                new[] { (h1, SampleVector), (h2, SampleVector) }));

        var retriever = new BatchRetriever(store);

        // Act
        var result = await retriever.FetchAsync(new[] { h1, h2 });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task FetchAsync_WhenStoreFails_ReturnsFailure()
    {
        // Arrange
        var handle = new VectorHandle("qdrant", "docs", "vec-1", 3);
        var store = Substitute.For<IHandleAwareVectorStore>();

        store.FetchBatchAsync(Arg.Any<IEnumerable<VectorHandle>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<(VectorHandle, float[])>, string>.Failure("timeout"));

        var retriever = new BatchRetriever(store);

        // Act
        var result = await retriever.FetchAsync(new[] { handle });

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("timeout");
    }

    [Fact]
    public void Constructor_WithZeroBatchSize_ThrowsArgumentOutOfRangeException()
    {
        var store = Substitute.For<IHandleAwareVectorStore>();
        var act = () => new BatchRetriever(store, batchSize: 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task FetchAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var handle = new VectorHandle("qdrant", "docs", "vec-1", 3);
        var store = Substitute.For<IHandleAwareVectorStore>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var retriever = new BatchRetriever(store);

        // Act & Assert
        await retriever.Invoking(r => r.FetchAsync(new[] { handle }, cts.Token))
            .Should().ThrowAsync<OperationCanceledException>();
    }
}
