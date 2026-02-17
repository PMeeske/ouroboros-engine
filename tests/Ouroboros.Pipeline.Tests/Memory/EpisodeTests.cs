namespace Ouroboros.Tests.Pipeline.Memory;

/// <summary>
/// Unit tests for Episode record type.
/// </summary>
[Trait("Category", "Unit")]
public class EpisodeTests
{
    [Fact]
    public void Episode_ShouldBeImmutable()
    {
        // Arrange
        var id = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;
        var goal = "Test goal";
        var branch = CreateTestBranch();
        var outcome = Outcome.Successful("success", TimeSpan.FromSeconds(1));
        var successScore = 0.9;
        var lessons = ImmutableList<string>.Empty.Add("Lesson 1");
        var context = ImmutableDictionary<string, object>.Empty;
        var embedding = new float[768];

        // Act
        var episode = new Episode(id, timestamp, goal, branch, outcome, successScore, lessons, context, embedding);

        // Assert
        episode.Id.Should().Be(id);
        episode.Timestamp.Should().Be(timestamp);
        episode.Goal.Should().Be(goal);
        episode.SuccessScore.Should().Be(successScore);
        episode.LessonsLearned.Should().HaveCount(1);
    }

    [Fact]
    public void Episode_WithModification_ShouldCreateNewInstance()
    {
        // Arrange
        var episode1 = CreateTestEpisode();

        // Act
        var episode2 = episode1 with { SuccessScore = 0.5 };

        // Assert
        episode1.SuccessScore.Should().Be(0.9);
        episode2.SuccessScore.Should().Be(0.5);
        episode1.Id.Should().Be(episode2.Id); // Same ID
    }

    private static Episode CreateTestEpisode()
    {
        return new Episode(
            Guid.NewGuid(),
            DateTime.UtcNow,
            "Test goal",
            CreateTestBranch(),
            Outcome.Successful("success", TimeSpan.FromSeconds(1)),
            0.9,
            ImmutableList<string>.Empty,
            ImmutableDictionary<string, object>.Empty,
            new float[768]);
    }

    private static PipelineBranch CreateTestBranch()
    {
        var store = new TrackedVectorStore();
        var dataSource = LangChain.DocumentLoaders.DataSource.FromPath(Environment.CurrentDirectory);
        return new PipelineBranch("test", store, dataSource);
    }
}