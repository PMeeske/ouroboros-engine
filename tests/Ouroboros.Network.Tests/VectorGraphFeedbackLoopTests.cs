// <copyright file="VectorGraphFeedbackLoopTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Network;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ouroboros.Abstractions;
using Ouroboros.Domain;
using Ouroboros.Domain.States;
using Ouroboros.Network;
using Ouroboros.Tools.MeTTa;
using Xunit;

/// <summary>
/// Tests for VectorGraphFeedbackLoop neuro-symbolic reasoning cycle.
/// Validates the complete feedback loop with mocked dependencies.
/// </summary>
[Trait("Category", "Unit")]
public class VectorGraphFeedbackLoopTests
{
    /// <summary>
    /// Mock embedding model that returns fixed-dimension vectors.
    /// </summary>
    private class MockEmbeddingModel : IEmbeddingModel
    {
        private readonly int _dimension;

        public MockEmbeddingModel(int dimension = 384)
        {
            _dimension = dimension;
        }

        public Task<float[]> CreateEmbeddingsAsync(string input, CancellationToken ct = default)
        {
            // Generate deterministic embedding based on hash
            var hash = input.GetHashCode();
            var embedding = new float[_dimension];

            for (int i = 0; i < _dimension; i++)
            {
                embedding[i] = (float)Math.Sin(hash + i) * 0.5f;
            }

            return Task.FromResult(embedding);
        }
    }

    /// <summary>
    /// Mock MeTTa engine that tracks facts and queries.
    /// </summary>
    private class MockMeTTaEngine : IMeTTaEngine
    {
        public List<string> FactsAdded { get; } = new();
        public List<string> RulesApplied { get; } = new();
        public List<string> QueriesExecuted { get; } = new();

        public Task<Result<string, string>> ExecuteQueryAsync(string query, CancellationToken ct = default)
        {
            QueriesExecuted.Add(query);
            // Return empty result to indicate no modifications
            return Task.FromResult(Result<string, string>.Success("[]"));
        }

        public Task<Result<Unit, string>> AddFactAsync(string fact, CancellationToken ct = default)
        {
            FactsAdded.Add(fact);
            return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
        }

        public Task<Result<string, string>> ApplyRuleAsync(string rule, CancellationToken ct = default)
        {
            RulesApplied.Add(rule);
            return Task.FromResult(Result<string, string>.Success("Rule applied"));
        }

        public Task<Result<bool, string>> VerifyPlanAsync(string plan, CancellationToken ct = default)
        {
            return Task.FromResult(Result<bool, string>.Success(true));
        }

        public Task<Result<Unit, string>> ResetAsync(CancellationToken ct = default)
        {
            FactsAdded.Clear();
            RulesApplied.Clear();
            QueriesExecuted.Clear();
            return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
        }

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// Mock Qdrant store that doesn't actually persist.
    /// Uses a real QdrantDagStore but with a non-operational endpoint.
    /// </summary>
    private static QdrantDagStore CreateMockQdrantDagStore()
    {
        // Create a store with a fake endpoint - it won't actually connect
        var config = new QdrantDagConfig("http://localhost:16334", "test-nodes", "test-edges", 384);
        return new QdrantDagStore(config, null);
    }

