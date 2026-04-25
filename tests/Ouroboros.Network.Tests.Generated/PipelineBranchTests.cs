namespace Ouroboros.Network.Tests;

using System.Collections.Immutable;

[Trait("Category", "Unit")]
public sealed class PipelineBranchReifierTests
{
    #region Construction

    [Fact]
    public void Constructor_NullDag_ThrowsArgumentNullException()
    {
        // Arrange
        var projector = new NetworkStateProjector(new MerkleDag());

        // Act
        Action act = () => new PipelineBranchReifier(null!, projector);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("dag");
    }

    [Fact]
    public void Constructor_NullProjector_ThrowsArgumentNullException()
    {
        // Arrange
        var dag = new MerkleDag();

        // Act
        Action act = () => new PipelineBranchReifier(dag, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("projector");
    }

    [Fact]
    public void DefaultConstructor_InitializesWithSharedDag()
    {
        // Act
        var reifier = new PipelineBranchReifier();

        // Assert
        reifier.Dag.Should().NotBeNull();
        reifier.Projector.Should().NotBeNull();
        reifier.EventToNodeMapping.Should().BeEmpty();
    }

    #endregion

    #region ReifyBranch

    [Fact]
    public void ReifyBranch_NullBranch_ReturnsFailure()
    {
        // Arrange
        var reifier = new PipelineBranchReifier();

        // Act
        var result = reifier.ReifyBranch(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ReifyBranch_EmptyBranch_ReturnsSuccessWithZeroCounts()
    {
        // Arrange
        var reifier = new PipelineBranchReifier();
        var branch = new PipelineBranch("Empty", new List<PipelineEvent>(), DateTime.UtcNow);

        // Act
        var result = reifier.ReifyBranch(branch);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.NodesCreated.Should().Be(0);
        result.Value.TransitionsCreated.Should().Be(0);
    }

    [Fact]
    public void ReifyEvent_NullEvent_ReturnsFailure()
    {
        // Arrange
        var reifier = new PipelineBranchReifier();

        // Act
        var result = reifier.ReifyEvent(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void CreateSnapshot_ReturnsGlobalNetworkState()
    {
        // Arrange
        var reifier = new PipelineBranchReifier();

        // Act
        var snapshot = reifier.CreateSnapshot("TestBranch");

        // Assert
        snapshot.Should().NotBeNull();
        snapshot.Metadata.Should().ContainKey("branch").WhoseValue.Should().Be("TestBranch");
    }

    [Fact]
    public void CreateSnapshot_NullBranchName_ReturnsStateWithoutBranchMetadata()
    {
        // Arrange
        var reifier = new PipelineBranchReifier();

        // Act
        var snapshot = reifier.CreateSnapshot(null);

        // Assert
        snapshot.Should().NotBeNull();
    }

    #endregion
}

[Trait("Category", "Unit")]
public sealed class PipelineBranchExtensionsTests
{
    #region ToMerkleDag

    [Fact]
    public void ToMerkleDag_NullBranch_ReturnsFailure()
    {
        // Act
        var result = PipelineBranchExtensions.ToMerkleDag(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ToMerkleDag_EmptyBranch_ReturnsSuccess()
    {
        // Arrange
        var branch = new PipelineBranch("Test", new List<PipelineEvent>(), DateTime.UtcNow);

        // Act
        var result = branch.ToMerkleDag();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.NodeCount.Should().Be(0);
    }

    #endregion

    #region CreateReifier

    [Fact]
    public void CreateReifier_ReturnsReifier()
    {
        // Arrange
        var branch = new PipelineBranch("Test", new List<PipelineEvent>(), DateTime.UtcNow);

        // Act
        var reifier = branch.CreateReifier();

        // Assert
        reifier.Should().NotBeNull();
        reifier.Dag.Should().NotBeNull();
    }

    #endregion

    #region GetLatestReasoningNode

    [Fact]
    public void GetLatestReasoningNode_NoReasoningSteps_ReturnsNone()
    {
        // Arrange
        var branch = new PipelineBranch("Test", new List<PipelineEvent>(), DateTime.UtcNow);

        // Act
        var result = branch.GetLatestReasoningNode();

        // Assert
        result.HasValue.Should().BeFalse();
    }

    #endregion

    #region GetReasoningSummary

    [Fact]
    public void GetReasoningSummary_NoReasoningSteps_ReturnsEmptySummary()
    {
        // Arrange
        var branch = new PipelineBranch("Test", new List<PipelineEvent>(), DateTime.UtcNow);

        // Act
        var summary = branch.GetReasoningSummary();

        // Assert
        summary.BranchName.Should().Be("Test");
        summary.TotalSteps.Should().Be(0);
        summary.TotalToolCalls.Should().Be(0);
        summary.TotalDuration.Should().Be(TimeSpan.Zero);
    }

    #endregion

    #region ProjectNetworkState

    [Fact]
    public void ProjectNetworkState_EmptyBranch_ReturnsSuccess()
    {
        // Arrange
        var branch = new PipelineBranch("Test", new List<PipelineEvent>(), DateTime.UtcNow);

        // Act
        var result = branch.ProjectNetworkState();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalNodes.Should().Be(0);
        result.Value.Metadata.Should().ContainKey("branch").WhoseValue.Should().Be("Test");
    }

    [Fact]
    public void ProjectNetworkState_WithMetadata_MergesMetadata()
    {
        // Arrange
        var branch = new PipelineBranch("Test", new List<PipelineEvent>(), DateTime.UtcNow);
        var metadata = ImmutableDictionary<string, string>.Empty.Add("custom", "value");

        // Act
        var result = branch.ProjectNetworkState(metadata);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Metadata.Should().ContainKey("custom").WhoseValue.Should().Be("value");
        result.Value.Metadata.Should().ContainKey("branch").WhoseValue.Should().Be("Test");
    }

    #endregion
}
