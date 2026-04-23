using Ouroboros.Agent.MetaAI.MetaLearning;

namespace Ouroboros.Agent.Tests.MetaLearning;

[Trait("Category", "Unit")]
public class MetaLearningTests
{
    #region MetaLearnerConfig

    [Fact]
    public void MetaLearnerConfig_DefaultCreation_ShouldUseDefaults()
    {
        var config = new MetaLearnerConfig();

        config.MinEpisodesForOptimization.Should().Be(10);
        config.MaxFewShotExamples.Should().Be(5);
        config.MinConfidenceThreshold.Should().Be(0.6);
        config.DefaultEvaluationWindow.Should().Be(TimeSpan.FromDays(30));
    }

    [Fact]
    public void MetaLearnerConfig_CustomCreation_ShouldSetValues()
    {
        var config = new MetaLearnerConfig(20, 10, 0.8, TimeSpan.FromDays(7));

        config.MinEpisodesForOptimization.Should().Be(20);
        config.MaxFewShotExamples.Should().Be(10);
        config.MinConfidenceThreshold.Should().Be(0.8);
        config.DefaultEvaluationWindow.Should().Be(TimeSpan.FromDays(7));
    }

    #endregion

    #region MetaLearner Constructor

    [Fact]
    public void MetaLearner_Constructor_ValidArgs_ShouldInitialize()
    {
        var mockLlm = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        var mockRegistry = new Mock<ISkillRegistry>();
        var mockMemory = new Mock<IMemoryStore>();
        var config = new MetaLearnerConfig();

        var learner = new MetaLearner(mockLlm.Object, mockRegistry.Object, mockMemory.Object, config);
        learner.Should().NotBeNull();
    }

    [Fact]
    public void MetaLearner_Constructor_NullLlm_ShouldThrow()
    {
        var mockRegistry = new Mock<ISkillRegistry>();
        var mockMemory = new Mock<IMemoryStore>();

        Action act = () => new MetaLearner(null!, mockRegistry.Object, mockMemory.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MetaLearner_Constructor_NullSkillRegistry_ShouldThrow()
    {
        var mockLlm = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        var mockMemory = new Mock<IMemoryStore>();

        Action act = () => new MetaLearner(mockLlm.Object, null!, mockMemory.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MetaLearner_Constructor_NullMemory_ShouldThrow()
    {
        var mockLlm = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        var mockRegistry = new Mock<ISkillRegistry>();

        Action act = () => new MetaLearner(mockLlm.Object, mockRegistry.Object, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MetaLearner_Constructor_NullConfig_ShouldUseDefault()
    {
        var mockLlm = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        var mockRegistry = new Mock<ISkillRegistry>();
        var mockMemory = new Mock<IMemoryStore>();

        var learner = new MetaLearner(mockLlm.Object, mockRegistry.Object, mockMemory.Object, null!);
        learner.Should().NotBeNull();
    }

    #endregion
}
