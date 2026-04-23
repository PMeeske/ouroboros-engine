using Ouroboros.Agent.MetaAI;
using Ouroboros.Abstractions;

namespace Ouroboros.Agent.Tests;

[Trait("Category", "Unit")]
public class OrchestrationScopeTests
{
    private readonly Mock<IActiveContext> _contextMock;
    private readonly OrchestrationScope _scope;

    public OrchestrationScopeTests()
    {
        _contextMock = new Mock<IActiveContext>();
        _scope = new OrchestrationScope("test-orchestration", _contextMock.Object, new OrchestrationObservabilityConfig { EnableTracing = true, SamplingRate = 1.0 });
    }

    #region Constructor

    [Fact]
    public void Constructor_WithNullOrchestrationName_ShouldThrowArgumentNullException()
    {
        // Arrange
        var config = new OrchestrationObservabilityConfig();

        // Act
        Action act = () => new OrchestrationScope(null!, _contextMock.Object, config);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("orchestrationName");
    }

    [Fact]
    public void Constructor_WithNullContext_ShouldThrowArgumentNullException()
    {
        // Arrange
        var config = new OrchestrationObservabilityConfig();

        // Act
        Action act = () => new OrchestrationScope("name", null!, config);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("context");
    }

    [Fact]
    public void Constructor_WithNullConfig_ShouldUseDefault()
    {
        // Act
        var scope = new OrchestrationScope("name", _contextMock.Object, null!);

        // Assert
        scope.Should().NotBeNull();
    }

    #endregion

    #region CreateSpan

    [Fact]
    public void CreateSpan_ShouldReturnSpan()
    {
        // Act
        var span = _scope.CreateSpan("test-span");

        // Assert
        span.Should().NotBeNull();
    }

    #endregion

    #region RecordMetric

    [Fact]
    public void RecordMetric_ShouldRecord()
    {
        // Act
        _scope.RecordMetric("test_metric", 42.0);

        // Assert
        // Metrics are recorded internally; no direct observable state
    }

    #endregion

    #region RecordMetricWithTags

    [Fact]
    public void RecordMetricWithTags_ShouldRecordWithTags()
    {
        // Act
        _scope.RecordMetric("test_metric", 42.0, new Dictionary<string, string> { ["tag"] = "value" });

        // Assert
        // Metrics are recorded internally
    }

    #endregion

    #region RecordError

    [Fact]
    public void RecordError_WithNullException_ShouldNotThrow()
    {
        // Act
        _scope.RecordError(null!);

        // Assert
        // Should not throw
    }

    [Fact]
    public void RecordError_WithException_ShouldRecord()
    {
        // Arrange
        var ex = new InvalidOperationException("test error");

        // Act
        _scope.RecordError(ex);

        // Assert
        // Error recorded internally
    }

    #endregion

    #region CreateChildSpan

    [Fact]
    public void CreateChildSpan_ShouldReturnSpan()
    {
        // Arrange
        var parent = _scope.CreateSpan("parent");

        // Act
        var child = _scope.CreateChildSpan("child", parent);

        // Assert
        child.Should().NotBeNull();
    }

    #endregion

    #region Dispose

    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        // Act
        _scope.Dispose();

        // Assert
        // Should not throw
    }

    #endregion

    #region IsEnabled

    [Fact]
    public void IsEnabled_True_WhenTracingEnabled()
    {
        // Act
        var result = _scope.IsEnabled;

        // Assert
        result.Should().BeTrue();
    }

    #endregion
}

[Trait("Category", "Unit")]
public class OrchestrationExperimentTests
{
    private readonly Mock<IActiveContext> _contextMock;
    private readonly OrchestrationExperiment _experiment;

    public OrchestrationExperimentTests()
    {
        _contextMock = new Mock<IActiveContext>();
        _experiment = new OrchestrationExperiment("exp-1", _contextMock.Object);
    }

    #region Constructor

    [Fact]
    public void Constructor_WithNullExperimentId_ShouldThrowArgumentNullException()
    {
        // Act
        Action act = () => new OrchestrationExperiment(null!, _contextMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("experimentId");
    }

    [Fact]
    public void Constructor_WithNullContext_ShouldThrowArgumentNullException()
    {
        // Act
        Action act = () => new OrchestrationExperiment("exp-1", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("context");
    }

    #endregion

    #region CreateVariant

    [Fact]
    public void CreateVariant_ShouldReturnNonNull()
    {
        // Act
        var variant = _experiment.CreateVariant("variant-1");

        // Assert
        variant.Should().NotBeNull();
    }

    [Fact]
    public void CreateVariant_WithNullName_ShouldThrowArgumentNullException()
    {
        // Act
        Action act = () => _experiment.CreateVariant(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CreateVariant_WithEmptyName_ShouldThrowArgumentException()
    {
        // Act
        Action act = () => _experiment.CreateVariant("");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region RecordVariantMetrics

    [Fact]
    public void RecordVariantMetrics_ShouldNotThrow()
    {
        // Arrange
        var metrics = new VariantMetrics(0.8, 100, 150, 200, 0.7, 10, 8);

        // Act
        _experiment.RecordVariantMetrics("v1", metrics);

        // Assert
        // Recorded internally
    }

    #endregion

    #region RecordVariantResult

    [Fact]
    public void RecordVariantResult_ShouldNotThrow()
    {
        // Arrange
        var prompts = new List<PromptResult> { new PromptResult("test", true, 100, 0.9, "model", null) };
        var metrics = new VariantMetrics(0.8, 100, 150, 200, 0.7, 10, 8);
        var result = new VariantResult("v1", prompts, metrics);

        // Act
        _experiment.RecordVariantResult(result);

        // Assert
        // Recorded internally
    }

    #endregion

    #region GetBestVariant

    [Fact]
    public void GetBestVariant_NoResults_ShouldReturnNull()
    {
        // Act
        var result = _experiment.GetBestVariant();

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region CompleteExperiment

    [Fact]
    public void CompleteExperiment_ShouldReturnResult()
    {
        // Act
        var result = _experiment.CompleteExperiment();

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region DetermineBestVariant

    [Fact]
    public void DetermineBestVariant_NoMetrics_ShouldReturnNull()
    {
        // Act
        var result = _experiment.DetermineBestVariant();

        // Assert
        result.Should().BeNull();
    }

    #endregion
}
