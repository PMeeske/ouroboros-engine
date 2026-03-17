using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Ouroboros.Pipeline.GraphRAG;
using Ouroboros.Pipeline.GraphRAG.Models;

namespace Ouroboros.Tests.GraphRAG;

[Trait("Category", "Unit")]
public sealed class GraphRetrievalArrowTests
{
    private readonly IHybridRetriever _retriever;
    private readonly IGraphExtractor _extractor;
    private readonly IQueryDecomposer _decomposer;

    public GraphRetrievalArrowTests()
    {
        _retriever = Substitute.For<IHybridRetriever>();
        _extractor = Substitute.For<IGraphExtractor>();
        _decomposer = Substitute.For<IQueryDecomposer>();
    }

    private static PipelineBranch CreateBranch()
    {
        var store = Substitute.For<IVectorStore>();
        var source = DataSource.FromPath("/test/path");
        return new PipelineBranch("test", store, source);
    }

    private static HybridSearchResult CreateSearchResult(params SearchMatch[] matches) =>
        new(matches.ToList(), new List<Inference>(), new List<ReasoningChainStep>());

    private static SearchMatch CreateMatch(string id, string name, double relevance) =>
        new(id, name, "Person", "Content for " + name, relevance, relevance * 0.9, relevance * 0.8);

    #region Search

    [Fact]
    public async Task Search_WithSuccessfulRetriever_ReturnsBranchAndResult()
    {
        // Arrange
        var branch = CreateBranch();
        var searchResult = CreateSearchResult(CreateMatch("e1", "Alice", 0.9));

        _retriever.SearchAsync("test query", Arg.Any<HybridSearchConfig>(), Arg.Any<CancellationToken>())
            .Returns(Result<HybridSearchResult, string>.Success(searchResult));

        var arrow = GraphRetrievalArrow.Search(_retriever, "test query");

        // Act
        var result = await arrow(branch);

        // Assert
        result.Branch.Should().Be(branch);
        result.Result.Matches.Should().HaveCount(1);
        result.Result.Matches[0].EntityName.Should().Be("Alice");
    }

    [Fact]
    public async Task Search_WithFailedRetriever_ReturnsEmptyResult()
    {
        // Arrange
        var branch = CreateBranch();

        _retriever.SearchAsync("test query", Arg.Any<HybridSearchConfig>(), Arg.Any<CancellationToken>())
            .Returns(Result<HybridSearchResult, string>.Failure("Search failed"));

        var arrow = GraphRetrievalArrow.Search(_retriever, "test query");

        // Act
        var result = await arrow(branch);

        // Assert
        result.Branch.Should().Be(branch);
        result.Result.Should().Be(HybridSearchResult.Empty);
    }

