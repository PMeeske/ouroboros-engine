namespace Ouroboros.Network.Tests;

using System.Collections.Immutable;
using Microsoft.Extensions.Logging.Abstractions;

[Trait("Category", "Unit")]
public sealed class NetworkStateTrackerTests
{
    #region Construction

    [Fact]
    public void Constructor_InitializesProperties()
    {
        // Act
        var tracker = new NetworkStateTracker();

        // Assert
        tracker.Dag.Should().NotBeNull();
        tracker.Projector.Should().NotBeNull();
        tracker.ReplayEngine.Should().NotBeNull();
        tracker.TrackedBranchCount.Should().Be(0);
        tracker.AutoPersist.Should().BeFalse();
        tracker.AutoExportMeTTa.Should().BeFalse();
        tracker.HasQdrantStore.Should().BeFalse();
        tracker.HasMeTTaEngine.Should().BeFalse();
        tracker.MeTTaFacts.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithLogger_UsesLogger()
    {
        // Arrange
        var logger = new NullLogger<NetworkStateTracker>();

        // Act
        var tracker = new NetworkStateTracker(logger);

        // Assert
        tracker.Should().NotBeNull();
    }

    #endregion

    #region ConfigureQdrantPersistence

    [Fact]
    public void ConfigureQdrantPersistence_NullStore_ThrowsArgumentNullException()
    {
        // Arrange
        var tracker = new NetworkStateTracker();

        // Act
        Action act = () => tracker.ConfigureQdrantPersistence(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("store");
    }

    [Fact]
    public void ConfigureQdrantPersistence_SetsStoreAndAutoPersist()
    {
        // Arrange
        var tracker = new NetworkStateTracker();
        var config = new QdrantDagConfig();
        var store = new QdrantDagStore(config);

        // Act
        tracker.ConfigureQdrantPersistence(store, true);

        // Assert
        tracker.HasQdrantStore.Should().BeTrue();
        tracker.AutoPersist.Should().BeTrue();
    }

    #endregion

    #region ConfigureMeTTaExport

    [Fact]
    public void ConfigureMeTTaExport_NullEngine_ThrowsArgumentNullException()
    {
        // Arrange
        var tracker = new NetworkStateTracker();

        // Act
        Action act = () => tracker.ConfigureMeTTaExport(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("engine");
    }

    [Fact]
    public void ConfigureMeTTaExport_SetsEngineAndAutoExport()
    {
        // Arrange
        var tracker = new NetworkStateTracker();
        var engine = new Mock<IMeTTaEngine>().Object;

        // Act
        tracker.ConfigureMeTTaExport(engine, true);

        // Assert
        tracker.HasMeTTaEngine.Should().BeTrue();
        tracker.AutoExportMeTTa.Should().BeTrue();
    }

    #endregion

    #region TrackBranch / UpdateBranch

    [Fact]
    public void TrackBranch_NullBranch_ReturnsFailure()
    {
        // Arrange
        var tracker = new NetworkStateTracker();

        // Act
        var result = tracker.TrackBranch(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void TrackBranch_EmptyBranch_ReturnsSuccess()
    {
        // Arrange
        var tracker = new NetworkStateTracker();
        var branch = new PipelineBranch("Test", new List<PipelineEvent>(), DateTime.UtcNow);

        // Act
        var result = tracker.TrackBranch(branch);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void UpdateBranch_NullBranch_ReturnsFailure()
    {
        // Arrange
        var tracker = new NetworkStateTracker();

        // Act
        var result = tracker.UpdateBranch(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void UpdateBranch_EmptyBranch_ReturnsSuccess()
    {
        // Arrange
        var tracker = new NetworkStateTracker();
        var branch = new PipelineBranch("Test", new List<PipelineEvent>(), DateTime.UtcNow);

        // Act
        var result = tracker.UpdateBranch(branch);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region CreateSnapshot

    [Fact]
    public void CreateSnapshot_ReturnsState()
    {
        // Arrange
        var tracker = new NetworkStateTracker();

        // Act
        var state = tracker.CreateSnapshot();

        // Assert
        state.Should().NotBeNull();
        state.Metadata.Should().ContainKey("trackedBranches");
    }

    #endregion

    #region GetStateSummary

    [Fact]
    public void GetStateSummary_FormatsCorrectly()
    {
        // Arrange
        var tracker = new NetworkStateTracker();

        // Act
        var summary = tracker.GetStateSummary();

        // Assert
        summary.Should().Contain("Network State Summary");
        summary.Should().Contain("Tracked Branches: 0");
    }

    #endregion

    #region GetBranchReifier

    [Fact]
    public void GetBranchReifier_ExistingBranch_ReturnsSome()
    {
        // Arrange
        var tracker = new NetworkStateTracker();
        var branch = new PipelineBranch("Tracked", new List<PipelineEvent>(), DateTime.UtcNow);
        tracker.TrackBranch(branch);

        // Act
        var result = tracker.GetBranchReifier("Tracked");

        // Assert
        result.HasValue.Should().BeTrue();
    }

    [Fact]
    public void GetBranchReifier_MissingBranch_ReturnsNone()
    {
        // Arrange
        var tracker = new NetworkStateTracker();

        // Act
        var result = tracker.GetBranchReifier("Missing");

        // Assert
        result.HasValue.Should().BeFalse();
    }

    #endregion

    #region PersistToQdrantAsync

    [Fact]
    public async Task PersistToQdrantAsync_WithoutStore_ReturnsFailure()
    {
        // Arrange
        var tracker = new NetworkStateTracker();

        // Act
        var result = await tracker.PersistToQdrantAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not configured");
    }

    #endregion

    #region ExportToMeTTaAsync

    [Fact]
    public async Task ExportToMeTTaAsync_WithoutEngine_ReturnsFailure()
    {
        // Arrange
        var tracker = new NetworkStateTracker();
        var branch = new PipelineBranch("Test", new List<PipelineEvent>(), DateTime.UtcNow);

        // Act
        var result = await tracker.ExportToMeTTaAsync(branch);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not configured");
    }

    #endregion

    #region ExportAllToMeTTaAsync

    [Fact]
    public async Task ExportAllToMeTTaAsync_WithoutEngine_ReturnsFailure()
    {
        // Arrange
        var tracker = new NetworkStateTracker();

        // Act
        var result = await tracker.ExportAllToMeTTaAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not configured");
    }

    #endregion

    #region VerifyConstraintAsync

    [Fact]
    public async Task VerifyConstraintAsync_WithoutEngine_ReturnsFailure()
    {
        // Arrange
        var tracker = new NetworkStateTracker();

        // Act
        var result = await tracker.VerifyConstraintAsync("branch", "acyclic");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not configured");
    }

    #endregion

    #region ReplayToNode

    [Fact]
    public void ReplayToNode_MissingNode_ReturnsFailure()
    {
        // Arrange
        var tracker = new NetworkStateTracker();

        // Act
        var result = tracker.ReplayToNode(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region Reset

    [Fact]
    public void Reset_ClearsBranches()
    {
        // Arrange
        var tracker = new NetworkStateTracker();
        var branch = new PipelineBranch("Test", new List<PipelineEvent>(), DateTime.UtcNow);
        tracker.TrackBranch(branch);

        // Act
        tracker.Reset();

        // Assert
        tracker.TrackedBranchCount.Should().Be(0);
    }

    #endregion

    #region Dispose / DisposeAsync

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        // Arrange
        var tracker = new NetworkStateTracker();

        // Act
        Action act = () => tracker.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task DisposeAsync_DoesNotThrow()
    {
        // Arrange
        var tracker = new NetworkStateTracker();

        // Act
        Func<Task> act = async () => await tracker.DisposeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void Dispose_MultipleCalls_DoesNotThrow()
    {
        // Arrange
        var tracker = new NetworkStateTracker();
        tracker.Dispose();

        // Act
        Action act = () => tracker.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    #endregion
}
