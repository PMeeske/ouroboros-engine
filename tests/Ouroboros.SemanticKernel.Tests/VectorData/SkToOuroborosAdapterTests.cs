// <copyright file="SkToOuroborosAdapterTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Domain.Vectors;
using Ouroboros.SemanticKernel.VectorData;
using SkVectorStore = Microsoft.Extensions.VectorData.VectorStore;

namespace Ouroboros.SemanticKernel.Tests.VectorData;

public sealed class SkToOuroborosAdapterTests
{
    private readonly Mock<SkVectorStore> _mockSkStore = new();
    private const string CollectionName = "test_vectors";
    private const int VectorDimension = 1536;

    private SkToOuroborosAdapter CreateAdapter() =>
        new(_mockSkStore.Object, CollectionName, VectorDimension);

    // ── Constructor ──────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullSkStore_ThrowsArgumentNullException()
    {
        var act = () => new SkToOuroborosAdapter(null!, CollectionName, VectorDimension);
        act.Should().Throw<ArgumentNullException>().WithParameterName("skStore");
    }

    [Fact]
    public void Constructor_NullCollectionName_ThrowsArgumentException()
    {
        var mockStore = new Mock<SkVectorStore>();
        var act = () => new SkToOuroborosAdapter(mockStore.Object, null!, VectorDimension);
        act.Should().Throw<ArgumentException>().WithParameterName("collectionName");
    }

    [Fact]
    public void Constructor_WhitespaceCollectionName_ThrowsArgumentException()
    {
        var mockStore = new Mock<SkVectorStore>();
        var act = () => new SkToOuroborosAdapter(mockStore.Object, "  ", VectorDimension);
        act.Should().Throw<ArgumentException>().WithParameterName("collectionName");
    }

    [Fact]
    public void Constructor_ValidArgs_DoesNotThrow()
    {
        var act = () => CreateAdapter();
        act.Should().NotThrow();
    }

    // ── AddAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_NullVectors_ThrowsArgumentNullException()
    {
        var adapter = CreateAdapter();
        var act = () => adapter.AddAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("vectors");
    }

    // ── GetSimilarDocumentsAsync ─────────────────────────────────────────

    [Fact]
    public async Task GetSimilarDocumentsAsync_NullEmbedding_ThrowsArgumentNullException()
    {
        var adapter = CreateAdapter();
        var act = () => adapter.GetSimilarDocumentsAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("embedding");
    }

    [Fact]
    public async Task GetSimilarDocumentsAsync_ZeroAmount_ThrowsArgumentOutOfRangeException()
    {
        var adapter = CreateAdapter();
        var act = () => adapter.GetSimilarDocumentsAsync(new float[] { 1.0f }, amount: 0);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>().WithParameterName("amount");
    }

    [Fact]
    public async Task GetSimilarDocumentsAsync_NegativeAmount_ThrowsArgumentOutOfRangeException()
    {
        var adapter = CreateAdapter();
        var act = () => adapter.GetSimilarDocumentsAsync(new float[] { 1.0f }, amount: -1);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>().WithParameterName("amount");
    }

    // ── GetAll ────────────────────────────────────────────────────────────

    [Fact]
    public void GetAll_ThrowsNotSupportedException()
    {
        var adapter = CreateAdapter();
        var act = () => adapter.GetAll();
        act.Should().Throw<NotSupportedException>()
            .WithMessage("*does not support synchronous GetAll*");
    }

    // ── CountAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task CountAsync_ThrowsNotSupportedException()
    {
        var adapter = CreateAdapter();
        var act = () => adapter.CountAsync();
        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*Count is not supported*");
    }

    // ── ScrollAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task ScrollAsync_ThrowsNotSupportedException()
    {
        var adapter = CreateAdapter();
        var act = () => adapter.ScrollAsync();
        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*Scroll is not supported*");
    }

    // ── RecommendAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task RecommendAsync_ThrowsNotSupportedException()
    {
        var adapter = CreateAdapter();
        var act = () => adapter.RecommendAsync(new List<string> { "id1" });
        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*Recommend is not supported*");
    }

    // ── DeleteByIdAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task DeleteByIdAsync_NullIds_ThrowsArgumentNullException()
    {
        var adapter = CreateAdapter();
        var act = () => adapter.DeleteByIdAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("ids");
    }

    // ── DeleteByFilterAsync ──────────────────────────────────────────────

    [Fact]
    public async Task DeleteByFilterAsync_ThrowsNotSupportedException()
    {
        var adapter = CreateAdapter();
        var filter = new Dictionary<string, object> { ["key"] = "value" };
        var act = () => adapter.DeleteByFilterAsync(filter);
        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*Filter-based deletion is not supported*");
    }

    // ── GetInfoAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetInfoAsync_CollectionExists_ReturnsReadyStatus()
    {
        _mockSkStore
            .Setup(s => s.CollectionExistsAsync(CollectionName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var adapter = CreateAdapter();
        var info = await adapter.GetInfoAsync();

        info.Name.Should().Be(CollectionName);
        info.Status.Should().Be("ready");
        info.VectorDimension.Should().Be(VectorDimension);
        info.VectorCount.Should().Be(0UL);
    }

    [Fact]
    public async Task GetInfoAsync_CollectionNotExists_ReturnsNotFoundStatus()
    {
        _mockSkStore
            .Setup(s => s.CollectionExistsAsync(CollectionName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var adapter = CreateAdapter();
        var info = await adapter.GetInfoAsync();

        info.Name.Should().Be(CollectionName);
        info.Status.Should().Be("not_found");
    }

    // ── SearchWithFilterAsync ────────────────────────────────────────────

    [Fact]
    public async Task SearchWithFilterAsync_NullEmbedding_ThrowsArgumentNullException()
    {
        var adapter = CreateAdapter();
        var act = () => adapter.SearchWithFilterAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("embedding");
    }

    [Fact]
    public async Task SearchWithFilterAsync_ZeroAmount_ThrowsArgumentOutOfRangeException()
    {
        var adapter = CreateAdapter();
        var act = () => adapter.SearchWithFilterAsync(new float[] { 1.0f }, amount: 0);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>().WithParameterName("amount");
    }

    // ── BatchSearchAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task BatchSearchAsync_NullEmbeddings_ThrowsArgumentNullException()
    {
        var adapter = CreateAdapter();
        var act = () => adapter.BatchSearchAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("embeddings");
    }

    [Fact]
    public async Task BatchSearchAsync_ZeroAmount_ThrowsArgumentOutOfRangeException()
    {
        var adapter = CreateAdapter();
        var embeddings = new List<float[]> { new float[] { 1.0f } };
        var act = () => adapter.BatchSearchAsync(embeddings, amount: 0);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>().WithParameterName("amount");
    }
}