    [Fact]
    public async Task Search_WithNullConfig_UsesDefaultConfig()
    {
        // Arrange
        var branch = CreateBranch();
        var searchResult = CreateSearchResult();

        _retriever.SearchAsync("query", Arg.Any<HybridSearchConfig>(), Arg.Any<CancellationToken>())
            .Returns(Result<HybridSearchResult, string>.Success(searchResult));

        var arrow = GraphRetrievalArrow.Search(_retriever, "query", config: null);

        // Act
        await arrow(branch);

        // Assert
        await _retriever.Received(1).SearchAsync("query", Arg.Is<HybridSearchConfig>(c => c != null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Search_WithCustomConfig_UsesProvidedConfig()
    {
        // Arrange
        var branch = CreateBranch();
        var config = HybridSearchConfig.VectorFocused;
        var searchResult = CreateSearchResult();

        _retriever.SearchAsync("query", config, Arg.Any<CancellationToken>())
            .Returns(Result<HybridSearchResult, string>.Success(searchResult));

        var arrow = GraphRetrievalArrow.Search(_retriever, "query", config);

        // Act
        await arrow(branch);

        // Assert
        await _retriever.Received(1).SearchAsync("query", config, Arg.Any<CancellationToken>());
    }

    #endregion

    #region SearchSafe

    [Fact]
    public async Task SearchSafe_WithSuccessfulRetriever_ReturnsSuccessResult()
    {
        // Arrange
        var branch = CreateBranch();
        var searchResult = CreateSearchResult(CreateMatch("e1", "Alice", 0.9));

        _retriever.SearchAsync("query", Arg.Any<HybridSearchConfig>(), Arg.Any<CancellationToken>())
            .Returns(Result<HybridSearchResult, string>.Success(searchResult));

        var arrow = GraphRetrievalArrow.SearchSafe(_retriever, "query");

        // Act
        var result = await arrow(branch);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Branch.Should().Be(branch);
        result.Value.Result.Matches.Should().HaveCount(1);
    }

    [Fact]
    public async Task SearchSafe_WithFailedRetriever_ReturnsFailureResult()
    {
        // Arrange
        var branch = CreateBranch();

        _retriever.SearchAsync("query", Arg.Any<HybridSearchConfig>(), Arg.Any<CancellationToken>())
            .Returns(Result<HybridSearchResult, string>.Failure("not found"));

        var arrow = GraphRetrievalArrow.SearchSafe(_retriever, "query");

        // Act
        var result = await arrow(branch);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SearchSafe_WithException_ReturnsFailureWithMessage()
    {
        // Arrange
        var branch = CreateBranch();

        _retriever.SearchAsync("query", Arg.Any<HybridSearchConfig>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("connection lost"));

        var arrow = GraphRetrievalArrow.SearchSafe(_retriever, "query");

        // Act
        var result = await arrow(branch);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SearchSafe_WithOperationCanceledException_Rethrows()
    {
        // Arrange
        var branch = CreateBranch();

        _retriever.SearchAsync("query", Arg.Any<HybridSearchConfig>(), Arg.Any<CancellationToken>())
            .Throws(new OperationCanceledException());

        var arrow = GraphRetrievalArrow.SearchSafe(_retriever, "query");

        // Act
        var act = () => arrow(branch);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task SearchSafe_WithNullConfig_UsesDefaultConfig()
    {
        // Arrange
        var branch = CreateBranch();
        var searchResult = CreateSearchResult();

        _retriever.SearchAsync("query", Arg.Any<HybridSearchConfig>(), Arg.Any<CancellationToken>())
            .Returns(Result<HybridSearchResult, string>.Success(searchResult));

        var arrow = GraphRetrievalArrow.SearchSafe(_retriever, "query", config: null);

        // Act
        await arrow(branch);

        // Assert
        await _retriever.Received(1).SearchAsync("query", Arg.Is<HybridSearchConfig>(c => c != null), Arg.Any<CancellationToken>());
    }

    #endregion

    #region Extract

    [Fact]
    public async Task Extract_WithSuccessfulExtractor_ReturnsBranchAndGraph()
    {
        // Arrange
        var branch = CreateBranch();
        var entity = Entity.Create("e1", "Person", "Alice");
        var graph = KnowledgeGraph.Empty.WithEntity(entity);

        _extractor.ExtractAsync("some content", Arg.Any<CancellationToken>())
            .Returns(Result<KnowledgeGraph, string>.Success(graph));

        var arrow = GraphRetrievalArrow.Extract(_extractor, _ => "some content");

        // Act
        var result = await arrow(branch);

        // Assert
        result.Branch.Should().Be(branch);
        result.Graph.Entities.Should().HaveCount(1);
        result.Graph.Entities[0].Name.Should().Be("Alice");
    }

    [Fact]
    public async Task Extract_WithFailedExtractor_ReturnsEmptyGraph()
    {
        // Arrange
        var branch = CreateBranch();

        _extractor.ExtractAsync("content", Arg.Any<CancellationToken>())
            .Returns(Result<KnowledgeGraph, string>.Failure("extraction failed"));

        var arrow = GraphRetrievalArrow.Extract(_extractor, _ => "content");

        // Act
        var result = await arrow(branch);

        // Assert
        result.Branch.Should().Be(branch);
        result.Graph.Should().Be(KnowledgeGraph.Empty);
    }

    [Fact]
    public async Task Extract_UsesContentSelector()
    {
        // Arrange
        var branch = CreateBranch();

        _extractor.ExtractAsync("selected-content", Arg.Any<CancellationToken>())
            .Returns(Result<KnowledgeGraph, string>.Success(KnowledgeGraph.Empty));

        var arrow = GraphRetrievalArrow.Extract(_extractor, b => "selected-content");

        // Act
        await arrow(branch);

        // Assert
        await _extractor.Received(1).ExtractAsync("selected-content", Arg.Any<CancellationToken>());
    }

    #endregion

    #region Traverse

    [Fact]
    public async Task Traverse_WithSuccessfulRetriever_ReturnsBranchAndSubgraph()
    {
        // Arrange
        var branch = CreateBranch();
        var subgraph = KnowledgeGraph.Empty
            .WithEntity(Entity.Create("e1", "Person", "Alice"))
            .WithEntity(Entity.Create("e2", "Person", "Bob"));

        _retriever.TraverseAsync("e1", Arg.Any<IEnumerable<string>?>(), 2, Arg.Any<CancellationToken>())
            .Returns(Result<KnowledgeGraph, string>.Success(subgraph));

        var arrow = GraphRetrievalArrow.Traverse(_retriever, "e1");

        // Act
        var result = await arrow(branch);

        // Assert
        result.Branch.Should().Be(branch);
        result.Subgraph.Entities.Should().HaveCount(2);
    }

    [Fact]
    public async Task Traverse_WithFailedRetriever_ReturnsEmptyGraph()
    {
        // Arrange
        var branch = CreateBranch();

        _retriever.TraverseAsync("e1", Arg.Any<IEnumerable<string>?>(), 2, Arg.Any<CancellationToken>())
            .Returns(Result<KnowledgeGraph, string>.Failure("traversal failed"));

        var arrow = GraphRetrievalArrow.Traverse(_retriever, "e1");

        // Act
        var result = await arrow(branch);

        // Assert
        result.Subgraph.Should().Be(KnowledgeGraph.Empty);
    }

    [Fact]
    public async Task Traverse_WithCustomMaxHops_PassesMaxHopsToRetriever()
    {
        // Arrange
        var branch = CreateBranch();

        _retriever.TraverseAsync("e1", Arg.Any<IEnumerable<string>?>(), 5, Arg.Any<CancellationToken>())
            .Returns(Result<KnowledgeGraph, string>.Success(KnowledgeGraph.Empty));

        var arrow = GraphRetrievalArrow.Traverse(_retriever, "e1", maxHops: 5);

        // Act
        await arrow(branch);

        // Assert
        await _retriever.Received(1).TraverseAsync("e1", Arg.Any<IEnumerable<string>?>(), 5, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Traverse_WithRelationshipTypes_PassesTypesToRetriever()
    {
        // Arrange
        var branch = CreateBranch();
        var types = new List<string> { "WorksFor", "LocatedIn" };

        _retriever.TraverseAsync("e1", Arg.Any<IEnumerable<string>?>(), 2, Arg.Any<CancellationToken>())
            .Returns(Result<KnowledgeGraph, string>.Success(KnowledgeGraph.Empty));

        var arrow = GraphRetrievalArrow.Traverse(_retriever, "e1", relationshipTypes: types);

        // Act
        await arrow(branch);

        // Assert
        await _retriever.Received(1).TraverseAsync("e1", Arg.Any<IEnumerable<string>?>(), 2, Arg.Any<CancellationToken>());
    }

    #endregion

    #region ExecuteQuery

    [Fact]
    public async Task ExecuteQuery_WithSuccessfulDecompositionAndSearch_ReturnsFullResult()
    {
        // Arrange
        var branch = CreateBranch();
        var plan = QueryPlan.SingleHop("test query");
        var searchResult = CreateSearchResult(CreateMatch("e1", "Alice", 0.9));

        _decomposer.DecomposeAsync("test query", Arg.Any<CancellationToken>())
            .Returns(Result<QueryPlan, string>.Success(plan));

        _retriever.ExecutePlanAsync(plan, Arg.Any<HybridSearchConfig>(), Arg.Any<CancellationToken>())
            .Returns(Result<HybridSearchResult, string>.Success(searchResult));

        var arrow = GraphRetrievalArrow.ExecuteQuery(_decomposer, _retriever, "test query");

        // Act
        var result = await arrow(branch);

        // Assert
        result.Branch.Should().Be(branch);
        result.Result.Matches.Should().HaveCount(1);
        result.Plan.Should().Be(plan);
    }

    [Fact]
    public async Task ExecuteQuery_WithFailedDecomposition_ReturnsFallbackPlanAndEmptyResult()
    {
        // Arrange
        var branch = CreateBranch();

        _decomposer.DecomposeAsync("bad query", Arg.Any<CancellationToken>())
            .Returns(Result<QueryPlan, string>.Failure("decomposition failed"));

        var arrow = GraphRetrievalArrow.ExecuteQuery(_decomposer, _retriever, "bad query");

        // Act
        var result = await arrow(branch);

        // Assert
        result.Branch.Should().Be(branch);
        result.Result.Should().Be(HybridSearchResult.Empty);
        result.Plan.OriginalQuery.Should().Be("bad query");
        result.Plan.QueryType.Should().Be(QueryType.SingleHop);
    }

    [Fact]
    public async Task ExecuteQuery_WithFailedSearch_ReturnsEmptyResultWithPlan()
    {
        // Arrange
        var branch = CreateBranch();
        var plan = QueryPlan.SingleHop("query");

        _decomposer.DecomposeAsync("query", Arg.Any<CancellationToken>())
            .Returns(Result<QueryPlan, string>.Success(plan));

        _retriever.ExecutePlanAsync(plan, Arg.Any<HybridSearchConfig>(), Arg.Any<CancellationToken>())
            .Returns(Result<HybridSearchResult, string>.Failure("search failed"));

        var arrow = GraphRetrievalArrow.ExecuteQuery(_decomposer, _retriever, "query");

        // Act
        var result = await arrow(branch);

        // Assert
        result.Result.Should().Be(HybridSearchResult.Empty);
        result.Plan.Should().Be(plan);
    }

    [Fact]
    public async Task ExecuteQuery_WithNullConfig_UsesDefaultConfig()
    {
        // Arrange
        var branch = CreateBranch();
        var plan = QueryPlan.SingleHop("query");

        _decomposer.DecomposeAsync("query", Arg.Any<CancellationToken>())
            .Returns(Result<QueryPlan, string>.Success(plan));

        _retriever.ExecutePlanAsync(plan, Arg.Any<HybridSearchConfig>(), Arg.Any<CancellationToken>())
            .Returns(Result<HybridSearchResult, string>.Success(CreateSearchResult()));

        var arrow = GraphRetrievalArrow.ExecuteQuery(_decomposer, _retriever, "query", config: null);

        // Act
        await arrow(branch);

        // Assert
        await _retriever.Received(1).ExecutePlanAsync(plan, Arg.Is<HybridSearchConfig>(c => c != null), Arg.Any<CancellationToken>());
    }

    #endregion

    #region FormatAsContext

    [Fact]
    public void FormatAsContext_WithNoMatches_ReturnsNoInfoMessage()
    {
        // Arrange
        var result = HybridSearchResult.Empty;

        // Act
        var context = GraphRetrievalArrow.FormatAsContext(result);

        // Assert
        context.Should().Be("No relevant information found.");
    }

    [Fact]
    public void FormatAsContext_WithMatches_FormatsCorrectly()
    {
        // Arrange
        var matches = new List<SearchMatch>
        {
            new("e1", "Alice", "Person", "Alice is a developer", 0.9, 0.85, 0.95)
        };
        var result = new HybridSearchResult(matches, [], []);

        // Act
        var context = GraphRetrievalArrow.FormatAsContext(result);

        // Assert
        context.Should().Contain("[1] Alice (Person)");
        context.Should().Contain("Relevance:");
        context.Should().Contain("Content: Alice is a developer");
    }

    [Fact]
    public void FormatAsContext_WithMultipleMatches_NumbersSequentially()
    {
        // Arrange
        var matches = new List<SearchMatch>
        {
            new("e1", "Alice", "Person", "Content A", 0.9, 0.85, 0.95),
            new("e2", "Bob", "Person", "Content B", 0.8, 0.75, 0.85)
        };
        var result = new HybridSearchResult(matches, [], []);

        // Act
        var context = GraphRetrievalArrow.FormatAsContext(result);

        // Assert
        context.Should().Contain("[1]");
        context.Should().Contain("[2]");
    }

    [Fact]
    public void FormatAsContext_WithMaxMatchesLimit_ReturnsLimitedResults()
    {
        // Arrange
        var matches = new List<SearchMatch>
        {
            new("e1", "Alice", "Person", "Content A", 0.9, 0.85, 0.95),
            new("e2", "Bob", "Person", "Content B", 0.8, 0.75, 0.85),
            new("e3", "Charlie", "Person", "Content C", 0.7, 0.65, 0.75)
        };
        var result = new HybridSearchResult(matches, [], []);

        // Act
        var context = GraphRetrievalArrow.FormatAsContext(result, maxMatches: 2);

        // Assert
        context.Should().Contain("[1]");
        context.Should().Contain("[2]");
        context.Should().NotContain("[3]");
    }

    [Fact]
    public void FormatAsContext_OrdersByRelevanceDescending()
    {
        // Arrange
        var matches = new List<SearchMatch>
        {
            new("e1", "Low", "Person", "Content", 0.3, 0.2, 0.4),
            new("e2", "High", "Person", "Content", 0.9, 0.85, 0.95)
        };
        var result = new HybridSearchResult(matches, [], []);

        // Act
        var context = GraphRetrievalArrow.FormatAsContext(result);

        // Assert
        var highIndex = context.IndexOf("High", StringComparison.Ordinal);
        var lowIndex = context.IndexOf("Low", StringComparison.Ordinal);
        highIndex.Should().BeLessThan(lowIndex, "higher relevance should come first");
    }

    #endregion

    #region FormatReasoningChain

    [Fact]
    public void FormatReasoningChain_WithNoChain_ReturnsNoChainMessage()
    {
        // Arrange
        var result = HybridSearchResult.Empty;

        // Act
        var formatted = GraphRetrievalArrow.FormatReasoningChain(result);

        // Assert
        formatted.Should().Be("No reasoning chain available.");
    }

    [Fact]
    public void FormatReasoningChain_WithSteps_FormatsCorrectly()
    {
        // Arrange
        var chain = new List<ReasoningChainStep>
        {
            new(1, "Traverse", "Following WorksFor relationship", new List<string> { "e1", "e2" }),
            new(2, "Match", "Pattern matching on Person type", new List<string> { "e2" })
        };
        var result = new HybridSearchResult([], [], chain);

        // Act
        var formatted = GraphRetrievalArrow.FormatReasoningChain(result);

        // Assert
        formatted.Should().StartWith("Reasoning Chain:");
        formatted.Should().Contain("Step 1: Traverse");
        formatted.Should().Contain("Following WorksFor relationship");
        formatted.Should().Contain("Step 2: Match");
        formatted.Should().Contain("Pattern matching on Person type");
    }

    [Fact]
    public void FormatReasoningChain_WithSingleStep_FormatsCorrectly()
    {
        // Arrange
        var chain = new List<ReasoningChainStep>
        {
            new(1, "Infer", "Applying transitivity rule", new List<string> { "e1" })
        };
        var result = new HybridSearchResult([], [], chain);

        // Act
        var formatted = GraphRetrievalArrow.FormatReasoningChain(result);

        // Assert
        formatted.Should().Contain("Step 1: Infer");
        formatted.Should().Contain("Applying transitivity rule");
    }

    #endregion
}
