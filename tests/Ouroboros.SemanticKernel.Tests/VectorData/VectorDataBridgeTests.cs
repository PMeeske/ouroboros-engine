// <copyright file="VectorDataBridgeTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Domain.Vectors;
using Ouroboros.SemanticKernel.VectorData;
using SkVectorStore = Microsoft.Extensions.VectorData.VectorStore;

namespace Ouroboros.SemanticKernel.Tests.VectorData;

public sealed class VectorDataBridgeTests
{
    // ── ToOuroboros ───────────────────────────────────────────────────────

    [Fact]
    public void ToOuroboros_NullSkStore_ThrowsArgumentNullException()
    {
        var act = () => VectorDataBridge.ToOuroboros(null!, "collection");
        act.Should().Throw<ArgumentNullException>().WithParameterName("skStore");
    }

    [Fact]
    public void ToOuroboros_NullCollectionName_ThrowsArgumentException()
    {
        var mockStore = new Mock<SkVectorStore>();
        var act = () => VectorDataBridge.ToOuroboros(mockStore.Object, null!);
        act.Should().Throw<ArgumentException>().WithParameterName("collectionName");
    }

    [Fact]
    public void ToOuroboros_WhitespaceCollectionName_ThrowsArgumentException()
    {
        var mockStore = new Mock<SkVectorStore>();
        var act = () => VectorDataBridge.ToOuroboros(mockStore.Object, "   ");
        act.Should().Throw<ArgumentException>().WithParameterName("collectionName");
    }

    [Fact]
    public void ToOuroboros_ValidArgs_ReturnsIAdvancedVectorStore()
    {
        var mockStore = new Mock<SkVectorStore>();
        var result = VectorDataBridge.ToOuroboros(mockStore.Object, "my_collection", 768);
        result.Should().NotBeNull();
        result.Should().BeAssignableTo<IAdvancedVectorStore>();
    }

    // ── ToSk ─────────────────────────────────────────────────────────────

    [Fact]
    public void ToSk_NullOuroStore_ThrowsArgumentNullException()
    {
        var act = () => VectorDataBridge.ToSk(null!, "collection");
        act.Should().Throw<ArgumentNullException>().WithParameterName("ouroStore");
    }

    [Fact]
    public void ToSk_NullCollectionName_ThrowsArgumentException()
    {
        var mockStore = new Mock<IAdvancedVectorStore>();
        var act = () => VectorDataBridge.ToSk(mockStore.Object, null!);
        act.Should().Throw<ArgumentException>().WithParameterName("collectionName");
    }

    [Fact]
    public void ToSk_WhitespaceCollectionName_ThrowsArgumentException()
    {
        var mockStore = new Mock<IAdvancedVectorStore>();
        var act = () => VectorDataBridge.ToSk(mockStore.Object, "  ");
        act.Should().Throw<ArgumentException>().WithParameterName("collectionName");
    }

    [Fact]
    public void ToSk_ValidArgs_ReturnsSkVectorStore()
    {
        var mockStore = new Mock<IAdvancedVectorStore>();
        var result = VectorDataBridge.ToSk(mockStore.Object, "my_collection");
        result.Should().NotBeNull();
        result.Should().BeAssignableTo<SkVectorStore>();
    }
}
