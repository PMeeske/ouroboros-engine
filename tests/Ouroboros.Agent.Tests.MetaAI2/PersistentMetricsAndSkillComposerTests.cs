using Ouroboros.Agent.MetaAI;
using Ouroboros.Abstractions;

namespace Ouroboros.Agent.Tests;

[Trait("Category", "Unit")]
public class PersistentMetricsStoreTests
{
    private readonly string _testPath;
    private readonly PersistentMetricsConfig _config;

    public PersistentMetricsStoreTests()
    {
        _testPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _config = new PersistentMetricsConfig { StoragePath = _testPath, FileName = "test_metrics.json", AutoSave = false };
    }

    #region Constructor

    [Fact]
    public void Constructor_WithNullConfig_ShouldThrowArgumentNullException()
    {
        // Act
        Action act = () => new PersistentMetricsStore(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("config");
    }

    [Fact]
    public void Constructor_WithValidConfig_ShouldInitialize()
    {
        // Act
        var store = new PersistentMetricsStore(_config);

        // Assert
        store.Should().NotBeNull();
    }

    #endregion

    #region RecordMetric

    [Fact]
    public void RecordMetric_ShouldRecord()
    {
        // Arrange
        var store = new PersistentMetricsStore(_config);

        // Act
        store.RecordMetric("test_metric", 42.0);

        // Assert
        var metrics = store.GetMetrics();
        metrics.Should().ContainKey("test_metric");
    }

    #endregion

    #region RecordExecutionMetric

    [Fact]
    public void RecordExecutionMetric_ShouldRecord()
    {
        // Arrange
        var store = new PersistentMetricsStore(_config);

        // Act
        store.RecordExecutionMetric("action", 100.0, true, 0.9);

        // Assert
        var metrics = store.GetMetrics();
        metrics.Should().ContainKey("execution:action");
    }

    #endregion

    #region GetMetrics

    [Fact]
    public void GetMetrics_EmptyStore_ShouldReturnEmpty()
    {
        // Arrange
        var store = new PersistentMetricsStore(_config);

        // Act
        var result = store.GetMetrics();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetMetrics_WithMetrics_ShouldReturnAll()
    {
        // Arrange
        var store = new PersistentMetricsStore(_config);
        store.RecordMetric("metric1", 1.0);
        store.RecordMetric("metric2", 2.0);

        // Act
        var result = store.GetMetrics();

        // Assert
        result.Should().HaveCount(2);
    }

    #endregion

    #region GetMetricHistory

    [Fact]
    public void GetMetricHistory_NonExistentMetric_ShouldReturnEmpty()
    {
        // Arrange
        var store = new PersistentMetricsStore(_config);

        // Act
        var result = store.GetMetricHistory("non-existent");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetMetricHistory_ExistingMetric_ShouldReturnHistory()
    {
        // Arrange
        var store = new PersistentMetricsStore(_config);
        store.RecordMetric("metric1", 1.0);
        store.RecordMetric("metric1", 2.0);

        // Act
        var result = store.GetMetricHistory("metric1");

        // Assert
        result.Should().HaveCount(2);
    }

    #endregion

    #region SaveAsync

    [Fact]
    public async Task SaveAsync_ShouldCreateFile()
    {
        // Arrange
        var store = new PersistentMetricsStore(_config);
        store.RecordMetric("metric1", 1.0);
        Directory.CreateDirectory(_testPath);

        // Act
        await store.SaveAsync();

        // Assert
        var filePath = Path.Combine(_testPath, _config.FileName);
        File.Exists(filePath).Should().BeTrue();
    }

    #endregion

    #region LoadAsync

    [Fact]
    public async Task LoadAsync_ExistingFile_ShouldLoad()
    {
        // Arrange
        var store = new PersistentMetricsStore(_config);
        store.RecordMetric("metric1", 1.0);
        Directory.CreateDirectory(_testPath);
        await store.SaveAsync();

        // Act
        var newStore = new PersistentMetricsStore(_config);
        await newStore.LoadAsync();

        // Assert
        var metrics = newStore.GetMetrics();
        metrics.Should().ContainKey("metric1");
    }

    [Fact]
    public async Task LoadAsync_NonExistentFile_ShouldNotThrow()
    {
        // Arrange
        var store = new PersistentMetricsStore(_config);

        // Act
        await store.LoadAsync();

        // Assert
        // Should not throw
    }

    #endregion

    #region GetAverageMetric

    [Fact]
    public void GetAverageMetric_NonExistent_ShouldReturnZero()
    {
        // Arrange
        var store = new PersistentMetricsStore(_config);

        // Act
        var result = store.GetAverageMetric("non-existent");

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void GetAverageMetric_ExistingMetric_ShouldReturnAverage()
    {
        // Arrange
        var store = new PersistentMetricsStore(_config);
        store.RecordMetric("metric1", 10.0);
        store.RecordMetric("metric1", 20.0);

        // Act
        var result = store.GetAverageMetric("metric1");

        // Assert
        result.Should().Be(15.0);
    }

    #endregion

    #region GetMetricsForAction

    [Fact]
    public void GetMetricsForAction_NoMatches_ShouldReturnEmpty()
    {
        // Arrange
        var store = new PersistentMetricsStore(_config);

        // Act
        var result = store.GetMetricsForAction("action1");

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetPerformanceSummary

    [Fact]
    public void GetPerformanceSummary_ShouldReturnSummary()
    {
        // Arrange
        var store = new PersistentMetricsStore(_config);
        store.RecordExecutionMetric("action1", 100.0, true, 0.9);

        // Act
        var result = store.GetPerformanceSummary();

        // Assert
        result.Should().NotBeNull();
    }

    #endregion
}

[Trait("Category", "Unit")]
public class SkillComposerTests
{
    #region ComposeSkill

    [Fact]
    public void ComposeSkill_WithEmptyComponents_ShouldThrow()
    {
        // Arrange
        var composer = new SkillComposer();

        // Act
        Action act = () => composer.ComposeSkill("composed", new List<Skill>());

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ComposeSkill_WithNullName_ShouldThrow()
    {
        // Arrange
        var composer = new SkillComposer();
        var components = new List<Skill> { new Skill("s1", "desc", new List<string>(), new List<PlanStep>(), 0.8, 0, DateTime.UtcNow, DateTime.UtcNow) };

        // Act
        Action act = () => composer.ComposeSkill(null!, components);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ComposeSkill_WithSingleComponent_ShouldReturnSkill()
    {
        // Arrange
        var composer = new SkillComposer();
        var step = new PlanStep("action", new Dictionary<string, object>(), "outcome", 0.8);
        var component = new Skill("s1", "desc", new List<string> { "tag1" }, new List<PlanStep> { step }, 0.8, 5, DateTime.UtcNow, DateTime.UtcNow);
        var components = new List<Skill> { component };

        // Act
        var result = composer.ComposeSkill("composed", components);

        // Assert
        result.Name.Should().Be("composed");
        result.Plan.Should().ContainSingle();
        result.Tags.Should().Contain("tag1");
    }

    [Fact]
    public void ComposeSkill_WithMultipleComponents_ShouldMergeStepsAndTags()
    {
        // Arrange
        var composer = new SkillComposer();
        var step1 = new PlanStep("action1", new Dictionary<string, object>(), "outcome1", 0.8);
        var step2 = new PlanStep("action2", new Dictionary<string, object>(), "outcome2", 0.9);
        var c1 = new Skill("s1", "desc", new List<string> { "tag1" }, new List<PlanStep> { step1 }, 0.9, 10, DateTime.UtcNow, DateTime.UtcNow);
        var c2 = new Skill("s2", "desc", new List<string> { "tag2" }, new List<PlanStep> { step2 }, 0.8, 5, DateTime.UtcNow, DateTime.UtcNow);
        var components = new List<Skill> { c1, c2 };

        // Act
        var result = composer.ComposeSkill("composed", components);

        // Assert
        result.Plan.Should().HaveCount(2);
        result.Tags.Should().Contain("tag1", "tag2");
        result.SuccessRate.Should().BeApproximately(0.85, 0.01);
        result.UsageCount.Should().Be(15);
    }

    [Fact]
    public void ComposeSkill_WithRecursiveAllowed_ShouldComposeRecursively()
    {
        // Arrange
        var composer = new SkillComposer(new SkillCompositionConfig { AllowRecursiveComposition = true });
        var step = new PlanStep("action", new Dictionary<string, object>(), "outcome", 0.8);
        var c1 = new Skill("s1", "desc", new List<string>(), new List<PlanStep> { step }, 0.8, 0, DateTime.UtcNow, DateTime.UtcNow);

        // Act
        var result = composer.ComposeSkill("composed", new List<Skill> { c1 });

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public void ComposeSkill_DuplicateSteps_ShouldNotIncludeDuplicates()
    {
        // Arrange
        var composer = new SkillComposer();
        var step = new PlanStep("same_action", new Dictionary<string, object>(), "outcome", 0.8);
        var c1 = new Skill("s1", "desc", new List<string>(), new List<PlanStep> { step }, 0.8, 0, DateTime.UtcNow, DateTime.UtcNow);
        var c2 = new Skill("s2", "desc", new List<string>(), new List<PlanStep> { step }, 0.9, 0, DateTime.UtcNow, DateTime.UtcNow);

        // Act
        var result = composer.ComposeSkill("composed", new List<Skill> { c1, c2 });

        // Assert
        result.Plan.Should().ContainSingle();
    }

    #endregion

    #region DecomposeSkill

    [Fact]
    public void DecomposeSkill_WithNullSkill_ShouldThrow()
    {
        // Arrange
        var composer = new SkillComposer();

        // Act
        Action act = () => composer.DecomposeSkill(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void DecomposeSkill_EmptyPlan_ShouldReturnEmpty()
    {
        // Arrange
        var composer = new SkillComposer();
        var skill = new Skill("s1", "desc", new List<string>(), new List<PlanStep>(), 0.8, 0, DateTime.UtcNow, DateTime.UtcNow);

        // Act
        var result = composer.DecomposeSkill(skill);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void DecomposeSkill_WithPlan_ShouldReturnSubSkills()
    {
        // Arrange
        var composer = new SkillComposer();
        var step1 = new PlanStep("action1", new Dictionary<string, object>(), "outcome1", 0.8);
        var step2 = new PlanStep("action2", new Dictionary<string, object>(), "outcome2", 0.9);
        var skill = new Skill("s1", "desc", new List<string> { "tag1" }, new List<PlanStep> { step1, step2 }, 0.8, 10, DateTime.UtcNow, DateTime.UtcNow);

        // Act
        var result = composer.DecomposeSkill(skill);

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Contain("Step1");
        result[0].Plan.Should().ContainSingle();
    }

    #endregion
}
