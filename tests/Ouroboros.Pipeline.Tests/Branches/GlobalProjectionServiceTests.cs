using Ouroboros.Pipeline.Branches;

namespace Ouroboros.Tests.Pipeline.Branches;

[Trait("Category", "Unit")]
public sealed class GlobalProjectionServiceTests
{
    private static PipelineBranch CreateTrackingBranch()
    {
        return new PipelineBranch("tracking", new TrackedVectorStore(), DataSource.FromPath("."));
    }

    private static PipelineBranch CreateBranch(string name)
    {
        return new PipelineBranch(name, new TrackedVectorStore(), DataSource.FromPath("."));
    }

    #region CreateEpochAsync Tests

    [Fact]
    public async Task CreateEpochAsync_WithValidBranches_ReturnsSuccess()
    {
        // Arrange
        var tracking = CreateTrackingBranch();
        var branches = new[] { CreateBranch("branch-1"), CreateBranch("branch-2") };

        // Act
        var result = await GlobalProjectionService.CreateEpochAsync(tracking, branches);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Epoch.Should().NotBeNull();
        result.Value.UpdatedBranch.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateEpochAsync_AssignsEpochNumber1ToFirst()
    {
        // Arrange
        var tracking = CreateTrackingBranch();
        var branches = new[] { CreateBranch("b1") };

        // Act
        var result = await GlobalProjectionService.CreateEpochAsync(tracking, branches);

        // Assert
        result.Value.Epoch.EpochNumber.Should().Be(1);
    }

    [Fact]
    public async Task CreateEpochAsync_IncrementsEpochNumbers()
    {
        // Arrange
        var tracking = CreateTrackingBranch();
        var branches = new[] { CreateBranch("b1") };

        // Act
        var result1 = await GlobalProjectionService.CreateEpochAsync(tracking, branches);
        var result2 = await GlobalProjectionService.CreateEpochAsync(result1.Value.UpdatedBranch, branches);

        // Assert
        result1.Value.Epoch.EpochNumber.Should().Be(1);
        result2.Value.Epoch.EpochNumber.Should().Be(2);
    }

    [Fact]
    public async Task CreateEpochAsync_WithNullTrackingBranch_ThrowsArgumentNullException()
    {
        // Act
        Func<Task> act = async () => await GlobalProjectionService.CreateEpochAsync(null!, new[] { CreateBranch("b") });

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateEpochAsync_CapturesBranchSnapshots()
    {
        // Arrange
        var tracking = CreateTrackingBranch();
        var branch = CreateBranch("data-branch");
        var branchWithEvent = branch.WithReasoning(new Draft("text"), "prompt");

        // Act
        var result = await GlobalProjectionService.CreateEpochAsync(tracking, new[] { branchWithEvent });

        // Assert
        result.Value.Epoch.Branches.Should().HaveCount(1);
        result.Value.Epoch.Branches[0].Name.Should().Be("data-branch");
        result.Value.Epoch.Branches[0].Events.Should().HaveCount(1);
    }

    #endregion

    #region GetEpochs Tests

    [Fact]
    public void GetEpochs_WithNoEpochs_ReturnsEmptyList()
    {
        // Arrange
        var tracking = CreateTrackingBranch();

        // Act
        var epochs = GlobalProjectionService.GetEpochs(tracking);

        // Assert
        epochs.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEpochs_WithEpochs_ReturnsAll()
    {
        // Arrange
        var tracking = CreateTrackingBranch();
        var result1 = await GlobalProjectionService.CreateEpochAsync(tracking, new[] { CreateBranch("b1") });
        var result2 = await GlobalProjectionService.CreateEpochAsync(result1.Value.UpdatedBranch, new[] { CreateBranch("b2") });

        // Act
        var epochs = GlobalProjectionService.GetEpochs(result2.Value.UpdatedBranch);

        // Assert
        epochs.Should().HaveCount(2);
    }

    [Fact]
    public void GetEpochs_WithNullBranch_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => GlobalProjectionService.GetEpochs(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region GetEpoch Tests

    [Fact]
    public async Task GetEpoch_WithExistingNumber_ReturnsSuccess()
    {
        // Arrange
        var tracking = CreateTrackingBranch();
        var createResult = await GlobalProjectionService.CreateEpochAsync(tracking, new[] { CreateBranch("b1") });

        // Act
        var result = GlobalProjectionService.GetEpoch(createResult.Value.UpdatedBranch, 1);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EpochNumber.Should().Be(1);
    }

    [Fact]
    public void GetEpoch_WithNonExistingNumber_ReturnsFailure()
    {
        // Arrange
        var tracking = CreateTrackingBranch();

        // Act
        var result = GlobalProjectionService.GetEpoch(tracking, 999);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("999");
    }

    #endregion

    #region GetLatestEpoch Tests

    [Fact]
    public void GetLatestEpoch_WithNoEpochs_ReturnsFailure()
    {
        // Arrange
        var tracking = CreateTrackingBranch();

        // Act
        var result = GlobalProjectionService.GetLatestEpoch(tracking);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("No epochs available");
    }

    [Fact]
    public async Task GetLatestEpoch_WithMultipleEpochs_ReturnsHighestNumber()
    {
        // Arrange
        var tracking = CreateTrackingBranch();
        var r1 = await GlobalProjectionService.CreateEpochAsync(tracking, new[] { CreateBranch("b1") });
        var r2 = await GlobalProjectionService.CreateEpochAsync(r1.Value.UpdatedBranch, new[] { CreateBranch("b2") });

        // Act
        var result = GlobalProjectionService.GetLatestEpoch(r2.Value.UpdatedBranch);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EpochNumber.Should().Be(2);
    }

    #endregion

    #region GetMetrics Tests

    [Fact]
    public void GetMetrics_WithNoEpochs_ReturnsZeroMetrics()
    {
        // Arrange
        var tracking = CreateTrackingBranch();

        // Act
        var result = GlobalProjectionService.GetMetrics(tracking);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalEpochs.Should().Be(0);
        result.Value.TotalBranches.Should().Be(0);
        result.Value.TotalEvents.Should().Be(0);
    }

    [Fact]
    public async Task GetMetrics_WithEpochs_ReturnsCorrectMetrics()
    {
        // Arrange
        var tracking = CreateTrackingBranch();
        var branch = CreateBranch("measured");
        branch = branch.WithReasoning(new Draft("text"), "prompt");
        var r1 = await GlobalProjectionService.CreateEpochAsync(tracking, new[] { branch });

        // Act
        var result = GlobalProjectionService.GetMetrics(r1.Value.UpdatedBranch);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalEpochs.Should().Be(1);
        result.Value.TotalBranches.Should().BeGreaterThanOrEqualTo(1);
        result.Value.LastEpochAt.Should().NotBeNull();
    }

    [Fact]
    public void GetMetrics_WithNullBranch_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => GlobalProjectionService.GetMetrics(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region GetEpochsInRange Tests

    [Fact]
    public async Task GetEpochsInRange_ReturnsMatchingEpochs()
    {
        // Arrange
        var tracking = CreateTrackingBranch();
        var r1 = await GlobalProjectionService.CreateEpochAsync(tracking, new[] { CreateBranch("b") });

        var now = DateTime.UtcNow;
        var start = now.AddMinutes(-5);
        var end = now.AddMinutes(5);

        // Act
        var epochs = GlobalProjectionService.GetEpochsInRange(r1.Value.UpdatedBranch, start, end);

        // Assert
        epochs.Should().HaveCount(1);
    }

    [Fact]
    public void GetEpochsInRange_WithNoMatchingEpochs_ReturnsEmpty()
    {
        // Arrange
        var tracking = CreateTrackingBranch();
        var start = DateTime.UtcNow.AddDays(10);
        var end = DateTime.UtcNow.AddDays(20);

        // Act
        var epochs = GlobalProjectionService.GetEpochsInRange(tracking, start, end);

        // Assert
        epochs.Should().BeEmpty();
    }

    [Fact]
    public void GetEpochsInRange_WithNullBranch_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => GlobalProjectionService.GetEpochsInRange(null!, DateTime.UtcNow, DateTime.UtcNow);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region CreateEpochArrow Tests

    [Fact]
    public async Task CreateEpochArrow_ReturnsUpdatedBranchWithEpochEvent()
    {
        // Arrange
        var tracking = CreateTrackingBranch();
        var branches = new[] { CreateBranch("b1") };
        var arrow = GlobalProjectionService.CreateEpochArrow(branches);

        // Act
        var result = await arrow(tracking);

        // Assert
        result.Events.Should().HaveCount(1);
        result.Events[0].Should().BeOfType<EpochCreatedEvent>();
    }

    [Fact]
    public async Task CreateEpochArrow_WithMetadata_IncludesMetadataInEpoch()
    {
        // Arrange
        var tracking = CreateTrackingBranch();
        var branches = new[] { CreateBranch("b1") };
        var metadata = new Dictionary<string, object> { ["reason"] = "scheduled" };
        var arrow = GlobalProjectionService.CreateEpochArrow(branches, metadata);

        // Act
        var result = await arrow(tracking);

        // Assert
        var epochEvent = result.Events[0] as EpochCreatedEvent;
        epochEvent!.Epoch.Metadata.Should().ContainKey("reason");
    }

    #endregion
}
