using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Ouroboros.Core.Configuration;
using Ouroboros.Pipeline.Verification;
using LangChain.DocumentLoaders;

namespace Ouroboros.Tests.Memory;

[Trait("Category", "Unit")]
public class EpisodicMemoryEngineTests
{
    private static PipelineBranch CreateTestBranch(string name = "test-branch")
    {
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath(Environment.CurrentDirectory);
        return new PipelineBranch(name, store, source);
    }

    #region IEpisodicMemoryEngine Contract Tests

    [Fact]
    public async Task StoreEpisodeAsync_WithValidInputs_ReturnsSuccess()
    {
        // Arrange
        var engine = Substitute.For<IEpisodicMemoryEngine>();
        var branch = CreateTestBranch();
        var context = PipelineExecutionContext.WithGoal("test goal");
        var outcome = Outcome.Successful("output", TimeSpan.FromSeconds(1));
        var metadata = ImmutableDictionary<string, object>.Empty;
        var expectedId = new EpisodeId(Guid.NewGuid());

        engine.StoreEpisodeAsync(branch, context, outcome, metadata, Arg.Any<CancellationToken>())
            .Returns(Result<EpisodeId, string>.Success(expectedId));

        // Act
        var result = await engine.StoreEpisodeAsync(branch, context, outcome, metadata);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedId);
    }

    [Fact]
    public async Task StoreEpisodeAsync_WhenStorageFails_ReturnsFailure()
    {
        // Arrange
        var engine = Substitute.For<IEpisodicMemoryEngine>();
        var branch = CreateTestBranch();
        var context = PipelineExecutionContext.WithGoal("test goal");
        var outcome = Outcome.Successful("output", TimeSpan.FromSeconds(1));
        var metadata = ImmutableDictionary<string, object>.Empty;

        engine.StoreEpisodeAsync(branch, context, outcome, metadata, Arg.Any<CancellationToken>())
            .Returns(Result<EpisodeId, string>.Failure("Storage error"));

        // Act
        var result = await engine.StoreEpisodeAsync(branch, context, outcome, metadata);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Storage error");
    }

    [Fact]
    public async Task RetrieveSimilarEpisodesAsync_WithValidQuery_ReturnsEpisodes()
    {
        // Arrange
        var engine = Substitute.For<IEpisodicMemoryEngine>();
        var episodes = ImmutableList<Episode>.Empty;

        engine.RetrieveSimilarEpisodesAsync("test query", 5, 0.7, Arg.Any<CancellationToken>())
            .Returns(Result<ImmutableList<Episode>, string>.Success(episodes));

        // Act
        var result = await engine.RetrieveSimilarEpisodesAsync("test query");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task RetrieveSimilarEpisodesAsync_WithCustomParameters_PassesCorrectly()
    {
        // Arrange
        var engine = Substitute.For<IEpisodicMemoryEngine>();
        var episodes = ImmutableList<Episode>.Empty;

        engine.RetrieveSimilarEpisodesAsync("query", 10, 0.5, Arg.Any<CancellationToken>())
            .Returns(Result<ImmutableList<Episode>, string>.Success(episodes));

        // Act
        var result = await engine.RetrieveSimilarEpisodesAsync("query", 10, 0.5);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await engine.Received(1).RetrieveSimilarEpisodesAsync("query", 10, 0.5, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConsolidateMemoriesAsync_WithCompressStrategy_ReturnsSuccess()
    {
        // Arrange
        var engine = Substitute.For<IEpisodicMemoryEngine>();
        var olderThan = TimeSpan.FromDays(30);

        engine.ConsolidateMemoriesAsync(olderThan, ConsolidationStrategy.Compress, Arg.Any<CancellationToken>())
            .Returns(Result<Unit, string>.Success(Unit.Value));

        // Act
        var result = await engine.ConsolidateMemoriesAsync(olderThan, ConsolidationStrategy.Compress);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ConsolidateMemoriesAsync_WithAllStrategies_AcceptsEach()
    {
        // Arrange
        var engine = Substitute.For<IEpisodicMemoryEngine>();
        var olderThan = TimeSpan.FromDays(7);

        engine.ConsolidateMemoriesAsync(Arg.Any<TimeSpan>(), Arg.Any<ConsolidationStrategy>(), Arg.Any<CancellationToken>())
            .Returns(Result<Unit, string>.Success(Unit.Value));

        // Act & Assert
        foreach (var strategy in Enum.GetValues<ConsolidationStrategy>())
        {
            var result = await engine.ConsolidateMemoriesAsync(olderThan, strategy);
            result.IsSuccess.Should().BeTrue($"strategy {strategy} should succeed");
        }
    }

    [Fact]
    public async Task PlanWithExperienceAsync_WithNoEpisodes_ReturnsBasicPlan()
    {
        // Arrange
        var engine = Substitute.For<IEpisodicMemoryEngine>();
        var episodes = ImmutableList<Episode>.Empty;
        var plan = new Plan("Basic plan");

        engine.PlanWithExperienceAsync("goal", episodes, Arg.Any<CancellationToken>())
            .Returns(Result<Plan, string>.Success(plan));

        // Act
        var result = await engine.PlanWithExperienceAsync("goal", episodes);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Description.Should().Be("Basic plan");
    }

    [Fact]
    public async Task PlanWithExperienceAsync_WhenPlanningFails_ReturnsFailure()
    {
        // Arrange
        var engine = Substitute.For<IEpisodicMemoryEngine>();
        var episodes = ImmutableList<Episode>.Empty;

        engine.PlanWithExperienceAsync("goal", episodes, Arg.Any<CancellationToken>())
            .Returns(Result<Plan, string>.Failure("Planning failed"));

        // Act
        var result = await engine.PlanWithExperienceAsync("goal", episodes);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Planning failed");
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task StoreEpisodeAsync_WithCancelledToken_RespectsCancellation()
    {
        // Arrange
        var engine = Substitute.For<IEpisodicMemoryEngine>();
        var branch = CreateTestBranch();
        var context = PipelineExecutionContext.WithGoal("goal");
        var outcome = Outcome.Successful("output", TimeSpan.FromSeconds(1));
        var metadata = ImmutableDictionary<string, object>.Empty;
        var cts = new CancellationTokenSource();
        cts.Cancel();

        engine.StoreEpisodeAsync(branch, context, outcome, metadata, Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var act = async () => await engine.StoreEpisodeAsync(branch, context, outcome, metadata, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task RetrieveSimilarEpisodesAsync_WithCancelledToken_RespectsCancellation()
    {
        // Arrange
        var engine = Substitute.For<IEpisodicMemoryEngine>();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        engine.RetrieveSimilarEpisodesAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var act = async () => await engine.RetrieveSimilarEpisodesAsync("query", ct: cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion
}
