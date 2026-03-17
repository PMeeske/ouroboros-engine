using FluentAssertions;
using Ouroboros.Agent;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public class OrchestratorBuilderTests
{
    private readonly Mock<Ouroboros.Abstractions.Core.IChatCompletionModel> _mockModel = new();

    // === Constructor Tests ===

    [Fact]
    public void Constructor_ValidArgs_DoesNotThrow()
    {
        var act = () => new OrchestratorBuilder(ToolRegistry.CreateDefault());
        act.Should().NotThrow();
    }

    // === WithModel Tests ===

    [Fact]
    public void WithModel_ValidArgs_ReturnsSameBuilder()
    {
        var builder = new OrchestratorBuilder(ToolRegistry.CreateDefault());

        var result = builder.WithModel("test-model", _mockModel.Object, ModelType.General, new[] { "general" });

        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithModel_MultipleModels_ReturnsSameBuilder()
    {
        var builder = new OrchestratorBuilder(ToolRegistry.CreateDefault());

        var result = builder
            .WithModel("model-1", _mockModel.Object, ModelType.Code, new[] { "code" })
            .WithModel("model-2", _mockModel.Object, ModelType.Creative, new[] { "creative" });

        result.Should().BeSameAs(builder);
    }

    // === WithMetricTracking Tests ===

    [Fact]
    public void WithMetricTracking_ReturnsBuilder()
    {
        var builder = new OrchestratorBuilder(ToolRegistry.CreateDefault());

        var result = builder.WithMetricTracking(false);

        result.Should().BeSameAs(builder);
    }

    // === Build Tests ===

    [Fact]
    public void Build_NoModels_ReturnsOrchestratedChatModel()
    {
        var builder = new OrchestratorBuilder(ToolRegistry.CreateDefault());

        var chatModel = builder.Build();

        chatModel.Should().NotBeNull();
        chatModel.Should().BeOfType<OrchestratedChatModel>();
    }

    [Fact]
    public void Build_WithModels_RegistersModelsAndReturnsModel()
    {
        var builder = new OrchestratorBuilder(ToolRegistry.CreateDefault())
            .WithModel("test-model", _mockModel.Object, ModelType.General, new[] { "general" });

        var chatModel = builder.Build();

        chatModel.Should().NotBeNull();
    }

    // === GetOrchestrator Tests ===

    [Fact]
    public void GetOrchestrator_ReturnsUnderlyingOrchestrator()
    {
        var builder = new OrchestratorBuilder(ToolRegistry.CreateDefault());

        var orchestrator = builder.GetOrchestrator();

        orchestrator.Should().NotBeNull();
        orchestrator.Should().BeAssignableTo<IModelOrchestrator>();
    }
}
