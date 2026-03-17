using Ouroboros.Pipeline.Branches;

namespace Ouroboros.Tests.Pipeline.Branches;

[Trait("Category", "Unit")]
public sealed class GlobalProjectionArrowsTests
{
    private static PipelineBranch CreateBranch(string name = "test")
    {
        return new PipelineBranch(name, new TrackedVectorStore(), DataSource.FromPath("."));
    }

    #region CreateEpochArrow Tests

    [Fact]
    public async Task CreateEpochArrow_IncludesInputBranchInSnapshot()
    {
        // Arrange
        var branch = CreateBranch("main-branch");
        var arrow = GlobalProjectionArrows.CreateEpochArrow();

        // Act
        var result = await arrow(branch);

        // Assert
        var epochEvent = result.Events[0] as EpochCreatedEvent;
        epochEvent!.Epoch.Branches.Should().Contain(s => s.Name == "main-branch");
    }

    [Fact]
    public async Task CreateEpochArrow_WithRelatedBranches_IncludesAll()
    {
        // Arrange
        var branch = CreateBranch("primary");
        var related = new[] { CreateBranch("related-1"), CreateBranch("related-2") };
        var arrow = GlobalProjectionArrows.CreateEpochArrow(related);

        // Act
        var result = await arrow(branch);

        // Assert
        var epochEvent = result.Events[0] as EpochCreatedEvent;
        epochEvent!.Epoch.Branches.Should().HaveCount(3); // primary + 2 related
    }

    [Fact]
    public async Task CreateEpochArrow_WithNullRelatedBranches_IncludesOnlyInputBranch()
    {
        // Arrange
        var branch = CreateBranch("solo");
        var arrow = GlobalProjectionArrows.CreateEpochArrow(null);

        // Act
        var result = await arrow(branch);

        // Assert
        var epochEvent = result.Events[0] as EpochCreatedEvent;
        epochEvent!.Epoch.Branches.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateEpochArrow_AssignsIncrementingEpochNumbers()
    {
        // Arrange
        var branch = CreateBranch("counter");
        var arrow1 = GlobalProjectionArrows.CreateEpochArrow();

        // Act
        var after1 = await arrow1(branch);
        var arrow2 = GlobalProjectionArrows.CreateEpochArrow();
        var after2 = await arrow2(after1);

        // Assert
        var epochs = GlobalProjectionArrows.GetEpochs(after2);
        epochs.Should().HaveCount(2);
        epochs[0].EpochNumber.Should().BeLessThan(epochs[1].EpochNumber);
    }

    #endregion

    #region SafeCreateEpochArrow Tests

    [Fact]
    public async Task SafeCreateEpochArrow_WithValidInputs_ReturnsSuccess()
    {
        // Arrange
        var branch = CreateBranch("safe-test");
        var arrow = GlobalProjectionArrows.SafeCreateEpochArrow();

        // Act
        var result = await arrow(branch);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region GetEpochs Tests

    [Fact]
    public void GetEpochs_WithNoEpochs_ReturnsEmptyList()
    {
        // Arrange
        var branch = CreateBranch();

        // Act
        var epochs = GlobalProjectionArrows.GetEpochs(branch);

        // Assert
        epochs.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEpochs_ReturnsAllEpochs()
    {
        // Arrange
        var branch = CreateBranch();
        var arrow = GlobalProjectionArrows.CreateEpochArrow();
        var after1 = await arrow(branch);
        var after2 = await arrow(after1);

        // Act
        var epochs = GlobalProjectionArrows.GetEpochs(after2);

        // Assert
        epochs.Should().HaveCount(2);
    }

    #endregion

    #region GetEpoch Tests

    [Fact]
    public async Task GetEpoch_WithExistingNumber_ReturnsSuccess()
    {
        // Arrange
        var branch = CreateBranch();
        var arrow = GlobalProjectionArrows.CreateEpochArrow();
        var updated = await arrow(branch);

        // Act
        var result = GlobalProjectionArrows.GetEpoch(updated, 1);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EpochNumber.Should().Be(1);
    }

    [Fact]
    public void GetEpoch_WithNonExistingNumber_ReturnsFailure()
    {
        // Arrange
        var branch = CreateBranch();

        // Act
        var result = GlobalProjectionArrows.GetEpoch(branch, 42);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    #endregion

    #region GetLatestEpoch Tests

    [Fact]
    public void GetLatestEpoch_WithNoEpochs_ReturnsFailure()
    {
        // Arrange
        var branch = CreateBranch();

        // Act
        var result = GlobalProjectionArrows.GetLatestEpoch(branch);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task GetLatestEpoch_ReturnsHighestEpochNumber()
    {
        // Arrange
        var branch = CreateBranch();
        var arrow = GlobalProjectionArrows.CreateEpochArrow();
        var after1 = await arrow(branch);
        var after2 = await arrow(after1);

        // Act
        var result = GlobalProjectionArrows.GetLatestEpoch(after2);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EpochNumber.Should().Be(2);
    }

    #endregion

    #region GetMetrics Tests

    [Fact]
    public void GetMetrics_DelegatesToGlobalProjectionService()
    {
        // Arrange
        var branch = CreateBranch();

        // Act
        var result = GlobalProjectionArrows.GetMetrics(branch);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalEpochs.Should().Be(0);
    }

    #endregion

    #region GetEpochsInRange Tests

    [Fact]
    public async Task GetEpochsInRange_ReturnsMatchingEpochs()
    {
        // Arrange
        var branch = CreateBranch();
        var arrow = GlobalProjectionArrows.CreateEpochArrow();
        var updated = await arrow(branch);

        var start = DateTime.UtcNow.AddMinutes(-5);
        var end = DateTime.UtcNow.AddMinutes(5);

        // Act
        var epochs = GlobalProjectionArrows.GetEpochsInRange(updated, start, end);

        // Assert
        epochs.Should().HaveCount(1);
    }

    [Fact]
    public void GetEpochsInRange_WithNoMatchingRange_ReturnsEmpty()
    {
        // Arrange
        var branch = CreateBranch();

        // Act
        var epochs = GlobalProjectionArrows.GetEpochsInRange(
            branch,
            DateTime.UtcNow.AddDays(100),
            DateTime.UtcNow.AddDays(200));

        // Assert
        epochs.Should().BeEmpty();
    }

    #endregion

    #region Immutability Tests

    [Fact]
    public async Task CreateEpochArrow_DoesNotMutateOriginalBranch()
    {
        // Arrange
        var branch = CreateBranch();
        var originalEventCount = branch.Events.Count;
        var arrow = GlobalProjectionArrows.CreateEpochArrow();

        // Act
        var updated = await arrow(branch);

        // Assert
        branch.Events.Count.Should().Be(originalEventCount);
        updated.Events.Count.Should().Be(originalEventCount + 1);
    }

    #endregion
}