    [Fact]
    public async Task ExecuteCycleAsync_EmptyGraph_ReturnsSuccessWithZeroModifications()
    {
        // Arrange
        var dag = new MerkleDag();
        var store = CreateMockQdrantDagStore();
        var metta = new MockMeTTaEngine();
        var embedding = new MockEmbeddingModel();
        var loop = new VectorGraphFeedbackLoop(store, metta, embedding);

        // Act
        var result = await loop.ExecuteCycleAsync(dag, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.NodesAnalyzed.Should().Be(0);
        result.Value.NodesModified.Should().Be(0);
        result.Value.SourceNodes.Should().Be(0);
        result.Value.SinkNodes.Should().Be(0);
        result.Value.CyclicNodes.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteCycleAsync_SingleNode_ReturnsSuccessWithOneNodeAnalyzed()
    {
        // Arrange
        var dag = new MerkleDag();
        var node = MonadNode.FromReasoningState(new Draft("Test node"));
        dag.AddNode(node);

        var store = CreateMockQdrantDagStore();
        var metta = new MockMeTTaEngine();
        var embedding = new MockEmbeddingModel();
        var loop = new VectorGraphFeedbackLoop(store, metta, embedding);

        // Act
        var result = await loop.ExecuteCycleAsync(dag, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.NodesAnalyzed.Should().Be(1);
        metta.FactsAdded.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExecuteCycleAsync_LineGraph_ClassifiesSourceAndSink()
    {
        // Arrange
        var dag = new MerkleDag();
        var node1 = MonadNode.FromReasoningState(new Draft("Source node"));
        var node2 = MonadNode.FromReasoningState(new Critique("Middle node"));
        var node3 = MonadNode.FromReasoningState(new Draft("Sink node"));

        dag.AddNode(node1);
        dag.AddNode(node2);
        dag.AddNode(node3);

        dag.AddEdge(TransitionEdge.CreateSimple(node1.Id, node2.Id, "Forward", new { }));
        dag.AddEdge(TransitionEdge.CreateSimple(node2.Id, node3.Id, "Forward", new { }));

        var store = CreateMockQdrantDagStore();
        var metta = new MockMeTTaEngine();
        var embedding = new MockEmbeddingModel();

        var config = new FeedbackLoopConfig(
            DivergenceThreshold: 0.1f, // Lower threshold to detect sources/sinks
            RotationThreshold: 0.3f,
            MaxModificationsPerCycle: 10,
            AutoPersist: false);

        var loop = new VectorGraphFeedbackLoop(store, metta, embedding, config);

        // Act
        var result = await loop.ExecuteCycleAsync(dag, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.NodesAnalyzed.Should().Be(3);

        // Should have added facts about semantic sources, sinks, or neutral nodes
        metta.FactsAdded.Should().Contain(f =>
            f.Contains("semantic-source") ||
            f.Contains("semantic-sink") ||
            f.Contains("semantic-neutral"));

        // Should have applied symbolic reasoning rules
        metta.RulesApplied.Should().NotBeEmpty();

        // Should have queried for modifications
        metta.QueriesExecuted.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExecuteCycleAsync_WithCustomConfig_RespectsMaxModifications()
    {
        // Arrange
        var dag = new MerkleDag();
        for (int i = 0; i < 5; i++)
        {
            var node = MonadNode.FromReasoningState(new Draft($"Node {i}"));
            dag.AddNode(node);
        }

        var store = CreateMockQdrantDagStore();
        var metta = new MockMeTTaEngine();
        var embedding = new MockEmbeddingModel();

        var config = new FeedbackLoopConfig(
            DivergenceThreshold: 0.5f,
            RotationThreshold: 0.3f,
            MaxModificationsPerCycle: 2, // Limit to 2 modifications
            AutoPersist: true);

        var loop = new VectorGraphFeedbackLoop(store, metta, embedding, config);

        // Act
        var result = await loop.ExecuteCycleAsync(dag, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.NodesModified.Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public async Task ExecuteCycleAsync_NullDag_ReturnsFailure()
    {
        // Arrange
        var store = CreateMockQdrantDagStore();
        var metta = new MockMeTTaEngine();
        var embedding = new MockEmbeddingModel();
        var loop = new VectorGraphFeedbackLoop(store, metta, embedding);

        // Act
        var result = await loop.ExecuteCycleAsync(null!, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be null");
    }

    [Fact]
    public async Task ExecuteCycleAsync_ComplexGraph_AddsReasoningCycleFacts()
    {
        // Arrange - Create a graph with multiple interconnected nodes
        var dag = new MerkleDag();
        var nodes = new List<MonadNode>();

        for (int i = 0; i < 4; i++)
        {
            var node = MonadNode.FromReasoningState(new Draft($"Node {i}"));
            dag.AddNode(node);
            nodes.Add(node);
        }

        // Create a diamond pattern
        dag.AddEdge(TransitionEdge.CreateSimple(nodes[0].Id, nodes[1].Id, "Forward", new { }));
        dag.AddEdge(TransitionEdge.CreateSimple(nodes[0].Id, nodes[2].Id, "Forward", new { }));
        dag.AddEdge(TransitionEdge.CreateSimple(nodes[1].Id, nodes[3].Id, "Forward", new { }));
        dag.AddEdge(TransitionEdge.CreateSimple(nodes[2].Id, nodes[3].Id, "Forward", new { }));

        var store = CreateMockQdrantDagStore();
        var metta = new MockMeTTaEngine();
        var embedding = new MockEmbeddingModel();

        var config = new FeedbackLoopConfig(
            DivergenceThreshold: 0.5f,
            RotationThreshold: 0.1f, // Lower threshold to detect cycles
            MaxModificationsPerCycle: 10,
            AutoPersist: false);

        var loop = new VectorGraphFeedbackLoop(store, metta, embedding, config);

        // Act
        var result = await loop.ExecuteCycleAsync(dag, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.NodesAnalyzed.Should().Be(4);

        // Check that facts were added
        metta.FactsAdded.Should().NotBeEmpty();

        // Check that rules were applied
        metta.RulesApplied.Should().ContainSingle();
    }

    [Fact]
    public async Task ExecuteCycleAsync_RecordsDuration()
    {
        // Arrange
        var dag = new MerkleDag();
        var node = MonadNode.FromReasoningState(new Draft("Test"));
        dag.AddNode(node);

        var store = CreateMockQdrantDagStore();
        var metta = new MockMeTTaEngine();
        var embedding = new MockEmbeddingModel();
        var loop = new VectorGraphFeedbackLoop(store, metta, embedding);

        // Act
        var result = await loop.ExecuteCycleAsync(dag, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public void FeedbackLoopConfig_HasCorrectDefaults()
    {
        // Act
        var config = new FeedbackLoopConfig();

        // Assert
        config.DivergenceThreshold.Should().Be(0.5f);
        config.RotationThreshold.Should().Be(0.3f);
        config.MaxModificationsPerCycle.Should().Be(10);
        config.AutoPersist.Should().BeTrue();
    }

    [Fact]
    public void VectorGraphFeedbackLoop_Constructor_ThrowsOnNullStore()
    {
        // Arrange
        var metta = new MockMeTTaEngine();
        var embedding = new MockEmbeddingModel();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new VectorGraphFeedbackLoop(null!, metta, embedding));
    }

    [Fact]
    public void VectorGraphFeedbackLoop_Constructor_ThrowsOnNullMeTTaEngine()
    {
        // Arrange
        var store = CreateMockQdrantDagStore();
        var embedding = new MockEmbeddingModel();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new VectorGraphFeedbackLoop(store, null!, embedding));
    }

    [Fact]
    public void VectorGraphFeedbackLoop_Constructor_ThrowsOnNullEmbeddingModel()
    {
        // Arrange
        var store = CreateMockQdrantDagStore();
        var metta = new MockMeTTaEngine();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new VectorGraphFeedbackLoop(store, metta, null!));
    }

    [Fact]
    public async Task ExecuteCycleAsync_DisconnectedComponents_AnalyzesAll()
    {
        // Arrange - Create two disconnected components
        var dag = new MerkleDag();

        // Component 1
        var node1 = MonadNode.FromReasoningState(new Draft("Component 1 Node 1"));
        var node2 = MonadNode.FromReasoningState(new Draft("Component 1 Node 2"));
        dag.AddNode(node1);
        dag.AddNode(node2);
        dag.AddEdge(TransitionEdge.CreateSimple(node1.Id, node2.Id, "Forward", new { }));

        // Component 2 (disconnected)
        var node3 = MonadNode.FromReasoningState(new Draft("Component 2 Node 1"));
        var node4 = MonadNode.FromReasoningState(new Draft("Component 2 Node 2"));
        dag.AddNode(node3);
        dag.AddNode(node4);
        dag.AddEdge(TransitionEdge.CreateSimple(node3.Id, node4.Id, "Forward", new { }));

        var store = CreateMockQdrantDagStore();
        var metta = new MockMeTTaEngine();
        var embedding = new MockEmbeddingModel();
        var loop = new VectorGraphFeedbackLoop(store, metta, embedding);

        // Act
        var result = await loop.ExecuteCycleAsync(dag, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.NodesAnalyzed.Should().Be(4);
    }
}
