using Ouroboros.Providers.Routing;

namespace Ouroboros.Tests.Routing;

[Trait("Category", "Unit")]
public sealed class HybridRoutingConfigTests
{
    [Fact]
    public void Ctor_SetsDefaultModel()
    {
        var mockModel = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        var config = new HybridRoutingConfig(DefaultModel: mockModel.Object);

        config.DefaultModel.Should().BeSameAs(mockModel.Object);
        config.ReasoningModel.Should().BeNull();
        config.PlanningModel.Should().BeNull();
        config.CodingModel.Should().BeNull();
        config.FallbackModel.Should().BeNull();
        config.DetectionStrategy.Should().Be(TaskDetectionStrategy.Heuristic);
    }

    [Fact]
    public void Ctor_WithAllModels_PreservesAll()
    {
        var defaultModel = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>().Object;
        var reasoningModel = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>().Object;
        var planningModel = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>().Object;
        var codingModel = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>().Object;
        var fallbackModel = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>().Object;

        var config = new HybridRoutingConfig(
            DefaultModel: defaultModel,
            ReasoningModel: reasoningModel,
            PlanningModel: planningModel,
            CodingModel: codingModel,
            FallbackModel: fallbackModel,
            DetectionStrategy: TaskDetectionStrategy.RuleBased);

        config.DefaultModel.Should().BeSameAs(defaultModel);
        config.ReasoningModel.Should().BeSameAs(reasoningModel);
        config.PlanningModel.Should().BeSameAs(planningModel);
        config.CodingModel.Should().BeSameAs(codingModel);
        config.FallbackModel.Should().BeSameAs(fallbackModel);
        config.DetectionStrategy.Should().Be(TaskDetectionStrategy.RuleBased);
    }
}
