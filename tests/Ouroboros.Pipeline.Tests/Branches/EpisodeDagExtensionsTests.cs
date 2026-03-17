using Ouroboros.Domain.Environment;
using Ouroboros.Pipeline.Branches;

namespace Ouroboros.Tests.Pipeline.Branches;

/// <summary>
/// Additional unit tests for EpisodeDagExtensions beyond the integration tests.
/// Focuses on edge cases and boundary conditions.
/// </summary>
[Trait("Category", "Unit")]
public sealed class EpisodeDagExtensionsTests
{
    private static PipelineBranch CreateBranch()
    {
        return new PipelineBranch("test", new TrackedVectorStore(), DataSource.FromPath("."));
    }

    private static Episode CreateEpisode(
        double reward = 10.0,
        bool success = true,
        DateTime? startTime = null,
        int stepCount = 0)
    {
        var start = startTime ?? DateTime.UtcNow.AddMinutes(-1);
        var steps = Enumerable.Range(0, stepCount)
            .Select(_ => new EnvironmentStep(
                Observation: "obs",
                Action: "act",
                Reward: reward / Math.Max(stepCount, 1),
                Done: false,
                Info: null))
            .ToList();

        return new Episode(
            Id: Guid.NewGuid(),
            EnvironmentName: "TestEnv",
            Steps: steps.AsReadOnly(),
            TotalReward: reward,
            StartTime: start,
            EndTime: start.AddMinutes(1),
            Success: success,
            Metadata: null);
    }

    #region RecordEpisode Tests

