using Ouroboros.Pipeline.Branches;

namespace Ouroboros.Tests.Pipeline.Branches;

[Trait("Category", "Unit")]
public sealed class PipelineBranchTests
{
    private static PipelineBranch CreateBranch(string name = "test-branch")
    {
        return new PipelineBranch(name, new TrackedVectorStore(), DataSource.FromPath("/test"));
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidArgs_SetsProperties()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test");

        // Act
        var branch = new PipelineBranch("my-branch", store, source);

        // Assert
        branch.Name.Should().Be("my-branch");
        branch.Store.Should().BeSameAs(store);
        branch.Source.Should().Be(source);
        branch.Events.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithNullName_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new PipelineBranch(null!, new TrackedVectorStore(), DataSource.FromPath("."));

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullStore_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new PipelineBranch("test", null!, DataSource.FromPath("."));

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullSource_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new PipelineBranch("test", new TrackedVectorStore(), null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region WithEvents Factory Tests

    [Fact]
    public void WithEvents_CreatesInstanceWithExistingEvents()
    {
        // Arrange
        var events = new List<PipelineEvent>
        {
            new IngestBatch(Guid.NewGuid(), "src", new List<string> { "id1" }, DateTime.UtcNow)
        };

        // Act
        var branch = PipelineBranch.WithEvents("restored", new TrackedVectorStore(), DataSource.FromPath("."), events);

        // Assert
        branch.Name.Should().Be("restored");
        branch.Events.Should().HaveCount(1);
    }

    #endregion

    #region WithReasoning Tests

    [Fact]
    public void WithReasoning_ReturnsNewBranchWithEvent()
    {
        // Arrange
        var branch = CreateBranch();

        // Act
        var updated = branch.WithReasoning(new Draft("draft text"), "prompt");

        // Assert
        updated.Events.Should().HaveCount(1);
        branch.Events.Should().BeEmpty(); // original unchanged
    }

    [Fact]
    public void WithReasoning_WithNullState_ThrowsArgumentNullException()
    {
        // Arrange
        var branch = CreateBranch();

        // Act
        Action act = () => branch.WithReasoning(null!, "prompt");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WithReasoning_WithNullPrompt_ThrowsArgumentNullException()
    {
        // Arrange
        var branch = CreateBranch();

        // Act
        Action act = () => branch.WithReasoning(new Draft("text"), null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WithReasoning_PreservesExistingEvents()
    {
        // Arrange
        var branch = CreateBranch();
        branch = branch.WithReasoning(new Draft("first"), "p1");

        // Act
        var updated = branch.WithReasoning(new Critique("second"), "p2");

        // Assert
        updated.Events.Should().HaveCount(2);
    }

    [Fact]
    public void WithReasoning_WithToolCalls_IncludesToolsInEvent()
    {
        // Arrange
        var branch = CreateBranch();
        var tools = new List<ToolExecution>
        {
            new("tool1", "args", "output", DateTime.UtcNow)
        };

        // Act
        var updated = branch.WithReasoning(new Draft("text"), "prompt", tools);

        // Assert
        var step = updated.Events[0] as Ouroboros.Domain.Events.ReasoningStep;
        step!.ToolCalls.Should().HaveCount(1);
    }

    #endregion

    #region WithIngestEvent Tests

    [Fact]
    public void WithIngestEvent_ReturnsNewBranchWithIngestBatch()
    {
        // Arrange
        var branch = CreateBranch();

        // Act
        var updated = branch.WithIngestEvent("source-1", new[] { "doc1", "doc2" });

        // Assert
        updated.Events.Should().HaveCount(1);
        updated.Events[0].Should().BeOfType<IngestBatch>();
    }

    [Fact]
    public void WithIngestEvent_WithNullSource_ThrowsArgumentNullException()
    {
        // Arrange
        var branch = CreateBranch();

        // Act
        Action act = () => branch.WithIngestEvent(null!, new[] { "id" });

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WithIngestEvent_WithNullIds_ThrowsArgumentNullException()
    {
        // Arrange
        var branch = CreateBranch();

        // Act
        Action act = () => branch.WithIngestEvent("source", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region WithSource Tests

    [Fact]
    public void WithSource_ReturnsNewBranchWithUpdatedSource()
    {
        // Arrange
        var branch = CreateBranch();
        var newSource = DataSource.FromPath("/new/path");

        // Act
        var updated = branch.WithSource(newSource);

        // Assert
        updated.Source.Should().Be(newSource);
        updated.Name.Should().Be(branch.Name);
    }

    [Fact]
    public void WithSource_PreservesEvents()
    {
        // Arrange
        var branch = CreateBranch();
        branch = branch.WithReasoning(new Draft("text"), "prompt");

        // Act
        var updated = branch.WithSource(DataSource.FromPath("/new"));

        // Assert
        updated.Events.Should().HaveCount(1);
    }

    [Fact]
    public void WithSource_WithNullSource_ThrowsArgumentNullException()
    {
        // Arrange
        var branch = CreateBranch();

        // Act
        Action act = () => branch.WithSource(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Fork Tests

    [Fact]
    public void Fork_CreatesNewBranchWithNewNameAndStore()
    {
        // Arrange
        var branch = CreateBranch("original");
        branch = branch.WithReasoning(new Draft("text"), "prompt");
        var newStore = new TrackedVectorStore();

        // Act
        var forked = branch.Fork("forked", newStore);

        // Assert
        forked.Name.Should().Be("forked");
        forked.Store.Should().BeSameAs(newStore);
        forked.Events.Should().HaveCount(1); // events are copied
    }

    [Fact]
    public void Fork_WithNullName_ThrowsArgumentNullException()
    {
        // Arrange
        var branch = CreateBranch();

        // Act
        Action act = () => branch.Fork(null!, new TrackedVectorStore());

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Fork_WithNullStore_ThrowsArgumentNullException()
    {
        // Arrange
        var branch = CreateBranch();

        // Act
        Action act = () => branch.Fork("forked", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region WithEvent Tests

    [Fact]
    public void WithEvent_AddsGenericEvent()
    {
        // Arrange
        var branch = CreateBranch();
        var evt = new IngestBatch(Guid.NewGuid(), "src", new List<string>(), DateTime.UtcNow);

        // Act
        var updated = branch.WithEvent(evt);

        // Assert
        updated.Events.Should().HaveCount(1);
    }

    [Fact]
    public void WithEvent_WithNullEvent_ThrowsArgumentNullException()
    {
        // Arrange
        var branch = CreateBranch();

        // Act
        Action act = () => branch.WithEvent(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Immutability Tests

    [Fact]
    public void AllMutations_ReturnNewInstances()
    {
        // Arrange
        var original = CreateBranch();

        // Act
        var withReasoning = original.WithReasoning(new Draft("text"), "prompt");
        var withIngest = original.WithIngestEvent("src", new[] { "id" });
        var withSource = original.WithSource(DataSource.FromPath("/new"));
        var forked = original.Fork("fork", new TrackedVectorStore());

        // Assert
        original.Events.Should().BeEmpty();
        withReasoning.Events.Should().HaveCount(1);
        withIngest.Events.Should().HaveCount(1);
        withSource.Events.Should().BeEmpty();
        forked.Events.Should().BeEmpty();
    }

    #endregion
}
