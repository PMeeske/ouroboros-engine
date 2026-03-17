using FluentAssertions;
using LangChain.DocumentLoaders;

namespace Ouroboros.Tests.Memory;

[Trait("Category", "Unit")]
public class EpisodeTests
{
    private static PipelineBranch CreateTestBranch(string name = "test-branch")
    {
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath(Environment.CurrentDirectory);
        return new PipelineBranch(name, store, source);
    }

    [Fact]
    public void Constructor_WithAllProperties_SetsValuesCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;
        var goal = "test goal";
        var branch = CreateTestBranch();
        var outcome = Outcome.Successful("output", TimeSpan.FromSeconds(1));
        var successScore = 0.85;
        var lessons = ImmutableList.Create("lesson1", "lesson2");
        var context = ImmutableDictionary<string, object>.Empty.Add("key", "value");
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };

        // Act
        var episode = new Episode(
            id, timestamp, goal, branch, outcome,
            successScore, lessons, context, embedding);

        // Assert
        episode.Id.Should().Be(id);
        episode.Timestamp.Should().Be(timestamp);
        episode.Goal.Should().Be(goal);
        episode.ReasoningTrace.Should().BeSameAs(branch);
        episode.Result.Should().Be(outcome);
        episode.SuccessScore.Should().Be(successScore);
        episode.LessonsLearned.Should().HaveCount(2);
        episode.Context.Should().ContainKey("key");
        episode.Embedding.Should().BeEquivalentTo(new float[] { 0.1f, 0.2f, 0.3f });
    }

    [Fact]
    public void RecordEquality_WithSameValues_AreEqual()
    {
        // Arrange
        var id = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;
        var branch = CreateTestBranch();
        var outcome = Outcome.Successful("output", TimeSpan.FromSeconds(1));
        var lessons = ImmutableList<string>.Empty;
        var context = ImmutableDictionary<string, object>.Empty;
        var embedding = new float[] { 0.1f };

        var episode1 = new Episode(id, timestamp, "goal", branch, outcome, 0.9, lessons, context, embedding);
        var episode2 = new Episode(id, timestamp, "goal", branch, outcome, 0.9, lessons, context, embedding);

        // Assert
        episode1.Should().Be(episode2);
    }

    [Fact]
    public void RecordEquality_WithDifferentGoals_AreNotEqual()
    {
        // Arrange
        var id = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;
        var branch = CreateTestBranch();
        var outcome = Outcome.Successful("output", TimeSpan.FromSeconds(1));
        var lessons = ImmutableList<string>.Empty;
        var context = ImmutableDictionary<string, object>.Empty;
        var embedding = new float[] { 0.1f };

        var episode1 = new Episode(id, timestamp, "goal1", branch, outcome, 0.9, lessons, context, embedding);
        var episode2 = new Episode(id, timestamp, "goal2", branch, outcome, 0.9, lessons, context, embedding);

        // Assert
        episode1.Should().NotBe(episode2);
    }

    [Fact]
    public void With_Expression_CreatesModifiedCopy()
    {
        // Arrange
        var branch = CreateTestBranch();
        var outcome = Outcome.Successful("output", TimeSpan.FromSeconds(1));
        var episode = new Episode(
            Guid.NewGuid(), DateTime.UtcNow, "original goal", branch, outcome,
            0.5, ImmutableList<string>.Empty, ImmutableDictionary<string, object>.Empty,
            new float[] { 0.1f });

        // Act
        var modified = episode with { Goal = "modified goal", SuccessScore = 0.9 };

        // Assert
        modified.Goal.Should().Be("modified goal");
        modified.SuccessScore.Should().Be(0.9);
        episode.Goal.Should().Be("original goal");
        episode.SuccessScore.Should().Be(0.5);
    }

    [Fact]
    public void SuccessScore_CanBeZero()
    {
        // Arrange
        var branch = CreateTestBranch();
        var outcome = Outcome.Failed("failed", TimeSpan.FromSeconds(1), new[] { "error" });

        // Act
        var episode = new Episode(
            Guid.NewGuid(), DateTime.UtcNow, "goal", branch, outcome,
            0.0, ImmutableList<string>.Empty, ImmutableDictionary<string, object>.Empty,
            Array.Empty<float>());

        // Assert
        episode.SuccessScore.Should().Be(0.0);
    }

    [Fact]
    public void SuccessScore_CanBeOne()
    {
        // Arrange
        var branch = CreateTestBranch();
        var outcome = Outcome.Successful("success", TimeSpan.FromSeconds(1));

        // Act
        var episode = new Episode(
            Guid.NewGuid(), DateTime.UtcNow, "goal", branch, outcome,
            1.0, ImmutableList<string>.Empty, ImmutableDictionary<string, object>.Empty,
            Array.Empty<float>());

        // Assert
        episode.SuccessScore.Should().Be(1.0);
    }

    [Fact]
    public void LessonsLearned_IsImmutableList()
    {
        // Arrange
        var branch = CreateTestBranch();
        var outcome = Outcome.Successful("output", TimeSpan.FromSeconds(1));
        var lessons = ImmutableList.Create("lesson1");

        // Act
        var episode = new Episode(
            Guid.NewGuid(), DateTime.UtcNow, "goal", branch, outcome,
            0.8, lessons, ImmutableDictionary<string, object>.Empty,
            Array.Empty<float>());

        // Assert
        episode.LessonsLearned.Should().BeAssignableTo<ImmutableList<string>>();
        episode.LessonsLearned.Should().HaveCount(1);
    }

    [Fact]
    public void Context_IsImmutableDictionary()
    {
        // Arrange
        var branch = CreateTestBranch();
        var outcome = Outcome.Successful("output", TimeSpan.FromSeconds(1));
        var context = ImmutableDictionary<string, object>.Empty
            .Add("env", "test")
            .Add("version", "1.0");

        // Act
        var episode = new Episode(
            Guid.NewGuid(), DateTime.UtcNow, "goal", branch, outcome,
            0.8, ImmutableList<string>.Empty, context,
            Array.Empty<float>());

        // Assert
        episode.Context.Should().BeAssignableTo<ImmutableDictionary<string, object>>();
        episode.Context.Should().HaveCount(2);
        episode.Context["env"].Should().Be("test");
    }
}
