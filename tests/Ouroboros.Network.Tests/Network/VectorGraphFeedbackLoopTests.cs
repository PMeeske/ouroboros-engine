// <copyright file="VectorGraphFeedbackLoopTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Network;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using FluentAssertions;
using Ouroboros.Network;
using Xunit;

/// <summary>
/// Tests for VectorGraphFeedbackLoop graph modification logic.
/// Validates strengthen, weaken, merge operations and MerkleDag mutation methods.
/// </summary>
[Trait("Category", "Unit")]
public class VectorGraphFeedbackLoopTests
{
    private static MonadNode CreateNode(
        Guid? id = null,
        string typeName = "TestType",
        string payload = "{}",
        params Guid[] parentIds)
    {
        return new MonadNode(
            id ?? Guid.NewGuid(),
            typeName,
            payload,
            DateTimeOffset.UtcNow,
            parentIds.ToImmutableArray());
    }

    // === MerkleDag Mutation Method Tests ===

    [Fact]
    public void RemoveEdge_ExistingEdge_RemovesEdgeAndUpdatesAdjacency()
    {
        // Arrange
        var dag = new MerkleDag();
        var n1 = CreateNode();
        var n2 = CreateNode();
        dag.AddNode(n1);
        dag.AddNode(n2);
        var edge = TransitionEdge.CreateSimple(n1.Id, n2.Id, "Test", new { }, confidence: 0.8);
        dag.AddEdge(edge);

        // Act
        var result = dag.RemoveEdge(edge.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        dag.EdgeCount.Should().Be(0);
        dag.GetOutgoingEdges(n1.Id).Should().BeEmpty();
        dag.GetIncomingEdges(n2.Id).Should().BeEmpty();
    }

    [Fact]
    public void RemoveEdge_NonExistentEdge_ReturnsFailure()
    {
        // Arrange
        var dag = new MerkleDag();

        // Act
        var result = dag.RemoveEdge(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void UpdateEdge_ExistingEdge_UpdatesConfidence()
    {
        // Arrange
        var dag = new MerkleDag();
        var n1 = CreateNode();
        var n2 = CreateNode();
        dag.AddNode(n1);
        dag.AddNode(n2);
        var edge = TransitionEdge.CreateSimple(n1.Id, n2.Id, "Test", new { }, confidence: 0.5);
        dag.AddEdge(edge);

        // Act
        var updatedEdge = new TransitionEdge(
            edge.Id,
            edge.InputIds,
            edge.OutputId,
            edge.OperationName,
            edge.OperationSpecJson,
            edge.CreatedAt,
            confidence: 0.9,
            durationMs: edge.DurationMs);

        var result = dag.UpdateEdge(updatedEdge);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var retrievedEdge = dag.GetEdge(edge.Id);
        retrievedEdge.HasValue.Should().BeTrue();
        retrievedEdge.Value!.Confidence.Should().Be(0.9);
    }

    [Fact]
    public void UpdateEdge_NonExistentEdge_ReturnsFailure()
    {
        // Arrange
        var dag = new MerkleDag();
        var n1 = CreateNode();
        var n2 = CreateNode();
        dag.AddNode(n1);
        dag.AddNode(n2);
        var edge = TransitionEdge.CreateSimple(n1.Id, n2.Id, "Test", new { }, confidence: 0.5);

        // Act - update without adding first
        var result = dag.UpdateEdge(edge);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RemoveNode_ExistingNode_RemovesNodeAndAllConnectedEdges()
    {
        // Arrange - n1 -> target -> n3 (target will be removed)
        var dag = new MerkleDag();
        var n1 = CreateNode();
        var target = CreateNode();
        var n3 = CreateNode();
        dag.AddNode(n1);
        dag.AddNode(target);
        dag.AddNode(n3);
        var edge1 = TransitionEdge.CreateSimple(n1.Id, target.Id, "To", new { }, confidence: 0.5);
        var edge2 = TransitionEdge.CreateSimple(target.Id, n3.Id, "From", new { }, confidence: 0.5);
        dag.AddEdge(edge1);
        dag.AddEdge(edge2);

        // Act
        var result = dag.RemoveNode(target.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        dag.NodeCount.Should().Be(2);
        dag.EdgeCount.Should().Be(0);
        dag.GetNode(target.Id).HasValue.Should().BeFalse();
    }

    [Fact]
    public void RemoveNode_NonExistentNode_ReturnsFailure()
    {
        // Arrange
        var dag = new MerkleDag();

        // Act
        var result = dag.RemoveNode(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void UpdateNode_ExistingNode_UpdatesPayload()
    {
        // Arrange
        var dag = new MerkleDag();
        var node = CreateNode(typeName: "Draft", payload: "{\"text\":\"original\"}");
        dag.AddNode(node);

        // Act
        var updatedNode = new MonadNode(
            node.Id,
            node.TypeName,
            "{\"text\":\"updated\"}",
            node.CreatedAt,
            node.ParentIds);

        var result = dag.UpdateNode(updatedNode);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var retrieved = dag.GetNode(node.Id);
        retrieved.HasValue.Should().BeTrue();
        retrieved.Value!.PayloadJson.Should().Contain("updated");
    }

    [Fact]
    public void UpdateNode_NonExistentNode_ReturnsFailure()
    {
        // Arrange
        var dag = new MerkleDag();
        var node = CreateNode(typeName: "Draft", payload: "{}");

        // Act
        var result = dag.UpdateNode(node);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void GetEdgesFrom_ReturnsOutgoingEdgesForNode()
    {
        // Arrange
        var dag = new MerkleDag();
        var n1 = CreateNode();
        var n2 = CreateNode();
        var n3 = CreateNode();
        dag.AddNode(n1);
        dag.AddNode(n2);
        dag.AddNode(n3);
        var edge1 = TransitionEdge.CreateSimple(n1.Id, n2.Id, "A", new { }, confidence: 0.6);
        var edge2 = TransitionEdge.CreateSimple(n1.Id, n3.Id, "B", new { }, confidence: 0.4);
        dag.AddEdge(edge1);
        dag.AddEdge(edge2);

        // Act
        var edges = dag.GetEdgesFrom(n1.Id);

        // Assert
        edges.Should().HaveCount(2);
        edges.Values.Should().Contain(e => e.OutputId == n2.Id);
        edges.Values.Should().Contain(e => e.OutputId == n3.Id);
    }

    [Fact]
    public void GetEdgesFrom_NodeWithNoOutgoingEdges_ReturnsEmpty()
    {
        // Arrange
        var dag = new MerkleDag();
        var n1 = CreateNode();
        dag.AddNode(n1);

        // Act
        var edges = dag.GetEdgesFrom(n1.Id);

        // Assert
        edges.Should().BeEmpty();
    }

    // === ApplyStrengthen Tests ===

    [Fact]
    public void ApplyStrengthen_IncreasesEdgeWeights()
    {
        // Arrange
        var dag = new MerkleDag();
        var source = CreateNode(typeName: "Source", payload: "{\"data\":\"source\"}");
        var sink = CreateNode(typeName: "Sink", payload: "{\"data\":\"sink\"}");
        dag.AddNode(source);
        dag.AddNode(sink);

        var edge = TransitionEdge.CreateSimple(
            source.Id, sink.Id, "Connect", new { }, confidence: 0.5);
        dag.AddEdge(edge);

        var modifiedNodes = new HashSet<Guid>();

        // Act
        var result = VectorGraphFeedbackLoop.ApplyStrengthen(dag, source.Id, modifiedNodes);

        // Assert
        result.Should().BeTrue();
        var outgoingEdges = dag.GetOutgoingEdges(source.Id).ToList();
        outgoingEdges.Should().HaveCount(1);
        outgoingEdges[0].Confidence.Should().BeApproximately(0.55, 0.01);
        modifiedNodes.Should().Contain(source.Id);
        modifiedNodes.Should().Contain(sink.Id);
    }

    [Fact]
    public void ApplyStrengthen_CapsAtMaxWeight()
    {
        // Arrange - edge already at 0.95 confidence
        var dag = new MerkleDag();
        var source = CreateNode();
        var sink = CreateNode();
        dag.AddNode(source);
        dag.AddNode(sink);
        var edge = TransitionEdge.CreateSimple(
            source.Id, sink.Id, "Connect", new { }, confidence: 0.95);
        dag.AddEdge(edge);

        var modifiedNodes = new HashSet<Guid>();

        // Act - 0.95 * 1.1 = 1.045, capped at 1.0
        VectorGraphFeedbackLoop.ApplyStrengthen(dag, source.Id, modifiedNodes);

        // Assert
        var outgoingEdges = dag.GetOutgoingEdges(source.Id).ToList();
        outgoingEdges[0].Confidence.Should().Be(1.0);
    }

    [Fact]
    public void ApplyStrengthen_NoEdges_ReturnsFalse()
    {
        // Arrange - isolated node with no edges
        var dag = new MerkleDag();
        var isolated = CreateNode();
        dag.AddNode(isolated);

        var modifiedNodes = new HashSet<Guid>();

        // Act
        var result = VectorGraphFeedbackLoop.ApplyStrengthen(dag, isolated.Id, modifiedNodes);

        // Assert
        result.Should().BeFalse();
        modifiedNodes.Should().BeEmpty();
    }

    // === ApplyWeaken Tests ===

    [Fact]
    public void ApplyWeaken_ReducesEdgeWeights()
    {
        // Arrange
        var dag = new MerkleDag();
        var source = CreateNode();
        var sink = CreateNode();
        dag.AddNode(source);
        dag.AddNode(sink);
        var edge = TransitionEdge.CreateSimple(
            source.Id, sink.Id, "Connect", new { }, confidence: 0.5);
        dag.AddEdge(edge);

        var modifiedNodes = new HashSet<Guid>();

        // Act
        var result = VectorGraphFeedbackLoop.ApplyWeaken(dag, source.Id, modifiedNodes);

        // Assert
        result.Should().BeTrue();
        var outgoingEdges = dag.GetOutgoingEdges(source.Id).ToList();
        outgoingEdges.Should().HaveCount(1);
        outgoingEdges[0].Confidence.Should().BeApproximately(0.4, 0.01);
        modifiedNodes.Should().Contain(source.Id);
    }

    [Fact]
    public void ApplyWeaken_PrunesBelowThreshold()
    {
        // Arrange - edge at 0.04 confidence, after weakening: 0.04 * 0.8 = 0.032 < 0.05
        var dag = new MerkleDag();
        var source = CreateNode();
        var sink = CreateNode();
        dag.AddNode(source);
        dag.AddNode(sink);
        var edge = TransitionEdge.CreateSimple(
            source.Id, sink.Id, "Connect", new { }, confidence: 0.04);
        dag.AddEdge(edge);

        var modifiedNodes = new HashSet<Guid>();

        // Act
        var result = VectorGraphFeedbackLoop.ApplyWeaken(dag, source.Id, modifiedNodes);

        // Assert - edge should be pruned
        result.Should().BeTrue();
        var outgoingEdges = dag.GetOutgoingEdges(source.Id).ToList();
        outgoingEdges.Should().BeEmpty();
        modifiedNodes.Should().Contain(source.Id);
        modifiedNodes.Should().Contain(sink.Id);
    }

    [Fact]
    public void ApplyWeaken_NoEdges_ReturnsFalse()
    {
        // Arrange - isolated node
        var dag = new MerkleDag();
        var isolated = CreateNode();
        dag.AddNode(isolated);

        var modifiedNodes = new HashSet<Guid>();

        // Act
        var result = VectorGraphFeedbackLoop.ApplyWeaken(dag, isolated.Id, modifiedNodes);

        // Assert
        result.Should().BeFalse();
        modifiedNodes.Should().BeEmpty();
    }

    // === ApplyMerge Tests ===

    [Fact]
    public void ApplyMerge_CombinesPayloads()
    {
        // Arrange - sink1 -> sink2 (sink2 is the merge partner)
        var dag = new MerkleDag();
        var sink1 = CreateNode(typeName: "Sink", payload: "{\"name\":\"sink1\",\"value\":1}");
        var sink2 = CreateNode(typeName: "Sink", payload: "{\"name\":\"sink2\",\"value\":2}");
        dag.AddNode(sink1);
        dag.AddNode(sink2);

        // Edge from sink1 -> sink2 (sink2 is the highest-weight target = merge partner)
        var edge = TransitionEdge.CreateSimple(
            sink1.Id, sink2.Id, "Relate", new { }, confidence: 0.8);
        dag.AddEdge(edge);

        // Also add an incoming edge to sink2 from a third node
        var third = CreateNode(typeName: "Other", payload: "{}");
        dag.AddNode(third);
        var inboundEdge = TransitionEdge.CreateSimple(
            third.Id, sink2.Id, "Connect", new { }, confidence: 0.6);
        dag.AddEdge(inboundEdge);

        var modifiedNodes = new HashSet<Guid>();

        // Act
        var result = VectorGraphFeedbackLoop.ApplyMerge(dag, sink1.Id, modifiedNodes);

        // Assert
        result.Should().BeTrue();

        // sink1 should still exist with merged payload
        var mergedNode = dag.GetNode(sink1.Id);
        mergedNode.HasValue.Should().BeTrue();
        mergedNode.Value!.PayloadJson.Should().Contain("sink2");

        // sink2 should be removed
        var removedNode = dag.GetNode(sink2.Id);
        removedNode.HasValue.Should().BeFalse();

        // The inbound edge from third->sink2 should now point to sink1
        var incomingToSink1 = dag.GetIncomingEdges(sink1.Id).ToList();
        incomingToSink1.Should().HaveCount(1);
        incomingToSink1[0].InputIds.Should().Contain(third.Id);

        // Both node IDs should be tracked for re-embedding
        modifiedNodes.Should().Contain(sink1.Id);
        modifiedNodes.Should().Contain(sink2.Id);
    }

    [Fact]
    public void ApplyMerge_NoOutboundEdges_ReturnsFalse()
    {
        // Arrange - node with no outbound edges
        var dag = new MerkleDag();
        var sink = CreateNode(typeName: "Sink", payload: "{}");
        dag.AddNode(sink);

        var modifiedNodes = new HashSet<Guid>();

        // Act
        var result = VectorGraphFeedbackLoop.ApplyMerge(dag, sink.Id, modifiedNodes);

        // Assert
        result.Should().BeFalse();
        modifiedNodes.Should().BeEmpty();
    }

    [Fact]
    public void ApplyMerge_NonExistentNode_ReturnsFalse()
    {
        // Arrange
        var dag = new MerkleDag();
        var modifiedNodes = new HashSet<Guid>();

        // Act
        var result = VectorGraphFeedbackLoop.ApplyMerge(dag, Guid.NewGuid(), modifiedNodes);

        // Assert
        result.Should().BeFalse();
        modifiedNodes.Should().BeEmpty();
    }

    // === ApplyDefault Tests ===

    [Fact]
    public void ApplyDefault_TracksNodeAsModified()
    {
        // Arrange
        var dag = new MerkleDag();
        var nodeId = Guid.NewGuid();
        var modifiedNodes = new HashSet<Guid>();

        // Act
        var result = VectorGraphFeedbackLoop.ApplyDefault(dag, nodeId, modifiedNodes);

        // Assert
        result.Should().BeTrue();
        modifiedNodes.Should().Contain(nodeId);
    }

    // === Edge Case Tests ===

    [Fact]
    public void ApplyStrengthen_MultipleEdges_StrengthenAll()
    {
        // Arrange - node with 3 outgoing edges
        var dag = new MerkleDag();
        var source = CreateNode();
        dag.AddNode(source);
        var targets = new List<MonadNode>();
        for (int i = 0; i < 3; i++)
        {
            var t = CreateNode();
            dag.AddNode(t);
            targets.Add(t);
            dag.AddEdge(TransitionEdge.CreateSimple(
                source.Id, t.Id, $"Edge{i}", new { }, confidence: 0.3));
        }
        var modifiedNodes = new HashSet<Guid>();

        // Act
        var result = VectorGraphFeedbackLoop.ApplyStrengthen(dag, source.Id, modifiedNodes);

        // Assert
        result.Should().BeTrue();
        var outgoing = dag.GetOutgoingEdges(source.Id).ToList();
        outgoing.Should().HaveCount(3);
        foreach (var e in outgoing)
        {
            e.Confidence.Should().BeApproximately(0.33, 0.01);
        }

        modifiedNodes.Should().Contain(source.Id);
        foreach (var t in targets)
        {
            modifiedNodes.Should().Contain(t.Id);
        }
    }

    [Fact]
    public void ApplyWeaken_EdgeExactlyAtThreshold_NotPruned()
    {
        // Arrange - edge at exactly 0.0625 confidence, after weakening: 0.0625 * 0.8 = 0.05
        var dag = new MerkleDag();
        var source = CreateNode();
        var sink = CreateNode();
        dag.AddNode(source);
        dag.AddNode(sink);
        var edge = TransitionEdge.CreateSimple(
            source.Id, sink.Id, "Connect", new { }, confidence: 0.0625);
        dag.AddEdge(edge);

        var modifiedNodes = new HashSet<Guid>();

        // Act
        var result = VectorGraphFeedbackLoop.ApplyWeaken(dag, source.Id, modifiedNodes);

        // Assert - edge at exactly 0.05 should NOT be pruned (threshold is strictly below 0.05)
        result.Should().BeTrue();
        var outgoing = dag.GetOutgoingEdges(source.Id).ToList();
        outgoing.Should().HaveCount(1);
        outgoing[0].Confidence.Should().BeApproximately(0.05, 0.001);
    }

    [Fact]
    public void ApplyMerge_SelectsHighestWeightPartner()
    {
        // Arrange - node with two outgoing edges of different weights
        var dag = new MerkleDag();
        var source = CreateNode(typeName: "Source", payload: "{\"id\":\"source\"}");
        var lowTarget = CreateNode(typeName: "Low", payload: "{\"id\":\"low\"}");
        var highTarget = CreateNode(typeName: "High", payload: "{\"id\":\"high\"}");
        dag.AddNode(source);
        dag.AddNode(lowTarget);
        dag.AddNode(highTarget);

        var lowEdge = TransitionEdge.CreateSimple(
            source.Id, lowTarget.Id, "Weak", new { }, confidence: 0.3);
        var highEdge = TransitionEdge.CreateSimple(
            source.Id, highTarget.Id, "Strong", new { }, confidence: 0.9);
        dag.AddEdge(lowEdge);
        dag.AddEdge(highEdge);

        var modifiedNodes = new HashSet<Guid>();

        // Act - merge should select highTarget (highest weight)
        var result = VectorGraphFeedbackLoop.ApplyMerge(dag, source.Id, modifiedNodes);

        // Assert
        result.Should().BeTrue();

        // highTarget should be removed (it's the merge partner)
        dag.GetNode(highTarget.Id).HasValue.Should().BeFalse();

        // lowTarget should still exist
        dag.GetNode(lowTarget.Id).HasValue.Should().BeTrue();
    }

    // === MeTTa Feedback Integration Tests ===

    [Fact]
    public async Task ApplyModificationsAsync_MeTTaStrengthenModification_IncreasesEdgeWeights()
    {
        // Arrange - build a dag with a source->sink edge at confidence 0.5
        var dag = new MerkleDag();
        var source = CreateNode(typeName: "Source", payload: "{\"data\":\"source\"}");
        var sink = CreateNode(typeName: "Sink", payload: "{\"data\":\"sink\"}");
        dag.AddNode(source);
        dag.AddNode(sink);
        var edge = TransitionEdge.CreateSimple(
            source.Id, sink.Id, "Connect", new { }, confidence: 0.5);
        dag.AddEdge(edge);

        // Parse a MeTTa strengthen directive
        var mettaOutput = $"(strengthen \"{source.Id}\" \"{sink.Id}\")";
        var modifications = VectorGraphFeedbackLoop.ParseModifications(mettaOutput);
        var modifiedNodes = new HashSet<Guid>();

        // Act
        await VectorGraphFeedbackLoop.ApplyModificationsAsync(dag, modifications, modifiedNodes, CancellationToken.None);

        // Assert - edge confidence should have increased from 0.5
        var outgoingEdges = dag.GetOutgoingEdges(source.Id).ToList();
        outgoingEdges.Should().HaveCount(1);
        outgoingEdges[0].Confidence.Should().BeApproximately(0.55, 0.01);
        modifiedNodes.Should().Contain(source.Id);
        modifiedNodes.Should().Contain(sink.Id);
    }

    [Fact]
    public async Task ApplyModificationsAsync_MeTTaWeakenModification_DecreasesEdgeWeights()
    {
        // Arrange - build a dag with a source->sink edge at confidence 0.5
        var dag = new MerkleDag();
        var source = CreateNode(typeName: "Source", payload: "{\"data\":\"source\"}");
        var sink = CreateNode(typeName: "Sink", payload: "{\"data\":\"sink\"}");
        dag.AddNode(source);
        dag.AddNode(sink);
        var edge = TransitionEdge.CreateSimple(
            source.Id, sink.Id, "Connect", new { }, confidence: 0.5);
        dag.AddEdge(edge);

        // Parse a MeTTa weaken directive
        var mettaOutput = $"(weaken-outgoing-edges \"{source.Id}\")";
        var modifications = VectorGraphFeedbackLoop.ParseModifications(mettaOutput);
        var modifiedNodes = new HashSet<Guid>();

        // Act
        await VectorGraphFeedbackLoop.ApplyModificationsAsync(dag, modifications, modifiedNodes, CancellationToken.None);

        // Assert - edge confidence should have decreased from 0.5
        var outgoingEdges = dag.GetOutgoingEdges(source.Id).ToList();
        outgoingEdges.Should().HaveCount(1);
        outgoingEdges[0].Confidence.Should().BeApproximately(0.4, 0.01);
        modifiedNodes.Should().Contain(source.Id);
    }

    [Fact]
    public async Task ApplyModificationsAsync_MeTTaMergeModification_CombinesNodes()
    {
        // Arrange - sink1 -> sink2 with an incoming edge from a third node
        var dag = new MerkleDag();
        var sink1 = CreateNode(typeName: "Sink", payload: "{\"name\":\"sink1\",\"value\":1}");
        var sink2 = CreateNode(typeName: "Sink", payload: "{\"name\":\"sink2\",\"value\":2}");
        var third = CreateNode(typeName: "Other", payload: "{}");
        dag.AddNode(sink1);
        dag.AddNode(sink2);
        dag.AddNode(third);

        var outEdge = TransitionEdge.CreateSimple(
            sink1.Id, sink2.Id, "Relate", new { }, confidence: 0.8);
        dag.AddEdge(outEdge);

        var inboundEdge = TransitionEdge.CreateSimple(
            third.Id, sink2.Id, "Connect", new { }, confidence: 0.6);
        dag.AddEdge(inboundEdge);

        // Parse a MeTTa merge directive targeting both sink nodes
        var mettaOutput = $"(merge-sinks \"{sink1.Id}\" \"{sink2.Id}\")";
        var modifications = VectorGraphFeedbackLoop.ParseModifications(mettaOutput);
        var modifiedNodes = new HashSet<Guid>();

        // Act
        await VectorGraphFeedbackLoop.ApplyModificationsAsync(dag, modifications, modifiedNodes, CancellationToken.None);

        // Assert - sink1 should still exist with merged payload, sink2 should be removed
        var mergedNode = dag.GetNode(sink1.Id);
        mergedNode.HasValue.Should().BeTrue();
        mergedNode.Value!.PayloadJson.Should().Contain("sink2");

        var removedNode = dag.GetNode(sink2.Id);
        removedNode.HasValue.Should().BeFalse();

        modifiedNodes.Should().Contain(sink1.Id);
        modifiedNodes.Should().Contain(sink2.Id);
    }

    [Fact]
    public async Task FeedbackCycle_ClassifyBuildReasonParse_MeTTaModificationsApplyToGraph()
    {
        // Arrange - build a dag with nodes that simulate a source-sink topology
        var dag = new MerkleDag();
        var source = CreateNode(typeName: "Source", payload: "{\"data\":\"source\"}");
        var sink = CreateNode(typeName: "Sink", payload: "{\"data\":\"sink\"}");
        var cyclic = CreateNode(typeName: "Cyclic", payload: "{\"data\":\"cyclic\"}");
        dag.AddNode(source);
        dag.AddNode(sink);
        dag.AddNode(cyclic);

        var sourceToSink = TransitionEdge.CreateSimple(
            source.Id, sink.Id, "Flow", new { }, confidence: 0.5);
        var cyclicToSink = TransitionEdge.CreateSimple(
            cyclic.Id, sink.Id, "Loop", new { }, confidence: 0.3);
        dag.AddEdge(sourceToSink);
        dag.AddEdge(cyclicToSink);

        // Record pre-mutation state
        var preSourceSinkConfidence = dag.GetEdge(sourceToSink.Id)!.Value!.Confidence ?? 0.5;
        var preCyclicSinkConfidence = dag.GetEdge(cyclicToSink.Id)!.Value!.Confidence ?? 0.5;
        preSourceSinkConfidence.Should().Be(0.5);
        preCyclicSinkConfidence.Should().Be(0.3);

        // Simulate MeTTa output that would result from classifying source as
        // semantic-source and sink as semantic-sink, then querying for modifications:
        // strengthen the source->sink edge, weaken the cyclic node's outgoing edges
        var mettaOutput = $"(strengthen \"{source.Id}\" \"{sink.Id}\")\n(weaken \"{cyclic.Id}\")";
        var modifications = VectorGraphFeedbackLoop.ParseModifications(mettaOutput);
        modifications.Should().NotBeEmpty("MeTTa should produce actionable modifications");

        var modifiedNodes = new HashSet<Guid>();

        // Act - apply the parsed MeTTa modifications to the graph
        await VectorGraphFeedbackLoop.ApplyModificationsAsync(dag, modifications, modifiedNodes, CancellationToken.None);

        // Assert - the MerkleDag has been mutated by the MeTTa feedback
        modifiedNodes.Should().NotBeEmpty("at least some nodes should be tracked as modified");

        // Source->sink edge should have been strengthened (confidence increased from 0.5)
        var strengthenedEdge = dag.GetEdge(sourceToSink.Id);
        strengthenedEdge.HasValue.Should().BeTrue();
        strengthenedEdge.Value!.Confidence.Should().BeGreaterThan(preSourceSinkConfidence);

        // Cyclic->sink edge should have been weakened (confidence decreased from 0.3)
        var weakenedEdge = dag.GetEdge(cyclicToSink.Id);
        weakenedEdge.HasValue.Should().BeTrue();
        weakenedEdge.Value!.Confidence.Should().BeLessThan(preCyclicSinkConfidence);
    }
}