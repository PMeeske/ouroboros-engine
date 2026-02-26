using System.Collections.Immutable;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class GlobalNetworkStateTests
{
    [Fact]
    public void Ctor_SetsAllProperties()
    {
        var now = DateTimeOffset.UtcNow;
        var nodeId = Guid.NewGuid();
        var leafId = Guid.NewGuid();

        var state = new GlobalNetworkState(
            epoch: 5,
            timestamp: now,
            totalNodes: 10,
            totalTransitions: 8,
            nodeCountByType: ImmutableDictionary<string, int>.Empty.Add("Draft", 5),
            transitionCountByOperation: ImmutableDictionary<string, int>.Empty.Add("Critique", 3),
            rootNodeIds: ImmutableArray.Create(nodeId),
            leafNodeIds: ImmutableArray.Create(leafId),
            averageConfidence: 0.85,
            totalProcessingTimeMs: 5000);

        state.Epoch.Should().Be(5);
        state.Timestamp.Should().Be(now);
        state.TotalNodes.Should().Be(10);
        state.TotalTransitions.Should().Be(8);
        state.NodeCountByType.Should().ContainKey("Draft");
        state.TransitionCountByOperation.Should().ContainKey("Critique");
        state.RootNodeIds.Should().ContainSingle();
        state.LeafNodeIds.Should().ContainSingle();
        state.AverageConfidence.Should().Be(0.85);
        state.TotalProcessingTimeMs.Should().Be(5000);
        state.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void Ctor_NullNodeCountByType_DefaultsToEmpty()
    {
        var state = new GlobalNetworkState(
            0, DateTimeOffset.UtcNow, 0, 0, null!, null!,
            ImmutableArray<Guid>.Empty, ImmutableArray<Guid>.Empty);

        state.NodeCountByType.Should().BeEmpty();
        state.TransitionCountByOperation.Should().BeEmpty();
    }

    [Fact]
    public void Ctor_WithMetadata_SetsMetadata()
    {
        var metadata = ImmutableDictionary<string, string>.Empty.Add("key", "value");
        var state = new GlobalNetworkState(
            1, DateTimeOffset.UtcNow, 0, 0,
            ImmutableDictionary<string, int>.Empty,
            ImmutableDictionary<string, int>.Empty,
            ImmutableArray<Guid>.Empty,
            ImmutableArray<Guid>.Empty,
            metadata: metadata);

        state.Metadata.Should().ContainKey("key");
    }
}
