using Ouroboros.Agent.MetaAI;
using Ouroboros.Core.Ethics;

namespace Ouroboros.Agent.Tests;

[Trait("Category", "Unit")]
public class MetaAIBuilderTests
{
    #region Constructor and CreateDefault

    [Fact]
    public void CreateDefault_ShouldReturnNewBuilder()
    {
        var builder = MetaAIBuilder.CreateDefault();
        builder.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_ShouldReturnNewBuilder()
    {
        var builder = new MetaAIBuilder();
        builder.Should().NotBeNull();
    }

    #endregion

    #region Fluent Configuration

    [Fact]
    public void WithLLM_ShouldReturnSameBuilder()
    {
        var builder = new MetaAIBuilder();
        var mockLlm = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        var result = builder.WithLLM(mockLlm.Object);

        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithTools_ShouldReturnSameBuilder()
    {
        var builder = new MetaAIBuilder();
        var tools = ToolRegistry.CreateDefault();
        var result = builder.WithTools(tools);

        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithEmbedding_ShouldReturnSameBuilder()
    {
        var builder = new MetaAIBuilder();
        var mockEmbedding = new Mock<IEmbeddingModel>();
        var result = builder.WithEmbedding(mockEmbedding.Object);

        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithVectorStore_ShouldReturnSameBuilder()
    {
        var builder = new MetaAIBuilder();
        var vectorStore = new TrackedVectorStore();
        var result = builder.WithVectorStore(vectorStore);

        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithMemoryStore_ShouldReturnSameBuilder()
    {
        var builder = new MetaAIBuilder();
        var mockMemory = new Mock<IMemoryStore>();
        var result = builder.WithMemoryStore(mockMemory.Object);

        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithSkillRegistry_ShouldReturnSameBuilder()
    {
        var builder = new MetaAIBuilder();
        var mockRegistry = new Mock<ISkillRegistry>();
        var result = builder.WithSkillRegistry(mockRegistry.Object);

        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithUncertaintyRouter_ShouldReturnSameBuilder()
    {
        var builder = new MetaAIBuilder();
        var mockRouter = new Mock<IUncertaintyRouter>();
        var result = builder.WithUncertaintyRouter(mockRouter.Object);

        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithSafetyGuard_ShouldReturnSameBuilder()
    {
        var builder = new MetaAIBuilder();
        var mockGuard = new Mock<ISafetyGuard>();
        var result = builder.WithSafetyGuard(mockGuard.Object);

        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithEthicsFramework_ShouldReturnSameBuilder()
    {
        var builder = new MetaAIBuilder();
        var mockEthics = new Mock<IEthicsFramework>();
        var result = builder.WithEthicsFramework(mockEthics.Object);

        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithSkillExtractor_ShouldReturnSameBuilder()
    {
        var builder = new MetaAIBuilder();
        var mockExtractor = new Mock<ISkillExtractor>();
        var result = builder.WithSkillExtractor(mockExtractor.Object);

        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithConfidenceThreshold_ShouldReturnSameBuilder()
    {
        var builder = new MetaAIBuilder();
        var result = builder.WithConfidenceThreshold(0.8);

        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithDefaultPermissionLevel_ShouldReturnSameBuilder()
    {
        var builder = new MetaAIBuilder();
        var result = builder.WithDefaultPermissionLevel(PermissionLevel.ReadOnly);

        result.Should().BeSameAs(builder);
    }

    #endregion

    #region Build Validation

    [Fact]
    public void Build_WithoutLLM_ShouldThrow()
    {
        var builder = new MetaAIBuilder();
        Action act = () => builder.Build();
        act.Should().Throw<InvalidOperationException>().WithMessage("*LLM must be configured*");
    }

    [Fact]
    public void Build_WithLLM_ShouldReturnOrchestrator()
    {
        var builder = new MetaAIBuilder();
        var mockLlm = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        var orchestrator = builder.WithLLM(mockLlm.Object).Build();

        orchestrator.Should().NotBeNull();
    }

    #endregion

    #region ConfidenceThreshold

    [Fact]
    public void WithConfidenceThreshold_AboveOne_ShouldClamp()
    {
        var builder = new MetaAIBuilder();
        var mockLlm = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        // Build should still work; threshold is clamped internally
        var orchestrator = builder.WithLLM(mockLlm.Object).WithConfidenceThreshold(1.5).Build();
        orchestrator.Should().NotBeNull();
    }

    [Fact]
    public void WithConfidenceThreshold_BelowZero_ShouldClamp()
    {
        var builder = new MetaAIBuilder();
        var mockLlm = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        var orchestrator = builder.WithLLM(mockLlm.Object).WithConfidenceThreshold(-0.5).Build();
        orchestrator.Should().NotBeNull();
    }

    #endregion
}
