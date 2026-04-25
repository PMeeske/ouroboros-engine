namespace Ouroboros.Pipeline.Tests;

using Ouroboros.Pipeline.Retrieval;

[Trait("Category", "Unit")]
public class HybridRetrievalResultTests
{
    #region Creation

    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        var result = new HybridRetrievalResult("query", new[] { "doc1" }, new List<LangChain.DocumentLoaders.Document>());
        result.Query.Should().Be("query");
        result.SymbolicMatches.Should().ContainSingle();
        result.SemanticMatches.Should().BeEmpty();
    }

    [Fact]
    public void AllDocumentIds_ShouldCombineBothSources()
    {
        var doc = new LangChain.DocumentLoaders.Document { PageContent = "content", Metadata = new Dictionary<string, object> { ["id"] = "doc2" } };
        var result = new HybridRetrievalResult("query", new[] { "doc1" }, new List<LangChain.DocumentLoaders.Document> { doc });
        var ids = result.AllDocumentIds.ToList();
        ids.Should().Contain("doc1");
        ids.Should().Contain("doc2");
    }

    [Fact]
    public void AllDocumentIds_NoMetadataId_ShouldUseFallback()
    {
        var doc = new LangChain.DocumentLoaders.Document { PageContent = "content", Metadata = new Dictionary<string, object>() };
        var result = new HybridRetrievalResult("query", Array.Empty<string>(), new List<LangChain.DocumentLoaders.Document> { doc });
        var ids = result.AllDocumentIds.ToList();
        ids.Should().ContainSingle();
        ids[0].Should().Be("content");
    }

    #endregion
}

[Trait("Category", "Unit")]
public class SymbolicRetrievalStepTests
{
    #region Construction

    [Fact]
    public void Constructor_NullEngine_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new SymbolicRetrievalStep(null!));
    }

    [Fact]
    public void Constructor_ValidEngine_ShouldInitialize()
    {
        var engine = new Mock<IMeTTaEngine>().Object;
        var step = new SymbolicRetrievalStep(engine);
        step.Should().NotBeNull();
    }

    #endregion

    #region RetrieveByStatusAsync

    [Fact]
    public async Task RetrieveByStatusAsync_ShouldReturnResult()
    {
        var engine = new Mock<IMeTTaEngine>();
        engine.Setup(e => e.ExecuteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("result");
        var step = new SymbolicRetrievalStep(engine.Object);
        var result = await step.RetrieveByStatusAsync("Current");
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region RetrieveByTopicAsync

    [Fact]
    public async Task RetrieveByTopicAsync_ShouldReturnResult()
    {
        var engine = new Mock<IMeTTaEngine>();
        engine.Setup(e => e.ExecuteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("result");
        var step = new SymbolicRetrievalStep(engine.Object);
        var result = await step.RetrieveByTopicAsync("topic");
        result.IsSuccess.Should().BeTrue();
    }

    #endregion
}
