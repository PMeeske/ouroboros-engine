// <copyright file="OuroborosToSkAdapterTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Domain.Vectors;
using Ouroboros.SemanticKernel.VectorData;
using SkVectorStore = Microsoft.Extensions.VectorData.VectorStore;

namespace Ouroboros.SemanticKernel.Tests.VectorData;

public sealed class OuroborosToSkAdapterTests
{
    private readonly Mock<IAdvancedVectorStore> _mockOuroStore = new();
    private const string CollectionName = "test_collection";

    private OuroborosToSkAdapter CreateAdapter() =>
        new(_mockOuroStore.Object, CollectionName);

    // ── Constructor ──────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullStore_ThrowsArgumentNullException()
    {
        var act = () => new OuroborosToSkAdapter(null!, CollectionName);
        act.Should().Throw<ArgumentNullException>().WithParameterName("ouroStore");
    }

    [Fact]
    public void Constructor_ValidArgs_DoesNotThrow()
    {
        var act = () => CreateAdapter();
        act.Should().NotThrow();
    }

    // ── GetCollection ────────────────────────────────────────────────────

    [Fact]
    public void GetCollection_ThrowsNotSupportedException()
    {
        using var adapter = CreateAdapter();
        var act = () => adapter.GetCollection<string, Dictionary<string, object?>>("name");
        act.Should().Throw<NotSupportedException>()
            .WithMessage("*Ouroboros-to-SK bridge does not support typed collection access*");
    }

    // ── GetDynamicCollection ─────────────────────────────────────────────

    [Fact]
    public void GetDynamicCollection_ThrowsNotSupportedException()
    {
        using var adapter = CreateAdapter();
        var definition = new Microsoft.Extensions.VectorData.VectorStoreCollectionDefinition
        {
            Properties = new List<Microsoft.Extensions.VectorData.VectorStoreProperty>
            {
                new Microsoft.Extensions.VectorData.VectorStoreKeyProperty("Id", typeof(object)),
            },
        };

        var act = () => adapter.GetDynamicCollection("name", definition);
        act.Should().Throw<NotSupportedException>()
            .WithMessage("*Ouroboros-to-SK bridge does not support dynamic collection access*");
    }

    // ── ListCollectionNamesAsync ─────────────────────────────────────────

    [Fact]
    public async Task ListCollectionNamesAsync_StoreReturnsInfo_YieldsInfoName()
    {
        _mockOuroStore
            .Setup(s => s.GetInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VectorStoreInfo("my_store", 100, 1536, "ready"));

        using var adapter = CreateAdapter();
        var names = new List<string>();

        await foreach (var name in adapter.ListCollectionNamesAsync())
        {
            names.Add(name);
        }

        names.Should().ContainSingle().Which.Should().Be("my_store");
    }

    [Fact]
    public async Task ListCollectionNamesAsync_StoreReturnsEmptyName_YieldsCollectionName()
    {
        _mockOuroStore
            .Setup(s => s.GetInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VectorStoreInfo("", 0, 1536, "ready"));

        using var adapter = CreateAdapter();
        var names = new List<string>();

        await foreach (var name in adapter.ListCollectionNamesAsync())
        {
            names.Add(name);
        }

        names.Should().ContainSingle().Which.Should().Be(CollectionName);
    }

    [Fact]
    public async Task ListCollectionNamesAsync_StoreThrowsException_YieldsNoNames()
    {
        _mockOuroStore
            .Setup(s => s.GetInfoAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Store unavailable"));

        using var adapter = CreateAdapter();
        var names = new List<string>();

        await foreach (var name in adapter.ListCollectionNamesAsync())
        {
            names.Add(name);
        }

        names.Should().BeEmpty();
    }

    [Fact]
    public async Task ListCollectionNamesAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockOuroStore
            .Setup(s => s.GetInfoAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        using var adapter = CreateAdapter();

        var act = async () =>
        {
            await foreach (var _ in adapter.ListCollectionNamesAsync(cts.Token))
            {
            }
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ── CollectionExistsAsync ────────────────────────────────────────────

    [Theory]
    [InlineData("ready", true)]
    [InlineData("Ready", true)]
    [InlineData("READY", true)]
    [InlineData("green", true)]
    [InlineData("Green", true)]
    [InlineData("not_found", false)]
    [InlineData("initializing", false)]
    [InlineData("", false)]
    public async Task CollectionExistsAsync_ReturnsCorrectly_BasedOnStatus(string status, bool expected)
    {
        _mockOuroStore
            .Setup(s => s.GetInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VectorStoreInfo("store", 10, 1536, status));

        using var adapter = CreateAdapter();

        var result = await adapter.CollectionExistsAsync("any");

        result.Should().Be(expected);
    }

    [Fact]
    public async Task CollectionExistsAsync_StoreThrowsException_ReturnsFalse()
    {
        _mockOuroStore
            .Setup(s => s.GetInfoAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Store error"));

        using var adapter = CreateAdapter();

        var result = await adapter.CollectionExistsAsync("any");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CollectionExistsAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        _mockOuroStore
            .Setup(s => s.GetInfoAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        using var adapter = CreateAdapter();

        var act = () => adapter.CollectionExistsAsync("any");

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ── EnsureCollectionDeletedAsync ─────────────────────────────────────

    [Fact]
    public async Task EnsureCollectionDeletedAsync_CallsClearOnStore()
    {
        using var adapter = CreateAdapter();

        await adapter.EnsureCollectionDeletedAsync("any");

        _mockOuroStore.Verify(s => s.ClearAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── GetService ───────────────────────────────────────────────────────

    [Fact]
    public void GetService_NullServiceType_ThrowsArgumentNullException()
    {
        using var adapter = CreateAdapter();
        var act = () => adapter.GetService(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("serviceType");
    }

    [Fact]
    public void GetService_IAdvancedVectorStore_ReturnsUnderlyingStore()
    {
        using var adapter = CreateAdapter();
        var result = adapter.GetService(typeof(IAdvancedVectorStore));
        result.Should().BeSameAs(_mockOuroStore.Object);
    }

    [Fact]
    public void GetService_SkVectorStoreType_ReturnsSelf()
    {
        using var adapter = CreateAdapter();
        var result = adapter.GetService(typeof(SkVectorStore));
        result.Should().BeSameAs(adapter);
    }

    [Fact]
    public void GetService_OuroborosToSkAdapterType_ReturnsSelf()
    {
        using var adapter = CreateAdapter();
        var result = adapter.GetService(typeof(OuroborosToSkAdapter));
        result.Should().BeSameAs(adapter);
    }

    [Fact]
    public void GetService_UnknownType_ReturnsNull()
    {
        using var adapter = CreateAdapter();
        var result = adapter.GetService(typeof(string));
        result.Should().BeNull();
    }
}
