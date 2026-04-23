namespace Ouroboros.Network.Tests;

using System.Collections.Immutable;

[Trait("Category", "Unit")]
public sealed class RecordsTests
{
    #region BranchReasoningSummary

    [Fact]
    public void BranchReasoningSummary_CreatesWithAllProperties()
    {
        // Arrange
        var stepsByKind = ImmutableDictionary<string, int>.Empty.Add("Draft", 3).Add("Critique", 2);
        var duration = TimeSpan.FromMinutes(5);

        // Act
        var summary = new BranchReasoningSummary("MainBranch", 5, stepsByKind, 10, duration);

        // Assert
        summary.BranchName.Should().Be("MainBranch");
        summary.TotalSteps.Should().Be(5);
        summary.StepsByKind.Should().Equal(stepsByKind);
        summary.TotalToolCalls.Should().Be(10);
        summary.TotalDuration.Should().Be(duration);
    }

    [Fact]
    public void BranchReasoningSummary_EmptyStepsByKind_IsValid()
    {
        // Arrange
        var stepsByKind = ImmutableDictionary<string, int>.Empty;

        // Act
        var summary = new BranchReasoningSummary("Empty", 0, stepsByKind, 0, TimeSpan.Zero);

        // Assert
        summary.TotalSteps.Should().Be(0);
        summary.StepsByKind.Should().BeEmpty();
    }

    [Fact]
    public void BranchReasoningSummary_ValueEquality_Works()
    {
        // Arrange
        var steps = ImmutableDictionary<string, int>.Empty.Add("Draft", 1);
        var a = new BranchReasoningSummary("B", 1, steps, 0, TimeSpan.FromSeconds(1));
        var b = new BranchReasoningSummary("B", 1, steps, 0, TimeSpan.FromSeconds(1));

        // Act & Assert
        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void BranchReasoningSummary_DifferentValues_AreNotEqual()
    {
        // Arrange
        var a = new BranchReasoningSummary("A", 1, ImmutableDictionary<string, int>.Empty, 0, TimeSpan.Zero);
        var b = new BranchReasoningSummary("B", 1, ImmutableDictionary<string, int>.Empty, 0, TimeSpan.Zero);

        // Act & Assert
        a.Should().NotBe(b);
    }

    #endregion

    #region DagSaveResult

    [Fact]
    public void DagSaveResult_CreatesWithAllProperties()
    {
        // Arrange
        var errors = new List<string> { "error1", "error2" };

        // Act
        var result = new DagSaveResult(10, 5, errors);

        // Assert
        result.NodesSaved.Should().Be(10);
        result.EdgesSaved.Should().Be(5);
        result.Errors.Should().HaveCount(2);
    }

    [Fact]
    public void DagSaveResult_EmptyErrors_IsValid()
    {
        // Act
        var result = new DagSaveResult(0, 0, Array.Empty<string>());

        // Assert
        result.Errors.Should().BeEmpty();
    }

    #endregion

    #region GlobalNetworkStateDelta

    [Fact]
    public void GlobalNetworkStateDelta_CreatesWithAllProperties()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        var delta = new GlobalNetworkStateDelta(1, 5, 10, 3, timestamp);

        // Assert
        delta.FromEpoch.Should().Be(1);
        delta.ToEpoch.Should().Be(5);
        delta.NodeDelta.Should().Be(10);
        delta.TransitionDelta.Should().Be(3);
        delta.Timestamp.Should().Be(timestamp);
    }

    [Fact]
    public void GlobalNetworkStateDelta_NegativeDelta_IsValid()
    {
        // Act
        var delta = new GlobalNetworkStateDelta(5, 1, -10, -3, DateTimeOffset.UtcNow);

        // Assert
        delta.NodeDelta.Should().Be(-10);
        delta.TransitionDelta.Should().Be(-3);
    }

    #endregion

    #region Learning

    [Fact]
    public void Learning_CreatesWithAllProperties()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        var learning = new Learning("id1", "skill", "Learned X", "context A", 0.95, 42, timestamp);

