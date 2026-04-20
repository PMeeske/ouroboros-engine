// <copyright file="QdrantDagStoreTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Reflection;
using FluentAssertions;
using Moq;
using Ouroboros.Core.Configuration;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using Xunit;

namespace Ouroboros.Tests.Network;

[Trait("Category", "Unit")]
public sealed class QdrantDagStoreTests : IAsyncDisposable
{
    private readonly QdrantClient _client;
    private readonly Mock<IQdrantCollectionRegistry> _registry;
    private readonly QdrantSettings _settings;

    public QdrantDagStoreTests()
    {
        _client = new QdrantClient("localhost", 6334);
        _registry = new Mock<IQdrantCollectionRegistry>();
        _registry.Setup(r => r.GetCollectionName(QdrantCollectionRole.DagNodes))
            .Returns("test_dag_nodes");
        _registry.Setup(r => r.GetCollectionName(QdrantCollectionRole.DagEdges))
            .Returns("test_dag_edges");
        _settings = new QdrantSettings
        {
            GrpcEndpoint = "http://localhost:6334",
            DefaultVectorSize = 384,
            UseHttps = false
        };
    }

    #region Constructor Tests

    [Fact]
    public void Ctor_NullClient_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new QdrantDagStore(null!, _registry.Object, _settings);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("client");
    }

    [Fact]
    public void Ctor_NullRegistry_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new QdrantDagStore(_client, null!, _settings);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("registry");
    }

    [Fact]
    public void Ctor_NullSettings_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new QdrantDagStore(_client, _registry.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("settings");
    }

    [Fact]
    public void Ctor_ValidArgs_SetsSupportsSemanticSearchFalse()
    {
        // Act
        var store = new QdrantDagStore(_client, _registry.Object, _settings);

        // Assert
        store.SupportsSemanticSearch.Should().BeFalse();
    }

    [Fact]
    public void Ctor_WithEmbeddingFunc_SetsSupportsSemanticSearchTrue()
    {
        // Arrange
        Func<string, Task<float[]>> embeddingFunc = _ => Task.FromResult(new float[384]);

        // Act
        var store = new QdrantDagStore(_client, _registry.Object, _settings, embeddingFunc);

        // Assert
        store.SupportsSemanticSearch.Should().BeTrue();
    }

    #endregion

    #region SaveNodeAsync Null Check

    [Fact]
    public async Task SaveNodeAsync_NullNode_ReturnsFailure()
    {
        // Arrange
        var store = new QdrantDagStore(_client, _registry.Object, _settings);

        // Act
        var result = await store.SaveNodeAsync(null!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("null");
    }

    #endregion

    #region SaveEdgeAsync Null Check

    [Fact]
    public async Task SaveEdgeAsync_NullEdge_ReturnsFailure()
    {
        // Arrange
        var store = new QdrantDagStore(_client, _registry.Object, _settings);

        // Act
        var result = await store.SaveEdgeAsync(null!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("null");
    }

    #endregion

    #region SaveDagAsync Null Check

    [Fact]
    public async Task SaveDagAsync_NullDag_ReturnsFailure()
    {
        // Arrange
        var store = new QdrantDagStore(_client, _registry.Object, _settings);

        // Act
        var result = await store.SaveDagAsync(null!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("null");
    }

    #endregion

    #region SearchNodesAsync Without Embedding

    [Fact]
    public async Task SearchNodesAsync_NoEmbeddingFunc_ReturnsFailure()
    {
        // Arrange
        var store = new QdrantDagStore(_client, _registry.Object, _settings);

        // Act
        var result = await store.SearchNodesAsync("query");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("embedding function");
    }

    #endregion

    #region DeserializeNode Tests (via reflection)

    [Fact]
    public void DeserializeNode_ValidPayload_ReturnsMonadNode()
    {
        // Arrange
        var id = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var payload = CreateNodePayload(id, "TestType", "{\"key\":\"val\"}", now, parentId.ToString());

        // Act
        var node = InvokeDeserializeNode(payload);

        // Assert
        node.Should().NotBeNull();
        node!.Id.Should().Be(id);
        node.TypeName.Should().Be("TestType");
        node.PayloadJson.Should().Be("{\"key\":\"val\"}");
        node.CreatedAt.Should().Be(now);
        node.ParentIds.Should().ContainSingle().Which.Should().Be(parentId);
    }

    [Fact]
    public void DeserializeNode_EmptyParentIds_ReturnsNodeWithEmptyParents()
    {
        // Arrange
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var payload = CreateNodePayload(id, "Root", "{}", now, "");

        // Act
        var node = InvokeDeserializeNode(payload);

        // Assert
        node.Should().NotBeNull();
        node!.ParentIds.Should().BeEmpty();
    }

    [Fact]
    public void DeserializeNode_InvalidGuidFormat_ReturnsNull()
    {
        // Arrange
        var payload = new Dictionary<string, Value>
        {
            ["id"] = new Value { StringValue = "not-a-guid" },
            ["type_name"] = new Value { StringValue = "Test" },
            ["payload_json"] = new Value { StringValue = "{}" },
            ["created_at"] = new Value { StringValue = DateTimeOffset.UtcNow.ToString("O") },
            ["parent_ids"] = new Value { StringValue = "" },
        };

        // Act
        var node = InvokeDeserializeNode(payload);

        // Assert
        node.Should().BeNull();
    }

    [Fact]
    public void DeserializeNode_MissingKey_ReturnsNull()
    {
        // Arrange
        var payload = new Dictionary<string, Value>
        {
            ["id"] = new Value { StringValue = Guid.NewGuid().ToString() },
            // Missing type_name and other keys
        };

        // Act
        var node = InvokeDeserializeNode(payload);

        // Assert
        node.Should().BeNull();
    }

    [Fact]
    public void DeserializeNode_MultipleParentIds_ParsesAll()
    {
        // Arrange
        var id = Guid.NewGuid();
        var parent1 = Guid.NewGuid();
        var parent2 = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var payload = CreateNodePayload(id, "Child", "{}", now, $"{parent1},{parent2}");

        // Act
        var node = InvokeDeserializeNode(payload);

        // Assert
        node.Should().NotBeNull();
        node!.ParentIds.Should().HaveCount(2);
        node.ParentIds.Should().Contain(parent1);
        node.ParentIds.Should().Contain(parent2);
    }

    #endregion

    #region DeserializeEdge Tests (via reflection)

    [Fact]
    public void DeserializeEdge_ValidPayload_ReturnsTransitionEdge()
    {
        // Arrange
        var id = Guid.NewGuid();
        var inputId = Guid.NewGuid();
        var outputId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var payload = CreateEdgePayload(id, inputId.ToString(), outputId, "Transform", "{}", now, 0.95, 150);

        // Act
        var edge = InvokeDeserializeEdge(payload);

        // Assert
        edge.Should().NotBeNull();
        edge!.Id.Should().Be(id);
        edge.InputIds.Should().ContainSingle().Which.Should().Be(inputId);
        edge.OutputId.Should().Be(outputId);
        edge.OperationName.Should().Be("Transform");
        edge.Confidence.Should().Be(0.95);
        edge.DurationMs.Should().Be(150);
    }

    [Fact]
    public void DeserializeEdge_NoConfidenceOrDuration_ReturnsNullOptionalFields()
    {
        // Arrange
        var id = Guid.NewGuid();
        var inputId = Guid.NewGuid();
        var outputId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var payload = new Dictionary<string, Value>
        {
            ["id"] = new Value { StringValue = id.ToString() },
            ["input_ids"] = new Value { StringValue = inputId.ToString() },
            ["output_id"] = new Value { StringValue = outputId.ToString() },
            ["operation_name"] = new Value { StringValue = "Op" },
            ["operation_spec_json"] = new Value { StringValue = "{}" },
            ["created_at"] = new Value { StringValue = now.ToString("O") },
        };

        // Act
        var edge = InvokeDeserializeEdge(payload);

        // Assert
        edge.Should().NotBeNull();
        edge!.Confidence.Should().BeNull();
        edge.DurationMs.Should().BeNull();
    }

    [Fact]
    public void DeserializeEdge_InvalidGuid_ReturnsNull()
    {
        // Arrange
        var payload = new Dictionary<string, Value>
        {
            ["id"] = new Value { StringValue = "bad-guid" },
            ["input_ids"] = new Value { StringValue = Guid.NewGuid().ToString() },
            ["output_id"] = new Value { StringValue = Guid.NewGuid().ToString() },
            ["operation_name"] = new Value { StringValue = "Op" },
            ["operation_spec_json"] = new Value { StringValue = "{}" },
            ["created_at"] = new Value { StringValue = DateTimeOffset.UtcNow.ToString("O") },
        };

        // Act
        var edge = InvokeDeserializeEdge(payload);

        // Assert
        edge.Should().BeNull();
    }

    [Fact]
    public void DeserializeEdge_MissingKey_ReturnsNull()
    {
        // Arrange
        var payload = new Dictionary<string, Value>
        {
            ["id"] = new Value { StringValue = Guid.NewGuid().ToString() },
        };

        // Act
        var edge = InvokeDeserializeEdge(payload);

        // Assert
        edge.Should().BeNull();
    }

    [Fact]
    public void DeserializeEdge_MultipleInputIds_ParsesAll()
    {
        // Arrange
        var id = Guid.NewGuid();
        var input1 = Guid.NewGuid();
        var input2 = Guid.NewGuid();
        var outputId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var payload = CreateEdgePayload(id, $"{input1},{input2}", outputId, "Merge", "{}", now);

        // Act
        var edge = InvokeDeserializeEdge(payload);

        // Assert
        edge.Should().NotBeNull();
        edge!.InputIds.Should().HaveCount(2);
        edge.InputIds.Should().Contain(input1);
        edge.InputIds.Should().Contain(input2);
    }

    #endregion

    #region TopologicalSort Tests (via reflection)

    [Fact]
    public void TopologicalSort_EmptyList_ReturnsEmpty()
    {
        // Arrange
        var nodes = Array.Empty<MonadNode>();

        // Act
        var result = InvokeTopologicalSort(nodes);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void TopologicalSort_SingleNode_ReturnsSameNode()
    {
        // Arrange
        var node = new MonadNode(Guid.NewGuid(), "Root", "{}", DateTimeOffset.UtcNow, ImmutableArray<Guid>.Empty);

        // Act
        var result = InvokeTopologicalSort(new[] { node });

        // Assert
        result.Should().ContainSingle().Which.Should().Be(node);
    }

    [Fact]
    public void TopologicalSort_ParentBeforeChild_PreservesOrder()
    {
        // Arrange
        var parentId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var parent = new MonadNode(parentId, "Parent", "{}", DateTimeOffset.UtcNow, ImmutableArray<Guid>.Empty);
        var child = new MonadNode(childId, "Child", "{}", DateTimeOffset.UtcNow, ImmutableArray.Create(parentId));

        // Act
        var result = InvokeTopologicalSort(new[] { parent, child });

        // Assert
        result.Should().HaveCount(2);
        result.IndexOf(parent).Should().BeLessThan(result.IndexOf(child));
    }

    [Fact]
    public void TopologicalSort_ChildBeforeParent_ReordersCorrectly()
    {
        // Arrange
        var parentId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var parent = new MonadNode(parentId, "Parent", "{}", DateTimeOffset.UtcNow, ImmutableArray<Guid>.Empty);
        var child = new MonadNode(childId, "Child", "{}", DateTimeOffset.UtcNow, ImmutableArray.Create(parentId));

        // Act - pass child before parent
        var result = InvokeTopologicalSort(new[] { child, parent });

        // Assert
        result.Should().HaveCount(2);
        result.IndexOf(parent).Should().BeLessThan(result.IndexOf(child));
    }

    [Fact]
    public void TopologicalSort_ThreeLevelChain_OrderedCorrectly()
    {
        // Arrange
        var grandparentId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var childId = Guid.NewGuid();

        var grandparent = new MonadNode(grandparentId, "GP", "{}", DateTimeOffset.UtcNow, ImmutableArray<Guid>.Empty);
        var parent = new MonadNode(parentId, "P", "{}", DateTimeOffset.UtcNow, ImmutableArray.Create(grandparentId));
        var child = new MonadNode(childId, "C", "{}", DateTimeOffset.UtcNow, ImmutableArray.Create(parentId));

        // Act - deliberately reversed
        var result = InvokeTopologicalSort(new[] { child, parent, grandparent });

        // Assert
        result.Should().HaveCount(3);
        result.IndexOf(grandparent).Should().BeLessThan(result.IndexOf(parent));
        result.IndexOf(parent).Should().BeLessThan(result.IndexOf(child));
    }

    [Fact]
    public void TopologicalSort_MissingParent_HandlesGracefully()
    {
        // Arrange
        var missingParentId = Guid.NewGuid();
        var child = new MonadNode(Guid.NewGuid(), "Orphan", "{}", DateTimeOffset.UtcNow,
            ImmutableArray.Create(missingParentId));

        // Act
        var result = InvokeTopologicalSort(new[] { child });

        // Assert - should not throw and should contain the orphan node
        result.Should().ContainSingle();
    }

    #endregion

    #region DisposeAsync Tests

    [Fact]
    public async Task DisposeAsync_DoesNotThrow()
    {
        // Arrange
        var store = new QdrantDagStore(_client, _registry.Object, _settings);

        // Act & Assert
        await FluentActions.Invoking(async () => await store.DisposeAsync())
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task DisposeAsync_CalledTwice_DoesNotThrow()
    {
        // Arrange
        var store = new QdrantDagStore(_client, _registry.Object, _settings);

        // Act
        await store.DisposeAsync();

        // Assert
        await FluentActions.Invoking(async () => await store.DisposeAsync())
            .Should().NotThrowAsync();
    }

    #endregion

    #region Helper Methods

    private static Dictionary<string, Value> CreateNodePayload(
        Guid id, string typeName, string payloadJson, DateTimeOffset createdAt, string parentIds)
    {
        return new Dictionary<string, Value>
        {
            ["id"] = new Value { StringValue = id.ToString() },
            ["type_name"] = new Value { StringValue = typeName },
            ["payload_json"] = new Value { StringValue = payloadJson },
            ["created_at"] = new Value { StringValue = createdAt.ToString("O") },
            ["parent_ids"] = new Value { StringValue = parentIds },
        };
    }

    private static Dictionary<string, Value> CreateEdgePayload(
        Guid id, string inputIds, Guid outputId, string opName, string opSpec,
        DateTimeOffset createdAt, double? confidence = null, long? durationMs = null)
    {
        var payload = new Dictionary<string, Value>
        {
            ["id"] = new Value { StringValue = id.ToString() },
            ["input_ids"] = new Value { StringValue = inputIds },
            ["output_id"] = new Value { StringValue = outputId.ToString() },
            ["operation_name"] = new Value { StringValue = opName },
            ["operation_spec_json"] = new Value { StringValue = opSpec },
            ["created_at"] = new Value { StringValue = createdAt.ToString("O") },
        };

        if (confidence.HasValue)
        {
            payload["confidence"] = new Value { DoubleValue = confidence.Value };
        }

        if (durationMs.HasValue)
        {
            payload["duration_ms"] = new Value { IntegerValue = durationMs.Value };
        }

        return payload;
    }

    private static MonadNode? InvokeDeserializeNode(IDictionary<string, Value> payload)
    {
        var method = typeof(QdrantDagStore)
            .GetMethod("DeserializeNode", BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull("DeserializeNode method must exist");
        return (MonadNode?)method!.Invoke(null, new object[] { payload });
    }

    private static TransitionEdge? InvokeDeserializeEdge(IDictionary<string, Value> payload)
    {
        var method = typeof(QdrantDagStore)
            .GetMethod("DeserializeEdge", BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull("DeserializeEdge method must exist");
        return (TransitionEdge?)method!.Invoke(null, new object[] { payload });
    }

    private static List<MonadNode> InvokeTopologicalSort(IReadOnlyList<MonadNode> nodes)
    {
        var method = typeof(QdrantDagStore)
            .GetMethod("TopologicalSort", BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull("TopologicalSort method must exist");
        return ((IReadOnlyList<MonadNode>)method!.Invoke(null, new object[] { nodes })!).ToList();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await Task.CompletedTask;
    }

    #endregion
}
