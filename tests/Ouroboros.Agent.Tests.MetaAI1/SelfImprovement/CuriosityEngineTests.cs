using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Agent.Tests.SelfImprovement;

[Trait("Category", "Unit")]
public class CuriosityEngineTests
{
    #region Constructor

    [Fact]
    public void Constructor_ValidArgs_ShouldInitialize()
    {
        var mockLlm = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        var mockMemory = new Mock<IMemoryStore>();
        var mockSkills = new Mock<ISkillRegistry>();
        var mockSafety = new Mock<ISafetyGuard>();
        var mockEthics = new Mock<Core.Ethics.IEthicsFramework>();

        var engine = new CuriosityEngine(mockLlm.Object, mockMemory.Object, mockSkills.Object, mockSafety.Object, mockEthics.Object);
        engine.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_NullLLM_ShouldThrow()
    {
        var mockMemory = new Mock<IMemoryStore>();
        var mockSkills = new Mock<ISkillRegistry>();
        var mockSafety = new Mock<ISafetyGuard>();
        var mockEthics = new Mock<Core.Ethics.IEthicsFramework>();

        Action act = () => new CuriosityEngine(null!, mockMemory.Object, mockSkills.Object, mockSafety.Object, mockEthics.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("llm");
    }

    [Fact]
    public void Constructor_NullMemory_ShouldThrow()
    {
        var mockLlm = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        var mockSkills = new Mock<ISkillRegistry>();
        var mockSafety = new Mock<ISafetyGuard>();
        var mockEthics = new Mock<Core.Ethics.IEthicsFramework>();

        Action act = () => new CuriosityEngine(mockLlm.Object, null!, mockSkills.Object, mockSafety.Object, mockEthics.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("memory");
    }

    [Fact]
    public void Constructor_NullSkills_ShouldThrow()
    {
        var mockLlm = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        var mockMemory = new Mock<IMemoryStore>();
        var mockSafety = new Mock<ISafetyGuard>();
        var mockEthics = new Mock<Core.Ethics.IEthicsFramework>();

        Action act = () => new CuriosityEngine(mockLlm.Object, mockMemory.Object, null!, mockSafety.Object, mockEthics.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("skills");
    }

    [Fact]
    public void Constructor_NullSafety_ShouldThrow()
    {
        var mockLlm = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        var mockMemory = new Mock<IMemoryStore>();
        var mockSkills = new Mock<ISkillRegistry>();
        var mockEthics = new Mock<Core.Ethics.IEthicsFramework>();

        Action act = () => new CuriosityEngine(mockLlm.Object, mockMemory.Object, mockSkills.Object, null!, mockEthics.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("safety");
    }

    [Fact]
    public void Constructor_NullEthics_ShouldThrow()
    {
        var mockLlm = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        var mockMemory = new Mock<IMemoryStore>();
        var mockSkills = new Mock<ISkillRegistry>();
        var mockSafety = new Mock<ISafetyGuard>();

        Action act = () => new CuriosityEngine(mockLlm.Object, mockMemory.Object, mockSkills.Object, mockSafety.Object, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("ethics");
    }

    [Fact]
    public void Constructor_NullConfig_ShouldUseDefault()
    {
        var mockLlm = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        var mockMemory = new Mock<IMemoryStore>();
        var mockSkills = new Mock<ISkillRegistry>();
        var mockSafety = new Mock<ISafetyGuard>();
        var mockEthics = new Mock<Core.Ethics.IEthicsFramework>();

        var engine = new CuriosityEngine(mockLlm.Object, mockMemory.Object, mockSkills.Object, mockSafety.Object, mockEthics.Object, null);
        engine.Should().NotBeNull();
    }

    #endregion
}