    [Fact]
    public void RecordEpisode_WithNullBranch_ThrowsArgumentNullException()
    {
        // Arrange
        PipelineBranch? branch = null;

        // Act
        Action act = () => branch!.RecordEpisode(CreateEpisode());

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RecordEpisode_WithNullEpisode_ThrowsArgumentNullException()
    {
        // Arrange
        var branch = CreateBranch();

        // Act
        Action act = () => branch.RecordEpisode(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RecordEpisode_DoesNotMutateOriginalBranch()
    {
        // Arrange
        var branch = CreateBranch();
        var episode = CreateEpisode();

        // Act
        var updated = branch.RecordEpisode(episode);

        // Assert
        branch.Events.Should().BeEmpty();
        updated.Events.Should().HaveCount(1);
    }

    #endregion

    #region RecordEpisodes Tests

    [Fact]
    public void RecordEpisodes_WithNullBranch_ThrowsArgumentNullException()
    {
        // Arrange
        PipelineBranch? branch = null;

        // Act
        Action act = () => branch!.RecordEpisodes(new[] { CreateEpisode() });

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RecordEpisodes_WithNullEpisodes_ThrowsArgumentNullException()
    {
        // Arrange
        var branch = CreateBranch();

        // Act
        Action act = () => branch.RecordEpisodes(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RecordEpisodes_WithEmptyList_ReturnsBranchUnchanged()
    {
        // Arrange
        var branch = CreateBranch();

        // Act
        var updated = branch.RecordEpisodes(Array.Empty<Episode>());

        // Assert
        updated.Events.Should().BeEmpty();
    }

    [Fact]
    public void RecordEpisodes_WithMultipleEpisodes_RecordsAll()
    {
        // Arrange
        var branch = CreateBranch();
        var episodes = Enumerable.Range(0, 5).Select(i => CreateEpisode(reward: i * 10.0)).ToArray();

        // Act
        var updated = branch.RecordEpisodes(episodes);

        // Assert
        updated.Events.Should().HaveCount(5);
    }

    #endregion

    #region GetEpisodes Tests

    [Fact]
    public void GetEpisodes_WithNullBranch_ThrowsArgumentNullException()
    {
        // Arrange
        PipelineBranch? branch = null;

        // Act
        Action act = () => branch!.GetEpisodes();

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetEpisodes_WithNoEpisodes_ReturnsEmpty()
    {
        // Arrange
        var branch = CreateBranch();

        // Act
        var episodes = branch.GetEpisodes().ToList();

        // Assert
        episodes.Should().BeEmpty();
    }

    [Fact]
    public void GetEpisodes_ReturnedInChronologicalOrder()
    {
        // Arrange
        var branch = CreateBranch();
        var t1 = DateTime.UtcNow.AddHours(-3);
        var t2 = DateTime.UtcNow.AddHours(-2);
        var t3 = DateTime.UtcNow.AddHours(-1);

        // Add in reverse order
        branch = branch.RecordEpisode(CreateEpisode(startTime: t3));
        branch = branch.RecordEpisode(CreateEpisode(startTime: t1));
        branch = branch.RecordEpisode(CreateEpisode(startTime: t2));

        // Act
        var episodes = branch.GetEpisodes().ToList();

        // Assert
        episodes.Should().HaveCount(3);
        episodes[0].StartTime.Should().BeOnOrBefore(episodes[1].StartTime);
        episodes[1].StartTime.Should().BeOnOrBefore(episodes[2].StartTime);
    }

    [Fact]
    public void GetEpisodes_IgnoresNonEpisodeEvents()
    {
        // Arrange
        var branch = CreateBranch();
        branch = branch.WithReasoning(new Draft("text"), "prompt");
        branch = branch.RecordEpisode(CreateEpisode());
        branch = branch.WithIngestEvent("source", new[] { "id1" });

        // Act
        var episodes = branch.GetEpisodes().ToList();

        // Assert
        episodes.Should().HaveCount(1);
    }

    #endregion

    #region GetEpisodeStatistics Tests

    [Fact]
    public void GetEpisodeStatistics_WithNullBranch_ThrowsArgumentNullException()
    {
        // Arrange
        PipelineBranch? branch = null;

        // Act
        Action act = () => branch!.GetEpisodeStatistics();

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetEpisodeStatistics_WithNoEpisodes_ReturnsZeroStats()
    {
        // Arrange
        var branch = CreateBranch();

        // Act
        var stats = branch.GetEpisodeStatistics();

        // Assert
        stats.TotalEpisodes.Should().Be(0);
        stats.SuccessfulEpisodes.Should().Be(0);
        stats.SuccessRate.Should().Be(0.0);
        stats.AverageReward.Should().Be(0.0);
        stats.AverageSteps.Should().Be(0.0);
        stats.TotalReward.Should().Be(0.0);
    }

    [Fact]
    public void GetEpisodeStatistics_WithAllSuccessful_HasFullSuccessRate()
    {
        // Arrange
        var branch = CreateBranch();
        branch = branch.RecordEpisodes(new[]
        {
            CreateEpisode(reward: 10, success: true),
            CreateEpisode(reward: 20, success: true)
        });

        // Act
        var stats = branch.GetEpisodeStatistics();

        // Assert
        stats.SuccessRate.Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public void GetEpisodeStatistics_WithNoSuccessful_HasZeroSuccessRate()
    {
        // Arrange
        var branch = CreateBranch();
        branch = branch.RecordEpisodes(new[]
        {
            CreateEpisode(reward: 5, success: false),
            CreateEpisode(reward: 3, success: false)
        });

        // Act
        var stats = branch.GetEpisodeStatistics();

        // Assert
        stats.SuccessRate.Should().Be(0.0);
        stats.TotalReward.Should().Be(8.0);
    }

    [Fact]
    public void GetEpisodeStatistics_CalculatesCorrectAverageReward()
    {
        // Arrange
        var branch = CreateBranch();
        branch = branch.RecordEpisodes(new[]
        {
            CreateEpisode(reward: 10),
            CreateEpisode(reward: 20),
            CreateEpisode(reward: 30)
        });

        // Act
        var stats = branch.GetEpisodeStatistics();

        // Assert
        stats.AverageReward.Should().BeApproximately(20.0, 0.001);
        stats.TotalReward.Should().Be(60.0);
    }

    #endregion

    #region GetBestEpisode Tests

    [Fact]
    public void GetBestEpisode_WithNullBranch_ThrowsArgumentNullException()
    {
        // Arrange
        PipelineBranch? branch = null;

        // Act
        Action act = () => branch!.GetBestEpisode();

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetBestEpisode_WithNoEpisodes_ReturnsNull()
    {
        // Arrange
        var branch = CreateBranch();

        // Act
        var best = branch.GetBestEpisode();

        // Assert
        best.Should().BeNull();
    }

    [Fact]
    public void GetBestEpisode_ReturnsHighestReward()
    {
        // Arrange
        var branch = CreateBranch();
        branch = branch.RecordEpisodes(new[]
        {
            CreateEpisode(reward: 5.0),
            CreateEpisode(reward: 100.0),
            CreateEpisode(reward: 50.0)
        });

        // Act
        var best = branch.GetBestEpisode();

        // Assert
        best.Should().NotBeNull();
        best!.TotalReward.Should().Be(100.0);
    }

    [Fact]
    public void GetBestEpisode_WithSingleEpisode_ReturnsThatEpisode()
    {
        // Arrange
        var branch = CreateBranch();
        branch = branch.RecordEpisode(CreateEpisode(reward: 42.0));

        // Act
        var best = branch.GetBestEpisode();

        // Assert
        best.Should().NotBeNull();
        best!.TotalReward.Should().Be(42.0);
    }

    [Fact]
    public void GetBestEpisode_WithNegativeRewards_ReturnsLeastNegative()
    {
        // Arrange
        var branch = CreateBranch();
        branch = branch.RecordEpisodes(new[]
        {
            CreateEpisode(reward: -10.0),
            CreateEpisode(reward: -1.0),
            CreateEpisode(reward: -5.0)
        });

        // Act
        var best = branch.GetBestEpisode();

        // Assert
        best.Should().NotBeNull();
        best!.TotalReward.Should().Be(-1.0);
    }

    #endregion
}
