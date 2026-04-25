using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Agent.Tests.SelfImprovement;

[Trait("Category", "Unit")]
public class SelfEvaluatorTests
{
    #region Constructor

    [Fact]
    public void Constructor_ValidArgs_ShouldInitialize()
    {
        var mockLlm = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        var mockCapReg = new Mock<ICapabilityRegistry>();
        var mockSkills = new Mock<ISkillRegistry>();
        var mockMemory = new Mock<IMemoryStore>();
        var mockOrchestrator = new Mock<IMetaAIPlannerOrchestrator>();

        var evaluator = new SelfEvaluator(mockLlm.Object, mockCapReg.Object, mockSkills.Object, mockMemory.Object, mockOrchestrator.Object);
        evaluator.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_NullLLM_ShouldThrow()
    {
        var mockCapReg = new Mock<ICapabilityRegistry>();
        var mockSkills = new Mock<ISkillRegistry>();
        var mockMemory = new Mock<IMemoryStore>();
        var mockOrchestrator = new Mock<IMetaAIPlannerOrchestrator>();

        Action act = () => new SelfEvaluator(null!, mockCapReg.Object, mockSkills.Object, mockMemory.Object, mockOrchestrator.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("llm");
    }

    [Fact]
    public void Constructor_NullCapabilities_ShouldThrow()
    {
        var mockLlm = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        var mockSkills = new Mock<ISkillRegistry>();
        var mockMemory = new Mock<IMemoryStore>();
        var mockOrchestrator = new Mock<IMetaAIPlannerOrchestrator>();

        Action act = () => new SelfEvaluator(mockLlm.Object, null!, mockSkills.Object, mockMemory.Object, mockOrchestrator.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("capabilities");
    }

    [Fact]
    public void Constructor_NullSkills_ShouldThrow()
    {
        var mockLlm = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        var mockCapReg = new Mock<ICapabilityRegistry>();
        var mockMemory = new Mock<IMemoryStore>();
        var mockOrchestrator = new Mock<IMetaAIPlannerOrchestrator>();

        Action act = () => new SelfEvaluator(mockLlm.Object, mockCapReg.Object, null!, mockMemory.Object, mockOrchestrator.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("skills");
    }

    [Fact]
    public void Constructor_NullMemory_ShouldThrow()
    {
        var mockLlm = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        var mockCapReg = new Mock<ICapabilityRegistry>();
        var mockSkills = new Mock<ISkillRegistry>();
        var mockOrchestrator = new Mock<IMetaAIPlannerOrchestrator>();

        Action act = () => new SelfEvaluator(mockLlm.Object, mockCapReg.Object, mockSkills.Object, null!, mockOrchestrator.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("memory");
    }

    [Fact]
    public void Constructor_NullOrchestrator_ShouldThrow()
    {
        var mockLlm = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        var mockCapReg = new Mock<ICapabilityRegistry>();
        var mockSkills = new Mock<ISkillRegistry>();
        var mockMemory = new Mock<IMemoryStore>();

        Action act = () => new SelfEvaluator(mockLlm.Object, mockCapReg.Object, mockSkills.Object, mockMemory.Object, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("orchestrator");
    }

    [Fact]
    public void Constructor_NullConfig_ShouldUseDefault()
    {
        var mockLlm = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        var mockCapReg = new Mock<ICapabilityRegistry>();
        var mockSkills = new Mock<ISkillRegistry>();
        var mockMemory = new Mock<IMemoryStore>();
        var mockOrchestrator = new Mock<IMetaAIPlannerOrchestrator>();

        var evaluator = new SelfEvaluator(mockLlm.Object, mockCapReg.Object, mockSkills.Object, mockMemory.Object, mockOrchestrator.Object, null);
        evaluator.Should().NotBeNull();
    }

    #endregion
}
