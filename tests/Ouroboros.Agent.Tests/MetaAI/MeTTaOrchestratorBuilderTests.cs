using FluentAssertions;
using Ouroboros.Core.Hyperon;
using Ouroboros.Core.LawsOfForm;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class MeTTaOrchestratorBuilderTests
{
    private readonly Mock<Ouroboros.Abstractions.Core.IChatCompletionModel> _mockLlm = new();
    private readonly Mock<IMemoryStore> _mockMemory = new();
    private readonly Mock<ISkillRegistry> _mockSkills = new();
    private readonly Mock<IUncertaintyRouter> _mockRouter = new();
    private readonly Mock<ISafetyGuard> _mockSafety = new();
    private readonly Mock<IMeTTaEngine> _mockEngine = new();

    private MeTTaOrchestratorBuilder CreateConfiguredBuilder()
    {
        return new MeTTaOrchestratorBuilder()
            .WithLLM(_mockLlm.Object)
            .WithMemory(_mockMemory.Object)
            .WithSkills(_mockSkills.Object)
            .WithRouter(_mockRouter.Object)
            .WithSafety(_mockSafety.Object)
            .WithMeTTaEngine(_mockEngine.Object);
    }

    // === Build Validation Tests ===

    [Fact]
    public void Build_NoLlm_ThrowsInvalidOperationException()
    {
        var builder = new MeTTaOrchestratorBuilder()
            .WithMemory(_mockMemory.Object)
            .WithSkills(_mockSkills.Object)
            .WithRouter(_mockRouter.Object)
            .WithSafety(_mockSafety.Object)
            .WithMeTTaEngine(_mockEngine.Object);

        var act = () => builder.Build();

        act.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain("LLM");
    }

    [Fact]
    public void Build_NoMemory_ThrowsInvalidOperationException()
    {
        var builder = new MeTTaOrchestratorBuilder()
            .WithLLM(_mockLlm.Object)
            .WithSkills(_mockSkills.Object)
            .WithRouter(_mockRouter.Object)
            .WithSafety(_mockSafety.Object)
            .WithMeTTaEngine(_mockEngine.Object);

        var act = () => builder.Build();

        act.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain("Memory");
    }

    [Fact]
    public void Build_NoSkills_ThrowsInvalidOperationException()
    {
        var builder = new MeTTaOrchestratorBuilder()
            .WithLLM(_mockLlm.Object)
            .WithMemory(_mockMemory.Object)
            .WithRouter(_mockRouter.Object)
            .WithSafety(_mockSafety.Object)
            .WithMeTTaEngine(_mockEngine.Object);

        var act = () => builder.Build();

        act.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain("Skills");
    }

    [Fact]
    public void Build_NoRouter_ThrowsInvalidOperationException()
    {
        var builder = new MeTTaOrchestratorBuilder()
            .WithLLM(_mockLlm.Object)
            .WithMemory(_mockMemory.Object)
            .WithSkills(_mockSkills.Object)
            .WithSafety(_mockSafety.Object)
            .WithMeTTaEngine(_mockEngine.Object);

        var act = () => builder.Build();

        act.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain("Router");
    }

    [Fact]
    public void Build_NoSafety_ThrowsInvalidOperationException()
    {
        var builder = new MeTTaOrchestratorBuilder()
            .WithLLM(_mockLlm.Object)
            .WithMemory(_mockMemory.Object)
            .WithSkills(_mockSkills.Object)
            .WithRouter(_mockRouter.Object)
            .WithMeTTaEngine(_mockEngine.Object);

        var act = () => builder.Build();

        act.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain("Safety");
    }

    [Fact]
    public void Build_AllRequired_ReturnsOrchestrator()
    {
        var builder = CreateConfiguredBuilder();

        var orchestrator = builder.Build();

        orchestrator.Should().NotBeNull();
        orchestrator.Should().BeOfType<MeTTaOrchestrator>();
    }

    // === Fluent API Tests ===

    [Fact]
    public void WithLLM_ReturnsSameBuilder()
    {
        var builder = new MeTTaOrchestratorBuilder();
        var result = builder.WithLLM(_mockLlm.Object);
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithTools_ReturnsSameBuilder()
    {
        var builder = new MeTTaOrchestratorBuilder();
        var result = builder.WithTools(new ToolRegistry());
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithMemory_ReturnsSameBuilder()
    {
        var builder = new MeTTaOrchestratorBuilder();
        var result = builder.WithMemory(_mockMemory.Object);
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithSkills_ReturnsSameBuilder()
    {
        var builder = new MeTTaOrchestratorBuilder();
        var result = builder.WithSkills(_mockSkills.Object);
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithRouter_ReturnsSameBuilder()
    {
        var builder = new MeTTaOrchestratorBuilder();
        var result = builder.WithRouter(_mockRouter.Object);
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithSafety_ReturnsSameBuilder()
    {
        var builder = new MeTTaOrchestratorBuilder();
        var result = builder.WithSafety(_mockSafety.Object);
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithMeTTaEngine_ReturnsSameBuilder()
    {
        var builder = new MeTTaOrchestratorBuilder();
        var result = builder.WithMeTTaEngine(_mockEngine.Object);
        result.Should().BeSameAs(builder);
    }

    // === FormReasoning Tests ===

    [Fact]
    public void FormReasoningEnabled_InitiallyFalse()
    {
        var builder = new MeTTaOrchestratorBuilder();
        builder.FormReasoningEnabled.Should().BeFalse();
    }

    [Fact]
    public void WithFormReasoning_NoArgs_EnablesFormReasoning()
    {
        var builder = new MeTTaOrchestratorBuilder();
        builder.WithFormReasoning();
        builder.FormReasoningEnabled.Should().BeTrue();
    }

    [Fact]
    public void WithFormReasoning_WithBridge_EnablesFormReasoning()
    {
        var builder = new MeTTaOrchestratorBuilder();
        var bridge = new FormMeTTaBridge(new AtomSpace());
        builder.WithFormReasoning(bridge);
        builder.FormReasoningEnabled.Should().BeTrue();
    }

    [Fact]
    public void WithFormReasoning_NullBridge_ThrowsArgumentNullException()
    {
        var builder = new MeTTaOrchestratorBuilder();
        var act = () => builder.WithFormReasoning(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Build_WithFormReasoningEnabled_OrchestratorHasFormBridge()
    {
        var builder = CreateConfiguredBuilder().WithFormReasoning();

        var orchestrator = builder.Build();

        orchestrator.FormReasoningEnabled.Should().BeTrue();
        orchestrator.FormBridge.Should().NotBeNull();
    }

    // === NoMeTTaEngine Tests ===

    [Fact]
    public void Build_NoMeTTaEngine_UsesSubprocessEngine()
    {
        var builder = new MeTTaOrchestratorBuilder()
            .WithLLM(_mockLlm.Object)
            .WithMemory(_mockMemory.Object)
            .WithSkills(_mockSkills.Object)
            .WithRouter(_mockRouter.Object)
            .WithSafety(_mockSafety.Object);

        // Should not throw - creates SubprocessMeTTaEngine by default
        var orchestrator = builder.Build();
        orchestrator.Should().NotBeNull();
    }

    // === AgentRuntime Tests ===

    [Fact]
    public void WithAgentRuntime_NullRuntime_ThrowsArgumentNullException()
    {
        var builder = new MeTTaOrchestratorBuilder();
        var act = () => builder.WithAgentRuntime(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
