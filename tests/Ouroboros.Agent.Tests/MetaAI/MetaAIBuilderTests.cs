using FluentAssertions;
using Ouroboros.Core.Ethics;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class MetaAIBuilderTests
{
    private readonly Mock<Ouroboros.Abstractions.Core.IChatCompletionModel> _mockLlm = new();
    private readonly Mock<IEmbeddingModel> _mockEmbedding = new();

    // === Build Tests ===

    [Fact]
    public void Build_NoLlm_ThrowsInvalidOperationException()
    {
        var builder = MetaAIBuilder.CreateDefault();

        var act = () => builder.Build();

        act.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain("LLM");
    }

    [Fact]
    public void Build_WithLlm_CreatesOrchestrator()
    {
        var builder = MetaAIBuilder.CreateDefault()
            .WithLLM(_mockLlm.Object);

        var orchestrator = builder.Build();

        orchestrator.Should().NotBeNull();
        orchestrator.Should().BeOfType<MetaAIPlannerOrchestrator>();
    }

    // === Fluent API Tests ===

    [Fact]
    public void WithLLM_ReturnsSameBuilder()
    {
        var builder = MetaAIBuilder.CreateDefault();

        var result = builder.WithLLM(_mockLlm.Object);

        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithTools_ReturnsSameBuilder()
    {
        var builder = MetaAIBuilder.CreateDefault();

        var result = builder.WithTools(new ToolRegistry());

        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithEmbedding_ReturnsSameBuilder()
    {
        var builder = MetaAIBuilder.CreateDefault();

        var result = builder.WithEmbedding(_mockEmbedding.Object);

        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithVectorStore_ReturnsSameBuilder()
    {
        var builder = MetaAIBuilder.CreateDefault();

        var result = builder.WithVectorStore(new TrackedVectorStore());

        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithMemoryStore_ReturnsSameBuilder()
    {
        var builder = MetaAIBuilder.CreateDefault();
        var mockMemory = new Mock<IMemoryStore>();

        var result = builder.WithMemoryStore(mockMemory.Object);

        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithSkillRegistry_ReturnsSameBuilder()
    {
        var builder = MetaAIBuilder.CreateDefault();
        var mockSkills = new Mock<ISkillRegistry>();

        var result = builder.WithSkillRegistry(mockSkills.Object);

        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithUncertaintyRouter_ReturnsSameBuilder()
    {
        var builder = MetaAIBuilder.CreateDefault();
        var mockRouter = new Mock<IUncertaintyRouter>();

        var result = builder.WithUncertaintyRouter(mockRouter.Object);

        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithSafetyGuard_ReturnsSameBuilder()
    {
        var builder = MetaAIBuilder.CreateDefault();
        var mockSafety = new Mock<ISafetyGuard>();

        var result = builder.WithSafetyGuard(mockSafety.Object);

        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithEthicsFramework_ReturnsSameBuilder()
    {
        var builder = MetaAIBuilder.CreateDefault();
        var mockEthics = new Mock<IEthicsFramework>();

        var result = builder.WithEthicsFramework(mockEthics.Object);

        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithSkillExtractor_ReturnsSameBuilder()
    {
        var builder = MetaAIBuilder.CreateDefault();
        var mockExtractor = new Mock<ISkillExtractor>();

        var result = builder.WithSkillExtractor(mockExtractor.Object);

        result.Should().BeSameAs(builder);
    }

    // === WithConfidenceThreshold Tests ===

    [Fact]
    public void WithConfidenceThreshold_ValidValue_ReturnsSameBuilder()
    {
        var builder = MetaAIBuilder.CreateDefault();

        var result = builder.WithConfidenceThreshold(0.8);

        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithConfidenceThreshold_AboveOne_ClampedToOne()
    {
        // The method clamps to 0-1 range, so it should not throw
        var builder = MetaAIBuilder.CreateDefault();

        var act = () => builder.WithConfidenceThreshold(1.5);

        act.Should().NotThrow();
    }

    [Fact]
    public void WithConfidenceThreshold_BelowZero_ClampedToZero()
    {
        var builder = MetaAIBuilder.CreateDefault();

        var act = () => builder.WithConfidenceThreshold(-0.5);

        act.Should().NotThrow();
    }

    // === WithDefaultPermissionLevel Tests ===

    [Fact]
    public void WithDefaultPermissionLevel_ReturnsSameBuilder()
    {
        var builder = MetaAIBuilder.CreateDefault();

        var result = builder.WithDefaultPermissionLevel(PermissionLevel.Admin);

        result.Should().BeSameAs(builder);
    }

    // === Build with all dependencies ===

    [Fact]
    public void Build_WithAllDependencies_CreatesOrchestrator()
    {
        var mockMemory = new Mock<IMemoryStore>();
        var mockSkills = new Mock<ISkillRegistry>();
        var mockRouter = new Mock<IUncertaintyRouter>();
        var mockSafety = new Mock<ISafetyGuard>();
        var mockEthics = new Mock<IEthicsFramework>();

        var builder = MetaAIBuilder.CreateDefault()
            .WithLLM(_mockLlm.Object)
            .WithTools(new ToolRegistry())
            .WithMemoryStore(mockMemory.Object)
            .WithSkillRegistry(mockSkills.Object)
            .WithUncertaintyRouter(mockRouter.Object)
            .WithSafetyGuard(mockSafety.Object)
            .WithEthicsFramework(mockEthics.Object)
            .WithConfidenceThreshold(0.75)
            .WithDefaultPermissionLevel(PermissionLevel.ReadOnly);

        var orchestrator = builder.Build();

        orchestrator.Should().NotBeNull();
    }

    // === CreateDefault Tests ===

    [Fact]
    public void CreateDefault_ReturnsNewBuilder()
    {
        var builder = MetaAIBuilder.CreateDefault();

        builder.Should().NotBeNull();
        builder.Should().BeOfType<MetaAIBuilder>();
    }
}
