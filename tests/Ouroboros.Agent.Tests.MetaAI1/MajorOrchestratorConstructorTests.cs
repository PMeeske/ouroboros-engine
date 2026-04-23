using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Agent.Tests;

[Trait("Category", "Unit")]
public class MajorOrchestratorConstructorTests
{
    #region AdaptivePlanner

    [Fact]
    public void AdaptivePlanner_Constructor_ValidArgs_ShouldInitialize()
    {
        var mockOrchestrator = new Mock<IMetaAIPlannerOrchestrator>();
        var mockLlm = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();

        var planner = new AdaptivePlanner(mockOrchestrator.Object, mockLlm.Object);
        planner.Should().NotBeNull();
    }

    [Fact]
    public void AdaptivePlanner_Constructor_NullOrchestrator_ShouldThrow()
    {
        var mockLlm = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        Action act = () => new AdaptivePlanner(null!, mockLlm.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("orchestrator");
    }

    [Fact]
    public void AdaptivePlanner_Constructor_NullLLM_ShouldThrow()
    {
        var mockOrchestrator = new Mock<IMetaAIPlannerOrchestrator>();
        Action act = () => new AdaptivePlanner(mockOrchestrator.Object, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("llm");
    }

    #endregion

    #region DistributedOrchestrator

    [Fact]
    public void DistributedOrchestrator_Constructor_ValidArgs_ShouldInitialize()
    {
        var mockSafety = new Mock<ISafetyGuard>();
        var orchestrator = new DistributedOrchestrator(mockSafety.Object);
        orchestrator.Should().NotBeNull();
    }

    [Fact]
    public void DistributedOrchestrator_Constructor_NullSafety_ShouldThrow()
    {
        Action act = () => new DistributedOrchestrator(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("safety");
    }

    [Fact]
    public void DistributedOrchestrator_Constructor_NullConfig_ShouldUseDefault()
    {
        var mockSafety = new Mock<ISafetyGuard>();
        var orchestrator = new DistributedOrchestrator(mockSafety.Object, null);
        orchestrator.Should().NotBeNull();
    }

    #endregion
}
