using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using LangChain.DocumentLoaders;

namespace Ouroboros.Tests.Memory;

[Trait("Category", "Unit")]
public class EpisodicMemoryExtensionsTests
{
    private static PipelineBranch CreateTestBranch(string name = "test-branch")
    {
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath(Environment.CurrentDirectory);
        return new PipelineBranch(name, store, source);
    }

    #region WithEpisodicMemory Tests

    [Fact]
    public void WithEpisodicMemory_NullStep_ThrowsArgumentNullException()
    {
        // Arrange
        var memory = Substitute.For<IEpisodicMemoryEngine>();
        Step<PipelineBranch, PipelineBranch> step = null!;

        // Act
        var act = () => step.WithEpisodicMemory(memory, b => b.Name);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("step");
    }

    [Fact]
    public void WithEpisodicMemory_NullMemory_ThrowsArgumentNullException()
    {
        // Arrange
        Step<PipelineBranch, PipelineBranch> step = branch => Task.FromResult(branch);

        // Act
        var act = () => step.WithEpisodicMemory(null!, b => b.Name);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("memory");
    }

    [Fact]
    public void WithEpisodicMemory_NullExtractGoal_ThrowsArgumentNullException()
    {
        // Arrange
        var memory = Substitute.For<IEpisodicMemoryEngine>();
        Step<PipelineBranch, PipelineBranch> step = branch => Task.FromResult(branch);

        // Act
        var act = () => step.WithEpisodicMemory(memory, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("extractGoal");
    }

    [Fact]
    public void WithEpisodicMemory_WithValidParameters_ReturnsNewStep()
    {
        // Arrange
        var memory = Substitute.For<IEpisodicMemoryEngine>();
        Step<PipelineBranch, PipelineBranch> step = branch => Task.FromResult(branch);

        // Act
        var wrappedStep = step.WithEpisodicMemory(memory, b => b.Name);

        // Assert
        wrappedStep.Should().NotBeNull();
    }

    [Fact]
    public async Task WithEpisodicMemory_ExecutesOriginalStep()
    {
        // Arrange
        var memory = Substitute.For<IEpisodicMemoryEngine>();
        var branch = CreateTestBranch("original");
        var resultBranch = CreateTestBranch("result");
        var stepExecuted = false;

        memory.RetrieveSimilarEpisodesAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(Result<ImmutableList<Episode>, string>.Success(ImmutableList<Episode>.Empty));

        memory.StoreEpisodeAsync(Arg.Any<PipelineBranch>(), Arg.Any<PipelineExecutionContext>(),
                Arg.Any<Outcome>(), Arg.Any<ImmutableDictionary<string, object>>(), Arg.Any<CancellationToken>())
            .Returns(Result<EpisodeId, string>.Success(new EpisodeId(Guid.NewGuid())));

        Step<PipelineBranch, PipelineBranch> step = b =>
        {
            stepExecuted = true;
            return Task.FromResult(resultBranch);
        };

        var wrappedStep = step.WithEpisodicMemory(memory, b => b.Name);

        // Act
        var result = await wrappedStep(branch);

        // Assert
        stepExecuted.Should().BeTrue();
        result.Should().BeSameAs(resultBranch);
    }

    [Fact]
    public async Task WithEpisodicMemory_RetrievesEpisodesBeforeExecution()
    {
        // Arrange
        var memory = Substitute.For<IEpisodicMemoryEngine>();
        var branch = CreateTestBranch("test");

        memory.RetrieveSimilarEpisodesAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(Result<ImmutableList<Episode>, string>.Success(ImmutableList<Episode>.Empty));

        memory.StoreEpisodeAsync(Arg.Any<PipelineBranch>(), Arg.Any<PipelineExecutionContext>(),
                Arg.Any<Outcome>(), Arg.Any<ImmutableDictionary<string, object>>(), Arg.Any<CancellationToken>())
            .Returns(Result<EpisodeId, string>.Success(new EpisodeId(Guid.NewGuid())));

        Step<PipelineBranch, PipelineBranch> step = b => Task.FromResult(b);
        var wrappedStep = step.WithEpisodicMemory(memory, b => b.Name);

        // Act
        await wrappedStep(branch);

        // Assert
        await memory.Received(1).RetrieveSimilarEpisodesAsync("test", 5, Arg.Any<double>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WithEpisodicMemory_StoresEpisodeAfterExecution()
    {
        // Arrange
        var memory = Substitute.For<IEpisodicMemoryEngine>();
        var branch = CreateTestBranch("test");

        memory.RetrieveSimilarEpisodesAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(Result<ImmutableList<Episode>, string>.Success(ImmutableList<Episode>.Empty));

        memory.StoreEpisodeAsync(Arg.Any<PipelineBranch>(), Arg.Any<PipelineExecutionContext>(),
                Arg.Any<Outcome>(), Arg.Any<ImmutableDictionary<string, object>>(), Arg.Any<CancellationToken>())
            .Returns(Result<EpisodeId, string>.Success(new EpisodeId(Guid.NewGuid())));

        Step<PipelineBranch, PipelineBranch> step = b => Task.FromResult(b);
        var wrappedStep = step.WithEpisodicMemory(memory, b => b.Name);

        // Act
        await wrappedStep(branch);

        // Assert
        await memory.Received(1).StoreEpisodeAsync(
            Arg.Any<PipelineBranch>(),
            Arg.Is<PipelineExecutionContext>(c => c.Goal == "test"),
            Arg.Any<Outcome>(),
            Arg.Any<ImmutableDictionary<string, object>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WithEpisodicMemory_WhenRetrievalFails_StillExecutesStep()
    {
        // Arrange
        var memory = Substitute.For<IEpisodicMemoryEngine>();
        var branch = CreateTestBranch("test");
        var stepExecuted = false;

        memory.RetrieveSimilarEpisodesAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(Result<ImmutableList<Episode>, string>.Failure("retrieval error"));

        memory.StoreEpisodeAsync(Arg.Any<PipelineBranch>(), Arg.Any<PipelineExecutionContext>(),
                Arg.Any<Outcome>(), Arg.Any<ImmutableDictionary<string, object>>(), Arg.Any<CancellationToken>())
            .Returns(Result<EpisodeId, string>.Success(new EpisodeId(Guid.NewGuid())));

        Step<PipelineBranch, PipelineBranch> step = b =>
        {
            stepExecuted = true;
            return Task.FromResult(b);
        };
        var wrappedStep = step.WithEpisodicMemory(memory, b => b.Name);

        // Act
        await wrappedStep(branch);

        // Assert
        stepExecuted.Should().BeTrue();
    }

    [Fact]
    public async Task WithEpisodicMemory_WhenStorageFails_StillReturnsResult()
    {
        // Arrange
        var memory = Substitute.For<IEpisodicMemoryEngine>();
        var branch = CreateTestBranch("test");

        memory.RetrieveSimilarEpisodesAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(Result<ImmutableList<Episode>, string>.Success(ImmutableList<Episode>.Empty));

        memory.StoreEpisodeAsync(Arg.Any<PipelineBranch>(), Arg.Any<PipelineExecutionContext>(),
                Arg.Any<Outcome>(), Arg.Any<ImmutableDictionary<string, object>>(), Arg.Any<CancellationToken>())
            .Returns(Result<EpisodeId, string>.Failure("storage failed"));

        Step<PipelineBranch, PipelineBranch> step = b => Task.FromResult(b);
        var wrappedStep = step.WithEpisodicMemory(memory, b => b.Name);

        // Act
        var result = await wrappedStep(branch);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task WithEpisodicMemory_WithCustomTopK_PassesCorrectValue()
    {
        // Arrange
        var memory = Substitute.For<IEpisodicMemoryEngine>();
        var branch = CreateTestBranch("test");

        memory.RetrieveSimilarEpisodesAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(Result<ImmutableList<Episode>, string>.Success(ImmutableList<Episode>.Empty));

        memory.StoreEpisodeAsync(Arg.Any<PipelineBranch>(), Arg.Any<PipelineExecutionContext>(),
                Arg.Any<Outcome>(), Arg.Any<ImmutableDictionary<string, object>>(), Arg.Any<CancellationToken>())
            .Returns(Result<EpisodeId, string>.Success(new EpisodeId(Guid.NewGuid())));

        Step<PipelineBranch, PipelineBranch> step = b => Task.FromResult(b);
        var wrappedStep = step.WithEpisodicMemory(memory, b => b.Name, topK: 10);

        // Act
        await wrappedStep(branch);

        // Assert
        await memory.Received(1).RetrieveSimilarEpisodesAsync("test", 10, Arg.Any<double>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region RetrieveEpisodesStep Tests

    [Fact]
    public void RetrieveEpisodesStep_NullMemory_ThrowsArgumentNullException()
    {
        // Act
        var act = () => EpisodicMemoryExtensions.RetrieveEpisodesStep(null!, "query");

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("memory");
    }

    [Fact]
    public void RetrieveEpisodesStep_NullQuery_ThrowsArgumentException()
    {
        // Arrange
        var memory = Substitute.For<IEpisodicMemoryEngine>();

        // Act
        var act = () => EpisodicMemoryExtensions.RetrieveEpisodesStep(memory, null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RetrieveEpisodesStep_EmptyQuery_ThrowsArgumentException()
    {
        // Arrange
        var memory = Substitute.For<IEpisodicMemoryEngine>();

        // Act
        var act = () => EpisodicMemoryExtensions.RetrieveEpisodesStep(memory, "");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RetrieveEpisodesStep_WhitespaceQuery_ThrowsArgumentException()
    {
        // Arrange
        var memory = Substitute.For<IEpisodicMemoryEngine>();

        // Act
        var act = () => EpisodicMemoryExtensions.RetrieveEpisodesStep(memory, "   ");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RetrieveEpisodesStep_WithValidInputs_ReturnsStep()
    {
        // Arrange
        var memory = Substitute.For<IEpisodicMemoryEngine>();

        // Act
        var step = EpisodicMemoryExtensions.RetrieveEpisodesStep(memory, "valid query");

        // Assert
        step.Should().NotBeNull();
    }

    [Fact]
    public async Task RetrieveEpisodesStep_WhenExecuted_RetrievesEpisodes()
    {
        // Arrange
        var memory = Substitute.For<IEpisodicMemoryEngine>();
        var branch = CreateTestBranch();

        memory.RetrieveSimilarEpisodesAsync("test query", 5, 0.7, Arg.Any<CancellationToken>())
            .Returns(Result<ImmutableList<Episode>, string>.Success(ImmutableList<Episode>.Empty));

        var step = EpisodicMemoryExtensions.RetrieveEpisodesStep(memory, "test query");

        // Act
        var result = await step(branch);

        // Assert
        result.Should().BeSameAs(branch);
        await memory.Received(1).RetrieveSimilarEpisodesAsync("test query", 5, 0.7, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RetrieveEpisodesStep_WithCustomTopKAndSimilarity_PassesCorrectParameters()
    {
        // Arrange
        var memory = Substitute.For<IEpisodicMemoryEngine>();
        var branch = CreateTestBranch();

        memory.RetrieveSimilarEpisodesAsync("query", 10, 0.5, Arg.Any<CancellationToken>())
            .Returns(Result<ImmutableList<Episode>, string>.Success(ImmutableList<Episode>.Empty));

        var step = EpisodicMemoryExtensions.RetrieveEpisodesStep(memory, "query", topK: 10, minSimilarity: 0.5);

        // Act
        await step(branch);

        // Assert
        await memory.Received(1).RetrieveSimilarEpisodesAsync("query", 10, 0.5, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RetrieveEpisodesStep_WhenRetrievalFails_ReturnsBranchUnchanged()
    {
        // Arrange
        var memory = Substitute.For<IEpisodicMemoryEngine>();
        var branch = CreateTestBranch();

        memory.RetrieveSimilarEpisodesAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(Result<ImmutableList<Episode>, string>.Failure("error"));

        var step = EpisodicMemoryExtensions.RetrieveEpisodesStep(memory, "query");

        // Act
        var result = await step(branch);

        // Assert
        result.Should().BeSameAs(branch);
    }

    #endregion

    #region ConsolidateMemoriesStep Tests

    [Fact]
    public void ConsolidateMemoriesStep_NullMemory_ThrowsArgumentNullException()
    {
        // Act
        var act = () => EpisodicMemoryExtensions.ConsolidateMemoriesStep(
            null!, TimeSpan.FromDays(30), ConsolidationStrategy.Compress);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("memory");
    }

    [Fact]
    public void ConsolidateMemoriesStep_WithValidInputs_ReturnsStep()
    {
        // Arrange
        var memory = Substitute.For<IEpisodicMemoryEngine>();

        // Act
        var step = EpisodicMemoryExtensions.ConsolidateMemoriesStep(
            memory, TimeSpan.FromDays(30), ConsolidationStrategy.Compress);

        // Assert
        step.Should().NotBeNull();
    }

    [Fact]
    public async Task ConsolidateMemoriesStep_WhenExecuted_ConsolidatesMemories()
    {
        // Arrange
        var memory = Substitute.For<IEpisodicMemoryEngine>();
        var branch = CreateTestBranch();
        var olderThan = TimeSpan.FromDays(30);

        memory.ConsolidateMemoriesAsync(olderThan, ConsolidationStrategy.Prune, Arg.Any<CancellationToken>())
            .Returns(Result<Unit, string>.Success(Unit.Value));

        var step = EpisodicMemoryExtensions.ConsolidateMemoriesStep(
            memory, olderThan, ConsolidationStrategy.Prune);

        // Act
        var result = await step(branch);

        // Assert
        result.Should().BeSameAs(branch);
        await memory.Received(1).ConsolidateMemoriesAsync(olderThan, ConsolidationStrategy.Prune, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConsolidateMemoriesStep_WhenConsolidationFails_ReturnsBranchUnchanged()
    {
        // Arrange
        var memory = Substitute.For<IEpisodicMemoryEngine>();
        var branch = CreateTestBranch();

        memory.ConsolidateMemoriesAsync(Arg.Any<TimeSpan>(), Arg.Any<ConsolidationStrategy>(), Arg.Any<CancellationToken>())
            .Returns(Result<Unit, string>.Failure("consolidation error"));

        var step = EpisodicMemoryExtensions.ConsolidateMemoriesStep(
            memory, TimeSpan.FromDays(7), ConsolidationStrategy.Abstract);

        // Act
        var result = await step(branch);

        // Assert
        result.Should().BeSameAs(branch);
    }

    [Theory]
    [InlineData(ConsolidationStrategy.Compress)]
    [InlineData(ConsolidationStrategy.Abstract)]
    [InlineData(ConsolidationStrategy.Prune)]
    [InlineData(ConsolidationStrategy.Hierarchical)]
    public async Task ConsolidateMemoriesStep_WithEachStrategy_PassesCorrectStrategy(ConsolidationStrategy strategy)
    {
        // Arrange
        var memory = Substitute.For<IEpisodicMemoryEngine>();
        var branch = CreateTestBranch();

        memory.ConsolidateMemoriesAsync(Arg.Any<TimeSpan>(), Arg.Any<ConsolidationStrategy>(), Arg.Any<CancellationToken>())
            .Returns(Result<Unit, string>.Success(Unit.Value));

        var step = EpisodicMemoryExtensions.ConsolidateMemoriesStep(memory, TimeSpan.FromDays(1), strategy);

        // Act
        await step(branch);

        // Assert
        await memory.Received(1).ConsolidateMemoriesAsync(Arg.Any<TimeSpan>(), strategy, Arg.Any<CancellationToken>());
    }

    #endregion

    #region ExtractGoalFromBranch Tests

    [Fact]
    public void ExtractGoalFromBranch_NullBranch_ThrowsArgumentNullException()
    {
        // Act
        var act = () => EpisodicMemoryExtensions.ExtractGoalFromBranch(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("branch");
    }

    [Fact]
    public void ExtractGoalFromBranch_WithNoEvents_ReturnsBranchName()
    {
        // Arrange
        var branch = CreateTestBranch("my-branch");

        // Act
        var goal = EpisodicMemoryExtensions.ExtractGoalFromBranch(branch);

        // Assert
        goal.Should().Be("my-branch");
    }

    [Fact]
    public void ExtractGoalFromBranch_WithEmptyEvents_ReturnsBranchName()
    {
        // Arrange
        var branch = CreateTestBranch("fallback-name");

        // Act
        var goal = EpisodicMemoryExtensions.ExtractGoalFromBranch(branch);

        // Assert
        goal.Should().Be("fallback-name");
    }

    #endregion
}
