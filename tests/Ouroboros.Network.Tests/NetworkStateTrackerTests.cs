namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class NetworkStateTrackerTests : IDisposable
{
    private readonly NetworkStateTracker _tracker;

    public NetworkStateTrackerTests()
    {
        _tracker = new NetworkStateTracker();
    }

    [Fact]
    public void Ctor_InitializesCorrectly()
    {
        _tracker.Dag.Should().NotBeNull();
        _tracker.Projector.Should().NotBeNull();
        _tracker.ReplayEngine.Should().NotBeNull();
        _tracker.TrackedBranchCount.Should().Be(0);
        _tracker.HasQdrantStore.Should().BeFalse();
        _tracker.HasMeTTaEngine.Should().BeFalse();
        _tracker.MeTTaFacts.Should().BeEmpty();
        _tracker.AutoPersist.Should().BeFalse();
        _tracker.AutoExportMeTTa.Should().BeFalse();
    }

    [Fact]
    public void AutoPersist_CanBeSet()
    {
        _tracker.AutoPersist = true;

        _tracker.AutoPersist.Should().BeTrue();
    }

    [Fact]
    public void AutoExportMeTTa_CanBeSet()
    {
        _tracker.AutoExportMeTTa = true;

        _tracker.AutoExportMeTTa.Should().BeTrue();
    }

    [Fact]
    public void ConfigureQdrantPersistence_Null_Throws()
    {
        FluentActions.Invoking(() => _tracker.ConfigureQdrantPersistence(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ConfigureMeTTaExport_Null_Throws()
    {
        FluentActions.Invoking(() => _tracker.ConfigureMeTTaExport(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TrackBranch_Null_ReturnsFailure()
    {
        var result = _tracker.TrackBranch(null!);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void UpdateBranch_Null_ReturnsFailure()
    {
        var result = _tracker.UpdateBranch(null!);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task TrackBranchAsync_Null_ReturnsFailure()
    {
        var result = await _tracker.TrackBranchAsync(null!);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateBranchAsync_Null_ReturnsFailure()
    {
        var result = await _tracker.UpdateBranchAsync(null!);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task PersistToQdrantAsync_NotConfigured_ReturnsFailure()
    {
        var result = await _tracker.PersistToQdrantAsync();

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ReplayToNode_MissingNode_ReturnsFailure()
    {
        var result = _tracker.ReplayToNode(Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void GetBranchReifier_UnknownBranch_ReturnsNone()
    {
        var result = _tracker.GetBranchReifier("nonexistent");

        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public void GetStateSummary_EmptyDag_ContainsHeaderAndZeros()
    {
        var summary = _tracker.GetStateSummary();

        summary.Should().Contain("Network State Summary");
        summary.Should().Contain("Total Nodes: 0");
        summary.Should().Contain("Total Transitions: 0");
    }

    [Fact]
    public void CreateSnapshot_ReturnsValidState()
    {
        var snapshot = _tracker.CreateSnapshot();

        snapshot.Should().NotBeNull();
        snapshot.TotalNodes.Should().Be(0);
        snapshot.TotalTransitions.Should().Be(0);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        _tracker.Reset();

        _tracker.TrackedBranchCount.Should().Be(0);
        _tracker.MeTTaFacts.Should().BeEmpty();
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        FluentActions.Invoking(() => _tracker.Dispose()).Should().NotThrow();
    }

    [Fact]
    public async Task DisposeAsync_DoesNotThrow()
    {
        await FluentActions.Invoking(async () => await _tracker.DisposeAsync()).Should().NotThrowAsync();
    }

    public void Dispose()
    {
        _tracker.Dispose();
    }
}