        // Assert
        learning.Id.Should().Be("id1");
        learning.Category.Should().Be("skill");
        learning.Content.Should().Be("Learned X");
        learning.Context.Should().Be("context A");
        learning.Confidence.Should().Be(0.95);
        learning.Epoch.Should().Be(42);
        learning.Timestamp.Should().Be(timestamp);
    }

    [Fact]
    public void Learning_ZeroConfidence_IsValid()
    {
        // Act
        var learning = new Learning("id", "fact", "content", "ctx", 0.0, 0, DateTimeOffset.MinValue);

        // Assert
        learning.Confidence.Should().Be(0.0);
    }

    #endregion

    #region ReificationResult

    [Fact]
    public void ReificationResult_CreatesWithAllProperties()
    {
        // Act
        var result = new ReificationResult("BranchA", 5, 4, 10, 8);

        // Assert
        result.BranchName.Should().Be("BranchA");
        result.NodesCreated.Should().Be(5);
        result.TransitionsCreated.Should().Be(4);
        result.TotalNodes.Should().Be(10);
        result.TotalTransitions.Should().Be(8);
    }

    [Fact]
    public void ReificationResult_ZeroCounts_IsValid()
    {
        // Act
        var result = new ReificationResult("Empty", 0, 0, 0, 0);

        // Assert
        result.NodesCreated.Should().Be(0);
        result.TransitionsCreated.Should().Be(0);
    }

    #endregion

    #region ScoredNode

    [Fact]
    public void ScoredNode_CreatesWithAllProperties()
    {
        // Arrange
        var node = new MonadNode(Guid.NewGuid(), "Test", "{}", DateTimeOffset.UtcNow, ImmutableArray<Guid>.Empty);

        // Act
        var scored = new ScoredNode(node, 0.85f);

        // Assert
        scored.Node.Should().Be(node);
        scored.Score.Should().Be(0.85f);
    }

    [Fact]
    public void ScoredNode_ZeroScore_IsValid()
    {
        // Arrange
        var node = new MonadNode(Guid.NewGuid(), "Test", "{}", DateTimeOffset.UtcNow, ImmutableArray<Guid>.Empty);

        // Act
        var scored = new ScoredNode(node, 0f);

        // Assert
        scored.Score.Should().Be(0f);
    }

    #endregion

    #region StepExecutionPayload

    [Fact]
    public void StepExecutionPayload_CreatesWithAllProperties()
    {
        // Arrange
        var aliases = new[] { "alias1", "alias2" };
        var executedAt = DateTime.UtcNow;

        // Act
        var payload = new StepExecutionPayload(
            "TokenA",
            aliases,
            "SourceClass",
            "Description",
            "args",
            "synopsis",
            100L,
            true,
            null,
            executedAt);

        // Assert
        payload.TokenName.Should().Be("TokenA");
        payload.Aliases.Should().Equal(aliases);
        payload.SourceClass.Should().Be("SourceClass");
        payload.Description.Should().Be("Description");
        payload.Arguments.Should().Be("args");
        payload.Synopsis.Should().Be("synopsis");
        payload.DurationMs.Should().Be(100L);
        payload.Success.Should().BeTrue();
        payload.Error.Should().BeNull();
        payload.ExecutedAt.Should().Be(executedAt);
    }

    [Fact]
    public void StepExecutionPayload_FailedStep_IsValid()
    {
        // Act
        var payload = new StepExecutionPayload(
            "TokenB",
            Array.Empty<string>(),
            "Class",
            "Desc",
            null,
            "synopsis",
            null,
            false,
            "Error occurred",
            DateTime.UtcNow);

        // Assert
        payload.Success.Should().BeFalse();
        payload.Error.Should().Be("Error occurred");
        payload.DurationMs.Should().BeNull();
    }

    #endregion

    #region QdrantDagConfig

    [Fact]
    public void QdrantDagConfig_DefaultValues_AreCorrect()
    {
        // Act
        var config = new QdrantDagConfig();

        // Assert
        config.Endpoint.Should().Be("http://localhost:6334");
        config.NodesCollection.Should().Be("ouroboros_dag_nodes");
        config.EdgesCollection.Should().Be("ouroboros_dag_edges");
        config.VectorSize.Should().Be(1536);
        config.UseHttps.Should().BeFalse();
    }

    [Fact]
    public void QdrantDagConfig_CustomValues_AreSet()
    {
        // Act
        var config = new QdrantDagConfig(
            Endpoint: "https://qdrant.example.com",
            NodesCollection: "custom_nodes",
            EdgesCollection: "custom_edges",
            VectorSize: 768,
            UseHttps: true);

        // Assert
        config.Endpoint.Should().Be("https://qdrant.example.com");
        config.NodesCollection.Should().Be("custom_nodes");
        config.EdgesCollection.Should().Be("custom_edges");
        config.VectorSize.Should().Be(768);
        config.UseHttps.Should().BeTrue();
    }

    [Fact]
    public void QdrantDagConfig_ValueEquality_Works()
    {
        // Arrange
        var a = new QdrantDagConfig();
        var b = new QdrantDagConfig();

        // Act & Assert
        a.Should().Be(b);
    }

    #endregion

    #region WalEntry

    [Fact]
    public void WalEntry_CreatesWithAllProperties()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        var entry = new WalEntry(WalEntryType.AddNode, timestamp, "{"id":"test"}");

        // Assert
        entry.Type.Should().Be(WalEntryType.AddNode);
        entry.Timestamp.Should().Be(timestamp);
        entry.PayloadJson.Should().Be("{\"id\":\"test\"}");
    }

    [Fact]
    public void WalEntry_AddEdgeType_IsValid()
    {
        // Act
        var entry = new WalEntry(WalEntryType.AddEdge, DateTimeOffset.UtcNow, "edge-data");

        // Assert
        entry.Type.Should().Be(WalEntryType.AddEdge);
    }

    #endregion

    #region FeedbackLoopConfig

    [Fact]
    public void FeedbackLoopConfig_DefaultValues_AreCorrect()
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
    public void FeedbackLoopConfig_CustomValues_AreSet()
    {
        // Act
        var config = new FeedbackLoopConfig(
            DivergenceThreshold: 0.8f,
            RotationThreshold: 0.5f,
            MaxModificationsPerCycle: 5,
            AutoPersist: false);

        // Assert
        config.DivergenceThreshold.Should().Be(0.8f);
        config.RotationThreshold.Should().Be(0.5f);
        config.MaxModificationsPerCycle.Should().Be(5);
        config.AutoPersist.Should().BeFalse();
    }

    #endregion

    #region FeedbackResult

    [Fact]
    public void FeedbackResult_CreatesWithAllProperties()
    {
        // Arrange
        var duration = TimeSpan.FromSeconds(30);

        // Act
        var result = new FeedbackResult(100, 10, 5, 3, 2, duration);

        // Assert
        result.NodesAnalyzed.Should().Be(100);
        result.NodesModified.Should().Be(10);
        result.SourceNodes.Should().Be(5);
        result.SinkNodes.Should().Be(3);
        result.CyclicNodes.Should().Be(2);
        result.Duration.Should().Be(duration);
    }

    [Fact]
    public void FeedbackResult_ZeroCounts_IsValid()
    {
        // Act
        var result = new FeedbackResult(0, 0, 0, 0, 0, TimeSpan.Zero);

        // Assert
        result.NodesAnalyzed.Should().Be(0);
        result.NodesModified.Should().Be(0);
    }

    #endregion

    #region GlobalNetworkState

    [Fact]
    public void GlobalNetworkState_Constructor_SetsAllProperties()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var nodeCountByType = ImmutableDictionary<string, int>.Empty.Add("Draft", 5);
        var transitionCountByOperation = ImmutableDictionary<string, int>.Empty.Add("UseCritique", 3);
        var rootNodeIds = ImmutableArray.Create(Guid.NewGuid());
        var leafNodeIds = ImmutableArray.Create(Guid.NewGuid());
        var metadata = ImmutableDictionary<string, string>.Empty.Add("key", "value");

        // Act
        var state = new GlobalNetworkState(
            1, timestamp, 10, 5, nodeCountByType, transitionCountByOperation,
            rootNodeIds, leafNodeIds, 0.85, 1000L, metadata);

        // Assert
        state.Epoch.Should().Be(1);
        state.Timestamp.Should().Be(timestamp);
        state.TotalNodes.Should().Be(10);
        state.TotalTransitions.Should().Be(5);
        state.NodeCountByType.Should().Equal(nodeCountByType);
        state.TransitionCountByOperation.Should().Equal(transitionCountByOperation);
        state.RootNodeIds.Should().Equal(rootNodeIds);
        state.LeafNodeIds.Should().Equal(leafNodeIds);
        state.AverageConfidence.Should().Be(0.85);
        state.TotalProcessingTimeMs.Should().Be(1000L);
        state.Metadata.Should().Equal(metadata);
    }

    [Fact]
    public void GlobalNetworkState_Constructor_NullDefaults_AreEmpty()
    {
        // Act
        var state = new GlobalNetworkState(
            0, DateTimeOffset.UtcNow, 0, 0,
            null!, null!, ImmutableArray<Guid>.Empty, ImmutableArray<Guid>.Empty);

        // Assert
        state.NodeCountByType.Should().BeEmpty();
        state.TransitionCountByOperation.Should().BeEmpty();
        state.Metadata.Should().BeEmpty();
        state.AverageConfidence.Should().BeNull();
        state.TotalProcessingTimeMs.Should().BeNull();
    }

    #endregion
}
