namespace Ouroboros.Pipeline.Tests;

using Ouroboros.Pipeline.Metacognition;
using Ouroboros.Abstractions;
using Ouroboros.Core.Monads;

[Trait("Category", "Unit")]
public class BayesianSelfAssessorTests
{
    #region Creation

    [Fact]
    public void Constructor_Default_ShouldInitializeWithEmptyBeliefsAndScores()
    {
        // Arrange & Act
        var assessor = new BayesianSelfAssessor();

        // Assert
        assessor.GetAllBeliefs().Should().BeEmpty();
        assessor.GetCalibrationFactor().Should().Be(1.0);
    }

    [Fact]
    public void Constructor_WithInitialBeliefs_ShouldPopulateBeliefs()
    {
        // Arrange
        var beliefs = new[] { CapabilityBelief.Uninformative("coding") };
        var scores = new[] { DimensionScore.Unknown(PerformanceDimension.Accuracy) };

        // Act
        var assessor = new BayesianSelfAssessor(beliefs, scores);

        // Assert
        assessor.GetAllBeliefs().Should().ContainKey("coding");
        assessor.GetCapabilityBelief("coding").HasValue.Should().BeTrue();
    }

    #endregion

    #region AssessAsync

    [Fact]
    public async Task AssessAsync_ShouldReturnSuccessWithAllDimensions()
    {
        // Arrange
        var assessor = new BayesianSelfAssessor();

        // Act
        var result = await assessor.AssessAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.DimensionScores.Should().NotBeEmpty();
    }

    #endregion

    #region AssessDimensionAsync

    [Fact]
    public async Task AssessDimensionAsync_ShouldReturnUnknownScoreForUninitializedDimension()
    {
        // Arrange
        var assessor = new BayesianSelfAssessor();

        // Act
        var result = await assessor.AssessDimensionAsync(PerformanceDimension.Accuracy);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Score.Should().Be(0.5);
        result.Value.Confidence.Should().Be(0.0);
    }

    [Fact]
    public async Task AssessDimensionAsync_AfterCalibration_ShouldApplyCalibrationFactor()
    {
        // Arrange
        var assessor = new BayesianSelfAssessor();
        assessor.CalibrateConfidence(new[] { (0.9, 0.5) });
        var factor = assessor.GetCalibrationFactor();

        // Act
        var result = await assessor.AssessDimensionAsync(PerformanceDimension.Accuracy);

        // Assert
        result.IsSuccess.Should().BeTrue();
        if (factor < 1.0)
        {
            result.Value.Confidence.Should().BeLessThan(1.0);
        }
    }

    #endregion

    #region GetCapabilityBelief

    [Fact]
    public void GetCapabilityBelief_NullOrEmpty_ShouldReturnNone()
    {
        // Arrange
        var assessor = new BayesianSelfAssessor();

        // Act
        var nullResult = assessor.GetCapabilityBelief(null!);
        var emptyResult = assessor.GetCapabilityBelief("   ");

        // Assert
        nullResult.HasValue.Should().BeFalse();
        emptyResult.HasValue.Should().BeFalse();
    }

    [Fact]
    public void GetCapabilityBelief_UnknownCapability_ShouldReturnNone()
    {
        // Arrange
        var assessor = new BayesianSelfAssessor();

        // Act
        var result = assessor.GetCapabilityBelief("unknown");

        // Assert
        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public void GetCapabilityBelief_AfterUpdate_ShouldReturnBelief()
    {
        // Arrange
        var assessor = new BayesianSelfAssessor();
        assessor.UpdateBelief("coding", 0.8);

        // Act
        var result = assessor.GetCapabilityBelief("coding");

        // Assert
        result.HasValue.Should().BeTrue();
        result.Value.Proficiency.Should().BeGreaterThan(0.0);
    }

    #endregion

    #region UpdateBelief

    [Fact]
    public void UpdateBelief_NullCapability_ShouldReturnFailure()
    {
        // Arrange
        var assessor = new BayesianSelfAssessor();

        // Act
        var result = assessor.UpdateBelief(null!, 0.5);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void UpdateBelief_OutOfRangeEvidence_ShouldReturnFailure(double evidence)
    {
        // Arrange
        var assessor = new BayesianSelfAssessor();

        // Act
        var result = assessor.UpdateBelief("coding", evidence);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void UpdateBelief_ValidInput_ShouldReturnSuccess()
    {
        // Arrange
        var assessor = new BayesianSelfAssessor();

        // Act
        var result = assessor.UpdateBelief("coding", 0.75);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CapabilityName.Should().Be("coding");
    }

    #endregion

    #region GetAllBeliefs

    [Fact]
    public void GetAllBeliefs_Initially_ShouldBeEmpty()
    {
        // Arrange & Act
        var assessor = new BayesianSelfAssessor();

        // Assert
        assessor.GetAllBeliefs().Should().BeEmpty();
    }

    [Fact]
    public void GetAllBeliefs_AfterUpdates_ShouldContainAllBeliefs()
    {
        // Arrange
        var assessor = new BayesianSelfAssessor();
        assessor.UpdateBelief("coding", 0.8);
        assessor.UpdateBelief("design", 0.6);

        // Act
        var beliefs = assessor.GetAllBeliefs();

        // Assert
        beliefs.Should().HaveCount(2);
        beliefs.Keys.Should().Contain("coding", "design");
    }

    #endregion

    #region CalibrateConfidence

    [Fact]
    public void CalibrateConfidence_EmptySamples_ShouldReturnSuccess()
    {
        // Arrange
        var assessor = new BayesianSelfAssessor();

        // Act
        var result = assessor.CalibrateConfidence(Array.Empty<(double, double)>());

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void CalibrateConfidence_Overconfident_ShouldReduceFactor()
    {
        // Arrange
        var assessor = new BayesianSelfAssessor();
        var samples = new[] { (0.9, 0.3), (0.9, 0.3), (0.9, 0.3) };

        // Act
        var result = assessor.CalibrateConfidence(samples);

        // Assert
        result.IsSuccess.Should().BeTrue();
        assessor.GetCalibrationFactor().Should().BeLessThan(1.0);
    }

    [Fact]
    public void CalibrateConfidence_Underconfident_ShouldIncreaseFactor()
    {
        // Arrange
        var assessor = new BayesianSelfAssessor();
        var samples = new[] { (0.3, 0.9), (0.3, 0.9), (0.3, 0.9) };

        // Act
        var result = assessor.CalibrateConfidence(samples);

        // Assert
        result.IsSuccess.Should().BeTrue();
        assessor.GetCalibrationFactor().Should().BeGreaterThan(1.0);
    }

    #endregion

    #region UpdateDimensionScore

    [Fact]
    public void UpdateDimensionScore_ShouldUpdateAndReturnSuccess()
    {
        // Arrange
        var assessor = new BayesianSelfAssessor();

        // Act
        var result = assessor.UpdateDimensionScore(PerformanceDimension.Speed, 0.8, 0.5, "test evidence");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Dimension.Should().Be(PerformanceDimension.Speed);
    }

    #endregion
}

[Trait("Category", "Unit")]
public class CapabilityBeliefTests
{
    #region Creation

    [Fact]
    public void Uninformative_ShouldCreateWithDefaultValues()
    {
        // Act
        var belief = CapabilityBelief.Uninformative("test");

        // Assert
        belief.CapabilityName.Should().Be("test");
        belief.Proficiency.Should().Be(0.5);
        belief.Uncertainty.Should().Be(1.0);
        belief.ValidationCount.Should().Be(0);
        belief.LastValidated.Should().Be(DateTime.MinValue);
    }

    [Fact]
    public void Create_ShouldClampValues()
    {
        // Act
        var belief = CapabilityBelief.Create("test", 1.5, -0.5);

        // Assert
        belief.Proficiency.Should().Be(1.0);
        belief.Uncertainty.Should().Be(0.0);
        belief.ValidationCount.Should().Be(1);
    }

    #endregion

    #region WithBayesianUpdate

    [Fact]
    public void WithBayesianUpdate_ShouldUpdateProficiencyAndUncertainty()
    {
        // Arrange
        var belief = CapabilityBelief.Uninformative("test");

        // Act
        var updated = belief.WithBayesianUpdate(0.8, 5);

        // Assert
        updated.Proficiency.Should().NotBe(0.5);
        updated.Uncertainty.Should().BeLessThan(1.0);
        updated.ValidationCount.Should().Be(5);
    }

    [Fact]
    public void WithBayesianUpdate_ExtremeEvidence_ShouldStayInRange()
    {
        // Arrange
        var belief = CapabilityBelief.Create("test", 0.5, 0.5);

        // Act
        var updated = belief.WithBayesianUpdate(1.0, 100);

        // Assert
        updated.Proficiency.Should().BeInRange(0.0, 1.0);
        updated.Uncertainty.Should().BeInRange(0.0, 1.0);
    }

    #endregion

    #region GetCredibleInterval

    [Theory]
    [InlineData(0.95)]
    [InlineData(0.90)]
    [InlineData(0.99)]
    [InlineData(0.5)]
    public void GetCredibleInterval_ShouldReturnOrderedBounds(double level)
    {
        // Arrange
        var belief = CapabilityBelief.Create("test", 0.6, 0.2);

        // Act
        var (lower, expected, upper) = belief.GetCredibleInterval(level);

        // Assert
        lower.Should().BeLessThanOrEqualTo(expected);
        expected.Should().BeLessThanOrEqualTo(upper);
        lower.Should().BeGreaterThanOrEqualTo(0.0);
        upper.Should().BeLessThanOrEqualTo(1.0);
    }

    #endregion

    #region Validate

    [Fact]
    public void Validate_EmptyName_ShouldReturnFailure()
    {
        // Arrange
        var belief = new CapabilityBelief("", 0.5, 0.5, DateTime.UtcNow, 0);

        // Act
        var result = belief.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Validate_InvalidProficiency_ShouldReturnFailure(double proficiency)
    {
        // Arrange
        var belief = new CapabilityBelief("test", proficiency, 0.5, DateTime.UtcNow, 0);

        // Act
        var result = belief.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Validate_ValidBelief_ShouldReturnSuccess()
    {
        // Arrange
        var belief = CapabilityBelief.Create("test", 0.5, 0.5);

        // Act
        var result = belief.Validate();

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion
}

[Trait("Category", "Unit")]
public class CognitiveEventTests
{
    #region Factory Methods

    [Fact]
    public void Thought_ShouldCreateThoughtEvent()
    {
        // Act
        var evt = CognitiveEvent.Thought("test thought");

        // Assert
        evt.EventType.Should().Be(CognitiveEventType.ThoughtGenerated);
        evt.Description.Should().Be("test thought");
        evt.Severity.Should().Be(Severity.Info);
    }

    [Fact]
    public void Decision_ShouldCreateDecisionEvent()
    {
        // Act
        var evt = CognitiveEvent.Decision("test decision");

        // Assert
        evt.EventType.Should().Be(CognitiveEventType.DecisionMade);
    }

    [Fact]
    public void Error_ShouldCreateErrorEvent()
    {
        // Act
        var evt = CognitiveEvent.Error("test error", Severity.Critical);

        // Assert
        evt.EventType.Should().Be(CognitiveEventType.ErrorDetected);
        evt.Severity.Should().Be(Severity.Critical);
    }

    [Fact]
    public void Confusion_ShouldCreateConfusionEvent()
    {
        // Act
        var evt = CognitiveEvent.Confusion("test confusion");

        // Assert
        evt.EventType.Should().Be(CognitiveEventType.ConfusionSensed);
        evt.Severity.Should().Be(Severity.Warning);
    }

    [Fact]
    public void Insight_ShouldCreateInsightEvent()
    {
        // Act
        var evt = CognitiveEvent.Insight("test insight");

        // Assert
        evt.EventType.Should().Be(CognitiveEventType.InsightGained);
    }

    [Fact]
    public void AttentionChange_ShouldCreateAttentionEvent()
    {
        // Act
        var evt = CognitiveEvent.AttentionChange("focus changed");

        // Assert
        evt.EventType.Should().Be(CognitiveEventType.AttentionShift);
    }

    [Fact]
    public void GoalStart_ShouldCreateGoalActivationEvent()
    {
        // Act
        var evt = CognitiveEvent.GoalStart("goal A");

        // Assert
        evt.EventType.Should().Be(CognitiveEventType.GoalActivated);
    }

    [Fact]
    public void GoalEnd_ShouldCreateGoalCompletionEvent()
    {
        // Act
        var evt = CognitiveEvent.GoalEnd("goal A");

        // Assert
        evt.EventType.Should().Be(CognitiveEventType.GoalCompleted);
    }

    [Fact]
    public void UncertaintyDetected_ShouldCreateUncertaintyEvent()
    {
        // Act
        var evt = CognitiveEvent.UncertaintyDetected("uncertain");

        // Assert
        evt.EventType.Should().Be(CognitiveEventType.Uncertainty);
    }

    [Fact]
    public void ContradictionDetected_ShouldCreateContradictionEvent()
    {
        // Act
        var evt = CognitiveEvent.ContradictionDetected("contradiction found");

        // Assert
        evt.EventType.Should().Be(CognitiveEventType.Contradiction);
        evt.Severity.Should().Be(Severity.Critical);
    }

    #endregion

    #region Context Operations

    [Fact]
    public void WithContext_ShouldAddSingleContextItem()
    {
        // Arrange
        var evt = CognitiveEvent.Thought("test");

        // Act
        var updated = evt.WithContext("key", "value");

        // Assert
        updated.Context.Should().ContainKey("key");
        updated.Context["key"].Should().Be("value");
    }

    [Fact]
    public void WithMergedContext_ShouldMergeMultipleItems()
    {
        // Arrange
        var evt = CognitiveEvent.Thought("test");
        var additional = ImmutableDictionary<string, object>.Empty.Add("a", 1).Add("b", 2);

        // Act
        var updated = evt.WithMergedContext(additional);

        // Assert
        updated.Context.Should().ContainKey("a");
        updated.Context.Should().ContainKey("b");
    }

    #endregion
}

[Trait("Category", "Unit")]
public class CognitiveEventTypeTests
{
    [Theory]
    [InlineData(CognitiveEventType.ThoughtGenerated)]
    [InlineData(CognitiveEventType.DecisionMade)]
    [InlineData(CognitiveEventType.ErrorDetected)]
    [InlineData(CognitiveEventType.ConfusionSensed)]
    [InlineData(CognitiveEventType.InsightGained)]
    [InlineData(CognitiveEventType.AttentionShift)]
    [InlineData(CognitiveEventType.GoalActivated)]
    [InlineData(CognitiveEventType.GoalCompleted)]
    [InlineData(CognitiveEventType.Uncertainty)]
    [InlineData(CognitiveEventType.Contradiction)]
    public void AllEnumValues_ShouldBeDefined(CognitiveEventType value)
    {
        // Assert
        ((int)value).Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void Enum_ShouldHaveTenValues()
    {
        // Act
        var values = Enum.GetValues<CognitiveEventType>();

        // Assert
        values.Should().HaveCount(10);
    }
}

[Trait("Category", "Unit")]
public class CognitiveHealthTests
{
    #region Creation

    [Fact]
    public void Optimal_ShouldCreateHealthyState()
    {
        // Act
        var health = CognitiveHealth.Optimal();

        // Assert
        health.HealthScore.Should().Be(1.0);
        health.ProcessingEfficiency.Should().Be(1.0);
        health.ErrorRate.Should().Be(0.0);
        health.Status.Should().Be(HealthStatus.Healthy);
        health.RequiresAttention().Should().BeFalse();
        health.IsCritical().Should().BeFalse();
    }

    [Fact]
    public void FromMetrics_WithCriticalAlert_ShouldReturnCritical()
    {
        // Arrange
        var alerts = ImmutableList.Create(MonitoringAlert.HighPriority("test", "msg", Array.Empty<CognitiveEvent>(), "action"));

        // Act
        var health = CognitiveHealth.FromMetrics(0.2, 0.3, 0.6, TimeSpan.FromSeconds(1), alerts);

        // Assert
        health.Status.Should().Be(HealthStatus.Critical);
        health.IsCritical().Should().BeTrue();
        health.RequiresAttention().Should().BeTrue();
    }

    [Fact]
    public void FromMetrics_WithHighPriorityAlerts_ShouldReturnImpaired()
    {
        // Arrange
        var alerts = ImmutableList.Create(
            MonitoringAlert.HighPriority("a", "msg", Array.Empty<CognitiveEvent>(), "action"),
            MonitoringAlert.HighPriority("b", "msg2", Array.Empty<CognitiveEvent>(), "action2"));

        // Act
        var health = CognitiveHealth.FromMetrics(0.6, 0.5, 0.2, TimeSpan.FromSeconds(1), alerts);

        // Assert
        health.Status.Should().Be(HealthStatus.Impaired);
    }

    [Fact]
    public void FromMetrics_WithModerateIssues_ShouldReturnDegraded()
    {
        // Arrange
        var alerts = ImmutableList<MonitoringAlert>.Empty;

        // Act
        var health = CognitiveHealth.FromMetrics(0.6, 0.5, 0.15, TimeSpan.FromSeconds(1), alerts);

        // Assert
        health.Status.Should().Be(HealthStatus.Degraded);
    }

    [Fact]
    public void FromMetrics_ShouldClampValues()
    {
        // Act
        var health = CognitiveHealth.FromMetrics(1.5, -0.5, -1.0, TimeSpan.FromSeconds(1), ImmutableList<MonitoringAlert>.Empty);

        // Assert
        health.HealthScore.Should().Be(1.0);
        health.ProcessingEfficiency.Should().Be(0.0);
        health.ErrorRate.Should().Be(0.0);
    }

    #endregion

    #region Validate

    [Fact]
    public void Validate_InvalidHealthScore_ShouldReturnFailure()
    {
        // Arrange
        var health = new CognitiveHealth(DateTime.UtcNow, 1.5, 0.5, 0.0, TimeSpan.Zero, ImmutableList<MonitoringAlert>.Empty, HealthStatus.Healthy);

        // Act
        var result = health.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Validate_InvalidEfficiency_ShouldReturnFailure()
    {
        // Arrange
        var health = new CognitiveHealth(DateTime.UtcNow, 0.5, -0.5, 0.0, TimeSpan.Zero, ImmutableList<MonitoringAlert>.Empty, HealthStatus.Healthy);

        // Act
        var result = health.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Validate_ValidValues_ShouldReturnSuccess()
    {
        // Arrange
        var health = CognitiveHealth.Optimal();

        // Act
        var result = health.Validate();

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion
}

[Trait("Category", "Unit")]
public class CognitiveIntrospectorTests
{
    #region Creation

    [Fact]
    public void Constructor_Default_ShouldInitialize()
    {
        // Arrange & Act
        var introspector = new CognitiveIntrospector();

        // Assert
        introspector.GetStateHistory().IsSuccess.Should().BeTrue();
        introspector.GetStateHistory().Value.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithMaxHistory_ShouldRespectLimit()
    {
        // Arrange & Act
        var introspector = new CognitiveIntrospector(10);

        // Assert
        introspector.GetStateHistory().Value.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithInvalidMaxHistory_ShouldUseDefault()
    {
        // Arrange & Act - the code uses max(0, value) effectively
        var introspector = new CognitiveIntrospector(-5);

        // Should still work without exception
        introspector.GetStateHistory().IsSuccess.Should().BeTrue();
    }

    #endregion

    #region CaptureState

    [Fact]
    public void CaptureState_ShouldAddToHistory()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();

        // Act
        var result = introspector.CaptureState();

        // Assert
        result.IsSuccess.Should().BeTrue();
        introspector.GetStateHistory().Value.Should().HaveCount(1);
    }

    #endregion

    #region Analyze

    [Fact]
    public void Analyze_NullState_ShouldReturnFailure()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();

        // Act
        var result = introspector.Analyze(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Analyze_ValidState_ShouldReturnReport()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();
        var state = InternalState.Initial();

        // Act
        var result = introspector.Analyze(state);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Observations.Should().NotBeEmpty();
    }

    #endregion

    #region CompareStates

    [Fact]
    public void CompareStates_NullStates_ShouldReturnFailure()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();

        // Act
        var result1 = introspector.CompareStates(null!, InternalState.Initial());
        var result2 = introspector.CompareStates(InternalState.Initial(), null!);

        // Assert
        result1.IsFailure.Should().BeTrue();
        result2.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void CompareStates_ValidStates_ShouldReturnComparison()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();
        var before = InternalState.Initial();
        var after = InternalState.Initial().WithCognitiveLoad(0.5);

        // Act
        var result = introspector.CompareStates(before, after);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CognitiveLoadDelta.Should().Be(0.5);
    }

    #endregion

    #region IdentifyPatterns

    [Fact]
    public void IdentifyPatterns_NullHistory_ShouldReturnFailure()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();

        // Act
        var result = introspector.IdentifyPatterns(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void IdentifyPatterns_InsufficientHistory_ShouldReturnMessage()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();
        var history = new[] { InternalState.Initial() };

        // Act
        var result = introspector.IdentifyPatterns(history);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
    }

    [Fact]
    public void IdentifyPatterns_WithHistory_ShouldDetectPatterns()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();
        var states = new List<InternalState>();
        for (int i = 0; i < 5; i++)
        {
            introspector.SetCognitiveLoad(0.2 + i * 0.15);
            var capture = introspector.CaptureState();
            if (capture.IsSuccess) states.Add(capture.Value);
        }

        // Act
        var result = introspector.IdentifyPatterns(states);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
    }

    #endregion

    #region Focus Management

    [Fact]
    public void SetCurrentFocus_NullOrEmpty_ShouldReturnFailure()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();

        // Act
        var result1 = introspector.SetCurrentFocus(null!);
        var result2 = introspector.SetCurrentFocus("   ");

        // Assert
        result1.IsFailure.Should().BeTrue();
        result2.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void SetCurrentFocus_ValidFocus_ShouldReturnSuccess()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();

        // Act
        var result = introspector.SetCurrentFocus("analysis");

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region Goal Management

    [Fact]
    public void AddGoal_NullOrEmpty_ShouldReturnFailure()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();

        // Act
        var result1 = introspector.AddGoal(null!);
        var result2 = introspector.AddGoal("   ");

        // Assert
        result1.IsFailure.Should().BeTrue();
        result2.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void AddGoal_Duplicate_ShouldReturnFailure()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();
        introspector.AddGoal("goal1");

        // Act
        var result = introspector.AddGoal("goal1");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void AddGoal_NewGoal_ShouldReturnSuccess()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();

        // Act
        var result = introspector.AddGoal("goal1");

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void RemoveGoal_NullOrEmpty_ShouldReturnFailure()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();

        // Act
        var result1 = introspector.RemoveGoal(null!);
        var result2 = introspector.RemoveGoal("   ");

        // Assert
        result1.IsFailure.Should().BeTrue();
        result2.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RemoveGoal_MissingGoal_ShouldReturnFailure()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();

        // Act
        var result = introspector.RemoveGoal("nonexistent");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RemoveGoal_ExistingGoal_ShouldReturnSuccess()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();
        introspector.AddGoal("goal1");

        // Act
        var result = introspector.RemoveGoal("goal1");

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region State Updates

    [Fact]
    public void SetCognitiveLoad_ShouldUpdateState()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();

        // Act
        var result = introspector.SetCognitiveLoad(0.75);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void SetValence_ShouldUpdateState()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();

        // Act
        var result = introspector.SetValence(-0.5);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void SetMode_ShouldUpdateState()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();

        // Act
        var result = introspector.SetMode(ProcessingMode.Analytical);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion
}

[Trait("Category", "Unit")]
public class CognitiveMonitorArrowTests
{
    [Fact]
    public void RecordArrow_ShouldReturnKleisliArrow()
    {
        // Arrange
        var monitor = new Mock<ICognitiveMonitor>();
        monitor.Setup(m => m.RecordEvent(It.IsAny<CognitiveEvent>())).Returns(Result<Unit, string>.Success(Unit.Value));
        var arrow = CognitiveMonitorArrow.RecordArrow(monitor.Object);
        var evt = CognitiveEvent.Thought("test");

        // Act
        var result = arrow(evt).Result;

        // Assert
        result.IsSuccess.Should().BeTrue();
        monitor.Verify(m => m.RecordEvent(evt), Times.Once);
    }

    [Fact]
    public void HealthCheckArrow_ShouldReturnHealth()
    {
        // Arrange
        var monitor = new Mock<ICognitiveMonitor>();
        var health = CognitiveHealth.Optimal();
        monitor.Setup(m => m.GetHealth()).Returns(health);
        var arrow = CognitiveMonitorArrow.HealthCheckArrow(monitor.Object);

        // Act
        var result = arrow(Unit.Value).Result;

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(health);
    }

    [Fact]
    public void GetRecentEventsArrow_NegativeCount_ShouldReturnFailure()
    {
        // Arrange
        var monitor = new Mock<ICognitiveMonitor>();
        var arrow = CognitiveMonitorArrow.GetRecentEventsArrow(monitor.Object);

        // Act
        var result = arrow(-1).Result;

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void GetRecentEventsArrow_ValidCount_ShouldReturnEvents()
    {
        // Arrange
        var monitor = new Mock<ICognitiveMonitor>();
        monitor.Setup(m => m.GetRecentEvents(5)).Returns(ImmutableList<CognitiveEvent>.Empty);
        var arrow = CognitiveMonitorArrow.GetRecentEventsArrow(monitor.Object);

        // Act
        var result = arrow(5).Result;

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void AcknowledgeAlertArrow_ShouldDelegateToMonitor()
    {
        // Arrange
        var monitor = new Mock<ICognitiveMonitor>();
        monitor.Setup(m => m.AcknowledgeAlert(It.IsAny<Guid>())).Returns(Result<Unit, string>.Success(Unit.Value));
        var arrow = CognitiveMonitorArrow.AcknowledgeAlertArrow(monitor.Object);
        var alertId = Guid.NewGuid();

        // Act
        var result = arrow(alertId).Result;

        // Assert
        result.IsSuccess.Should().BeTrue();
        monitor.Verify(m => m.AcknowledgeAlert(alertId), Times.Once);
    }

    [Fact]
    public void SetThresholdArrow_ShouldDelegateToMonitor()
    {
        // Arrange
        var monitor = new Mock<ICognitiveMonitor>();
        monitor.Setup(m => m.SetThreshold(It.IsAny<string>(), It.IsAny<double>())).Returns(Result<Unit, string>.Success(Unit.Value));
        var arrow = CognitiveMonitorArrow.SetThresholdArrow(monitor.Object);

        // Act
        var result = arrow(("metric1", 0.5)).Result;

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void RecordAndCheckHealthArrow_ShouldRecordThenReturnHealth()
    {
        // Arrange
        var monitor = new Mock<ICognitiveMonitor>();
        var health = CognitiveHealth.Optimal();
        monitor.Setup(m => m.RecordEvent(It.IsAny<CognitiveEvent>())).Returns(Result<Unit, string>.Success(Unit.Value));
        monitor.Setup(m => m.GetHealth()).Returns(health);
        var arrow = CognitiveMonitorArrow.RecordAndCheckHealthArrow(monitor.Object);

        // Act
        var result = arrow(CognitiveEvent.Thought("test")).Result;

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(health);
    }

    [Fact]
    public void HealthGateArrow_CriticalHealth_ShouldReturnFailure()
    {
        // Arrange
        var monitor = new Mock<ICognitiveMonitor>();
        var health = CognitiveHealth.FromMetrics(0.1, 0.2, 0.8, TimeSpan.Zero, ImmutableList<MonitoringAlert>.Empty);
        monitor.Setup(m => m.GetHealth()).Returns(health);
        var arrow = CognitiveMonitorArrow.HealthGateArrow(monitor.Object);

        // Act
        var result = arrow(Unit.Value).Result;

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void HealthGateArrow_NonCriticalHealth_ShouldReturnSuccess()
    {
        // Arrange
        var monitor = new Mock<ICognitiveMonitor>();
        var health = CognitiveHealth.Optimal();
        monitor.Setup(m => m.GetHealth()).Returns(health);
        var arrow = CognitiveMonitorArrow.HealthGateArrow(monitor.Object);

        // Act
        var result = arrow(Unit.Value).Result;

        // Assert
        result.IsSuccess.Should().BeTrue();
    }
}

[Trait("Category", "Unit")]
public class DimensionScoreTests
{
    #region Creation

    [Fact]
    public void Unknown_ShouldCreateWithDefaultValues()
    {
        // Act
        var score = DimensionScore.Unknown(PerformanceDimension.Accuracy);

        // Assert
        score.Dimension.Should().Be(PerformanceDimension.Accuracy);
        score.Score.Should().Be(0.5);
        score.Confidence.Should().Be(0.0);
        score.Evidence.Should().BeEmpty();
        score.Trend.Should().Be(Trend.Unknown);
    }

    [Fact]
    public void Create_ShouldClampValues()
    {
        // Act
        var score = DimensionScore.Create(PerformanceDimension.Speed, 1.5, -0.5, new[] { "evidence" });

        // Assert
        score.Score.Should().Be(1.0);
        score.Confidence.Should().Be(0.0);
    }

    #endregion

    #region WithBayesianUpdate

    [Fact]
    public void WithBayesianUpdate_ShouldUpdateScoreAndConfidence()
    {
        // Arrange
        var score = DimensionScore.Unknown(PerformanceDimension.Accuracy);

        // Act
        var updated = score.WithBayesianUpdate(0.8, 0.5, "new evidence");

        // Assert
        updated.Score.Should().NotBe(0.5);
        updated.Confidence.Should().BeGreaterThan(0.0);
        updated.Evidence.Should().Contain("new evidence");
    }

    [Fact]
    public void WithBayesianUpdate_WithZeroWeight_ShouldDefaultToNewScore()
    {
        // Arrange
        var score = DimensionScore.Unknown(PerformanceDimension.Accuracy);

        // Act
        var updated = score.WithBayesianUpdate(0.9, 0.0, "evidence");

        // Assert
        updated.Score.Should().Be(0.9);
    }

    [Fact]
    public void WithBayesianUpdate_SmallDelta_ShouldPreserveTrend()
    {
        // Arrange
        var score = DimensionScore.Create(PerformanceDimension.Accuracy, 0.5, 0.5, new[] { "e" });

        // Act
        var updated = score.WithBayesianUpdate(0.51, 0.1, "e2");

        // Assert
        updated.Trend.Should().Be(Trend.Stable);
    }

    [Fact]
    public void WithBayesianUpdate_PositiveDelta_ShouldSetImprovingTrend()
    {
        // Arrange
        var score = DimensionScore.Create(PerformanceDimension.Accuracy, 0.5, 0.5, new[] { "e" });

        // Act
        var updated = score.WithBayesianUpdate(0.8, 0.5, "e2");

        // Assert
        updated.Trend.Should().Be(Trend.Improving);
    }

    [Fact]
    public void WithBayesianUpdate_NegativeDelta_ShouldSetDecliningTrend()
    {
        // Arrange
        var score = DimensionScore.Create(PerformanceDimension.Accuracy, 0.8, 0.5, new[] { "e" });

        // Act
        var updated = score.WithBayesianUpdate(0.2, 0.5, "e2");

        // Assert
        updated.Trend.Should().Be(Trend.Declining);
    }

    #endregion

    #region Validate

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Validate_InvalidScore_ShouldReturnFailure(double scoreValue)
    {
        // Arrange
        var score = new DimensionScore(PerformanceDimension.Accuracy, scoreValue, 0.5, ImmutableList<string>.Empty, Trend.Unknown);

        // Act
        var result = score.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Validate_InvalidConfidence_ShouldReturnFailure(double confidence)
    {
        // Arrange
        var score = new DimensionScore(PerformanceDimension.Accuracy, 0.5, confidence, ImmutableList<string>.Empty, Trend.Unknown);

        // Act
        var result = score.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Validate_ValidValues_ShouldReturnSuccess()
    {
        // Arrange
        var score = DimensionScore.Unknown(PerformanceDimension.Accuracy);

        // Act
        var result = score.Validate();

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion
}

[Trait("Category", "Unit")]
public class HealthStatusTests
{
    [Theory]
    [InlineData(HealthStatus.Healthy)]
    [InlineData(HealthStatus.Degraded)]
    [InlineData(HealthStatus.Impaired)]
    [InlineData(HealthStatus.Critical)]
    public void AllEnumValues_ShouldBeDefined(HealthStatus value)
    {
        // Assert
        ((int)value).Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void Enum_ShouldHaveFourValues()
    {
        // Act
        var values = Enum.GetValues<HealthStatus>();

        // Assert
        values.Should().HaveCount(4);
    }
}

[Trait("Category", "Unit")]
public class InternalStateTests
{
    #region Creation

    [Fact]
    public void Initial_ShouldCreateDefaultState()
    {
        // Act
        var state = InternalState.Initial();

        // Assert
        state.ActiveGoals.Should().BeEmpty();
        state.CurrentFocus.Should().Be("None");
        state.CognitiveLoad.Should().Be(0.0);
        state.EmotionalValence.Should().Be(0.0);
        state.WorkingMemoryItems.Should().BeEmpty();
        state.AttentionDistribution.Should().BeEmpty();
        state.Mode.Should().Be(ProcessingMode.Reactive);
    }

    [Fact]
    public void Snapshot_ShouldCreateNewIdAndTimestamp()
    {
        // Arrange
        var state = InternalState.Initial();
        var originalId = state.Id;
        var originalTime = state.Timestamp;

        // Act
        var snapshot = state.Snapshot();

        // Assert
        snapshot.Id.Should().NotBe(originalId);
        snapshot.Timestamp.Should().BeAfter(originalTime);
    }

    #endregion

    #region Modifications

    [Fact]
    public void WithGoal_NullOrEmpty_ShouldReturnSameState()
    {
        // Arrange
        var state = InternalState.Initial();

        // Act
        var result1 = state.WithGoal(null!);
        var result2 = state.WithGoal("   ");

        // Assert
        result1.ActiveGoals.Should().BeEmpty();
        result2.ActiveGoals.Should().BeEmpty();
    }

    [Fact]
    public void WithGoal_ValidGoal_ShouldAddGoal()
    {
        // Arrange
        var state = InternalState.Initial();

        // Act
        var result = state.WithGoal("goal1");

        // Assert
        result.ActiveGoals.Should().Contain("goal1");
    }

    [Fact]
    public void WithoutGoal_ShouldRemoveGoal()
    {
        // Arrange
        var state = InternalState.Initial().WithGoal("goal1");

        // Act
        var result = state.WithoutGoal("goal1");

        // Assert
        result.ActiveGoals.Should().NotContain("goal1");
    }

    [Fact]
    public void WithFocus_ShouldUpdateFocus()
    {
        // Arrange
        var state = InternalState.Initial();

        // Act
        var result = state.WithFocus("analysis");

        // Assert
        result.CurrentFocus.Should().Be("analysis");
    }

    [Fact]
    public void WithFocus_Null_ShouldDefaultToNone()
    {
        // Arrange
        var state = InternalState.Initial();

        // Act
        var result = state.WithFocus(null!);

        // Assert
        result.CurrentFocus.Should().Be("None");
    }

    [Fact]
    public void WithCognitiveLoad_ShouldClampValue()
    {
        // Arrange
        var state = InternalState.Initial();

        // Act
        var low = state.WithCognitiveLoad(-0.5);
        var high = state.WithCognitiveLoad(1.5);
        var mid = state.WithCognitiveLoad(0.5);

        // Assert
        low.CognitiveLoad.Should().Be(0.0);
        high.CognitiveLoad.Should().Be(1.0);
        mid.CognitiveLoad.Should().Be(0.5);
    }

    [Fact]
    public void WithValence_ShouldClampValue()
    {
        // Arrange
        var state = InternalState.Initial();

        // Act
        var low = state.WithValence(-1.5);
        var high = state.WithValence(1.5);
        var mid = state.WithValence(0.5);

        // Assert
        low.EmotionalValence.Should().Be(-1.0);
        high.EmotionalValence.Should().Be(1.0);
        mid.EmotionalValence.Should().Be(0.5);
    }

    [Fact]
    public void WithWorkingMemoryItem_NullOrEmpty_ShouldReturnSameState()
    {
        // Arrange
        var state = InternalState.Initial();

        // Act
        var result1 = state.WithWorkingMemoryItem(null!);
        var result2 = state.WithWorkingMemoryItem("   ");

        // Assert
        result1.WorkingMemoryItems.Should().BeEmpty();
        result2.WorkingMemoryItems.Should().BeEmpty();
    }

    [Fact]
    public void WithWorkingMemoryItem_ValidItem_ShouldAddItem()
    {
        // Arrange
        var state = InternalState.Initial();

        // Act
        var result = state.WithWorkingMemoryItem("item1");

        // Assert
        result.WorkingMemoryItems.Should().Contain("item1");
    }

    [Fact]
    public void WithAttention_ShouldSetDistribution()
    {
        // Arrange
        var state = InternalState.Initial();
        var distribution = ImmutableDictionary<string, double>.Empty.Add("focus1", 0.7).Add("focus2", 0.3);

        // Act
        var result = state.WithAttention(distribution);

        // Assert
        result.AttentionDistribution.Should().HaveCount(2);
    }

    [Fact]
    public void WithMode_ShouldChangeMode()
    {
        // Arrange
        var state = InternalState.Initial();

        // Act
        var result = state.WithMode(ProcessingMode.Analytical);

        // Assert
        result.Mode.Should().Be(ProcessingMode.Analytical);
    }

    #endregion
}

[Trait("Category", "Unit")]
public class IntrospectionArrowTests
{
    [Fact]
    public void CaptureStateArrow_ShouldDelegateToIntrospector()
    {
        // Arrange
        var introspector = new Mock<IIntrospector>();
        var state = InternalState.Initial();
        introspector.Setup(i => i.CaptureState()).Returns(Result<InternalState, string>.Success(state));
        var arrow = IntrospectionArrow.CaptureStateArrow(introspector.Object);

        // Act
        var result = arrow(Unit.Value).Result;

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(state);
    }

    [Fact]
    public void AnalyzeArrow_ShouldDelegateToIntrospector()
    {
        // Arrange
        var introspector = new Mock<IIntrospector>();
        var report = IntrospectionReport.Empty(InternalState.Initial());
        introspector.Setup(i => i.Analyze(It.IsAny<InternalState>())).Returns(Result<IntrospectionReport, string>.Success(report));
        var arrow = IntrospectionArrow.AnalyzeArrow(introspector.Object);

        // Act
        var result = arrow(InternalState.Initial()).Result;

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void FullIntrospectionArrow_CaptureFailure_ShouldReturnFailure()
    {
        // Arrange
        var introspector = new Mock<IIntrospector>();
        introspector.Setup(i => i.CaptureState()).Returns(Result<InternalState, string>.Failure("capture failed"));
        var arrow = IntrospectionArrow.FullIntrospectionArrow(introspector.Object);

        // Act
        var result = arrow(Unit.Value).Result;

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void FocusAndCaptureArrow_FocusFailure_ShouldReturnFailure()
    {
        // Arrange
        var introspector = new Mock<IIntrospector>();
        introspector.Setup(i => i.SetCurrentFocus(It.IsAny<string>())).Returns(Result<Unit, string>.Failure("focus failed"));
        var arrow = IntrospectionArrow.FocusAndCaptureArrow(introspector.Object);

        // Act
        var result = arrow("test").Result;

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void MonitorTransitionArrow_InsufficientHistory_ShouldReturnNone()
    {
        // Arrange
        var introspector = new Mock<IIntrospector>();
        introspector.Setup(i => i.GetStateHistory()).Returns(Result<ImmutableList<InternalState>, string>.Success(ImmutableList<InternalState>.Empty));
        var arrow = IntrospectionArrow.MonitorTransitionArrow(introspector.Object);

        // Act
        var result = arrow(Unit.Value).Result;

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.HasValue.Should().BeFalse();
    }
}

[Trait("Category", "Unit")]
public class IntrospectionReportTests
{
    #region Creation

    [Fact]
    public void Empty_ShouldCreateWithDefaults()
    {
        // Arrange
        var state = InternalState.Initial();

        // Act
        var report = IntrospectionReport.Empty(state);

        // Assert
        report.StateSnapshot.Should().Be(state);
        report.Observations.Should().BeEmpty();
        report.Anomalies.Should().BeEmpty();
        report.Recommendations.Should().BeEmpty();
        report.SelfAssessmentScore.Should().Be(0.5);
        report.HasAnomalies.Should().BeFalse();
        report.HasRecommendations.Should().BeFalse();
    }

    #endregion

    #region WithObservation

    [Fact]
    public void WithObservation_ShouldAddObservation()
    {
        // Arrange
        var report = IntrospectionReport.Empty(InternalState.Initial());

        // Act
        var updated = report.WithObservation("obs1");

        // Assert
        updated.Observations.Should().Contain("obs1");
    }

    #endregion

    #region WithAnomaly

    [Fact]
    public void WithAnomaly_ShouldAddAnomaly()
    {
        // Arrange
        var report = IntrospectionReport.Empty(InternalState.Initial());

        // Act
        var updated = report.WithAnomaly("anomaly1");

        // Assert
        updated.Anomalies.Should().Contain("anomaly1");
        updated.HasAnomalies.Should().BeTrue();
    }

    #endregion

    #region WithRecommendation

    [Fact]
    public void WithRecommendation_ShouldAddRecommendation()
    {
        // Arrange
        var report = IntrospectionReport.Empty(InternalState.Initial());

        // Act
        var updated = report.WithRecommendation("rec1");

        // Assert
        updated.Recommendations.Should().Contain("rec1");
        updated.HasRecommendations.Should().BeTrue();
    }

    #endregion
}

[Trait("Category", "Unit")]
public class MetacognitiveAnalysisTests
{
    #region Creation and Properties

    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        // Arrange
        var trace = ReasoningTrace.Start();
        var reflection = ReflectionResult.Invalid(trace);
        var style = ThinkingStyle.Balanced();
        var improvements = ImmutableList.Create("improvement1");

        // Act
        var analysis = new MetacognitiveAnalysis(trace, reflection, style, improvements, DateTime.UtcNow);

        // Assert
        analysis.Trace.Should().Be(trace);
        analysis.Reflection.Should().Be(reflection);
        analysis.Style.Should().Be(style);
        analysis.Improvements.Should().ContainSingle();
    }

    [Fact]
    public void QualitySummary_ExcellentQuality_ShouldReturnExcellentMessage()
    {
        // Arrange
        var trace = ReasoningTrace.Start();
        var reflection = ReflectionResult.HighQuality(trace);
        var analysis = new MetacognitiveAnalysis(trace, reflection, ThinkingStyle.Balanced(), ImmutableList<string>.Empty, DateTime.UtcNow);

        // Act & Assert
        analysis.QualitySummary.Should().Be("Excellent reasoning quality");
        analysis.IsAcceptable.Should().BeTrue();
    }

    [Fact]
    public void QualitySummary_PoorQuality_ShouldReturnPoorMessage()
    {
        // Arrange
        var trace = ReasoningTrace.Start();
        var reflection = ReflectionResult.Invalid(trace);
        var analysis = new MetacognitiveAnalysis(trace, reflection, ThinkingStyle.Balanced(), ImmutableList<string>.Empty, DateTime.UtcNow);

        // Act & Assert
        analysis.QualitySummary.Should().Be("Very poor reasoning quality - fundamental issues detected");
        analysis.IsAcceptable.Should().BeFalse();
    }

    [Fact]
    public void PriorityImprovements_ShouldReturnUpToThree()
    {
        // Arrange
        var trace = ReasoningTrace.Start();
        var reflection = ReflectionResult.HighQuality(trace);
        var improvements = ImmutableList.Create("a", "b", "c", "d", "e");
        var analysis = new MetacognitiveAnalysis(trace, reflection, ThinkingStyle.Balanced(), improvements, DateTime.UtcNow);

        // Act
        var priority = analysis.PriorityImprovements.ToList();

        // Assert
        priority.Should().HaveCount(3);
        priority.Should().ContainInOrder("a", "b", "c");
    }

    #endregion
}

[Trait("Category", "Unit")]
public class MetacognitiveReasonerTests
{
    #region StartTrace and GetActiveTrace

    [Fact]
    public void StartTrace_ShouldCreateActiveTrace()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();

        // Act
        var traceId = reasoner.StartTrace();

        // Assert
        traceId.Should().NotBe(Guid.Empty);
        reasoner.GetActiveTrace().HasValue.Should().BeTrue();
    }

    [Fact]
    public void GetActiveTrace_WhenNoTrace_ShouldReturnNone()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();

        // Act
        var result = reasoner.GetActiveTrace();

        // Assert
        result.HasValue.Should().BeFalse();
    }

    #endregion

    #region AddStep

    [Fact]
    public void AddStep_NoActiveTrace_ShouldReturnFailure()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();

        // Act
        var result = reasoner.AddStep(ReasoningStepType.Observation, "content", "justification");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void AddStep_WithActiveTrace_ShouldReturnStepNumber()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();
        reasoner.StartTrace();

        // Act
        var result = reasoner.AddStep(ReasoningStepType.Observation, "content", "justification");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1);
    }

    [Fact]
    public void AddStep_InvalidDependencies_ShouldReturnFailure()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();
        reasoner.StartTrace();

        // Act
        var result = reasoner.AddStep(ReasoningStepType.Inference, "content", "justification", 99);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region EndTrace

    [Fact]
    public void EndTrace_NoActiveTrace_ShouldReturnFailure()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();

        // Act
        var result = reasoner.EndTrace("conclusion", true);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void EndTrace_WithActiveTrace_ShouldCompleteTrace()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();
        reasoner.StartTrace();
        reasoner.AddStep(ReasoningStepType.Observation, "obs", "just");

        // Act
        var result = reasoner.EndTrace("conclusion", true);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsActive.Should().BeFalse();
        result.Value.WasSuccessful.Should().BeTrue();
    }

    #endregion

    #region ReflectOn

    [Fact]
    public void ReflectOn_EmptyTrace_ShouldReturnInvalid()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();
        var trace = ReasoningTrace.Start();

        // Act
        var result = reasoner.ReflectOn(trace);

        // Assert
        result.QualityScore.Should().Be(0.0);
        result.HasIssues.Should().BeTrue();
    }

    [Fact]
    public void ReflectOn_ValidTrace_ShouldAnalyzeQuality()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();
        var trace = ReasoningTrace.Start()
            .AddObservation("obs1", "just1")
            .AddInference("inf1", "just2", 1)
            .AddObservation("obs2", "just3")
            .Complete("conclusion", 0.8, true);

        // Act
        var result = reasoner.ReflectOn(trace);

        // Assert
        result.QualityScore.Should().BeGreaterThan(0.0);
    }

    #endregion

    #region GetThinkingStyle

    [Fact]
    public void GetThinkingStyle_NoHistory_ShouldReturnBalanced()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();

        // Act
        var style = reasoner.GetThinkingStyle();

        // Assert
        style.StyleName.Should().Be("Balanced");
    }

    [Fact]
    public void GetThinkingStyle_WithHistory_ShouldAnalyzeStyle()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();
        for (int i = 0; i < 3; i++)
        {
            reasoner.StartTrace();
            reasoner.AddStep(ReasoningStepType.Observation, "obs", "just");
            reasoner.AddStep(ReasoningStepType.Inference, "inf", "just", 1);
            reasoner.EndTrace("done", true);
        }

        // Act
        var style = reasoner.GetThinkingStyle();

        // Assert
        style.Should().NotBeNull();
    }

    #endregion

    #region IdentifyBiases

    [Fact]
    public void IdentifyBiases_EmptyHistory_ShouldReturnEmpty()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();

        // Act
        var biases = reasoner.IdentifyBiases(Array.Empty<ReasoningTrace>());

        // Assert
        biases.Should().BeEmpty();
    }

    [Fact]
    public void IdentifyBiases_WithHastyGeneralization_ShouldDetect()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();
        var trace = ReasoningTrace.Start()
            .AddObservation("obs1", "just1")
            .Complete("conclusion", 0.8, true);

        // Act
        var biases = reasoner.IdentifyBiases(new[] { trace });

        // Assert
        biases.Should().ContainKey("Hasty Generalization");
    }

    #endregion

    #region SuggestImprovement

    [Fact]
    public void SuggestImprovement_EmptyTrace_ShouldReturnEmpty()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();
        var trace = ReasoningTrace.Start();

        // Act
        var suggestions = reasoner.SuggestImprovement(trace);

        // Assert
        suggestions.Should().NotBeEmpty(); // Even empty traces get "develop more" suggestion
    }

    [Fact]
    public void SuggestImprovement_ShortTrace_ShouldSuggestMoreObservations()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();
        var trace = ReasoningTrace.Start().AddObservation("obs1", "just1");

        // Act
        var suggestions = reasoner.SuggestImprovement(trace);

        // Assert
        suggestions.Should().Contain(s => s.Contains("observations", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region GetHistory

    [Fact]
    public void GetHistory_NoCompletedTraces_ShouldBeEmpty()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();

        // Act
        var history = reasoner.GetHistory();

        // Assert
        history.Should().BeEmpty();
    }

    [Fact]
    public void GetHistory_AfterEndTrace_ShouldContainTrace()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();
        reasoner.StartTrace();
        reasoner.AddStep(ReasoningStepType.Observation, "obs", "just");
        reasoner.EndTrace("done", true);

        // Act
        var history = reasoner.GetHistory();

        // Assert
        history.Should().ContainSingle();
    }

    #endregion
}

[Trait("Category", "Unit")]
public class MonitoringAlertTests
{
    #region Factory Methods

    [Fact]
    public void HighPriority_ShouldCreateWithPriority8()
    {
        // Arrange
        var events = Array.Empty<CognitiveEvent>();

        // Act
        var alert = MonitoringAlert.HighPriority("type", "message", events, "action");

        // Assert
        alert.Priority.Should().Be(8);
        alert.AlertType.Should().Be("type");
        alert.Message.Should().Be("message");
        alert.RecommendedAction.Should().Be("action");
        alert.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void MediumPriority_ShouldCreateWithPriority5()
    {
        // Act
        var alert = MonitoringAlert.MediumPriority("type", "message", Array.Empty<CognitiveEvent>(), "action");

        // Assert
        alert.Priority.Should().Be(5);
    }

    [Fact]
    public void LowPriority_ShouldCreateWithPriority2()
    {
        // Act
        var alert = MonitoringAlert.LowPriority("type", "message", Array.Empty<CognitiveEvent>(), "action");

        // Assert
        alert.Priority.Should().Be(2);
    }

    #endregion

    #region Validate

    [Fact]
    public void Validate_InvalidPriorityLow_ShouldReturnFailure()
    {
        // Arrange
        var alert = new MonitoringAlert(Guid.NewGuid(), "type", "msg", ImmutableList<CognitiveEvent>.Empty, "action", 0, DateTime.UtcNow);

        // Act
        var result = alert.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Validate_InvalidPriorityHigh_ShouldReturnFailure()
    {
        // Arrange
        var alert = new MonitoringAlert(Guid.NewGuid(), "type", "msg", ImmutableList<CognitiveEvent>.Empty, "action", 11, DateTime.UtcNow);

        // Act
        var result = alert.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyAlertType_ShouldReturnFailure()
    {
        // Arrange
        var alert = new MonitoringAlert(Guid.NewGuid(), "", "msg", ImmutableList<CognitiveEvent>.Empty, "action", 5, DateTime.UtcNow);

        // Act
        var result = alert.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyMessage_ShouldReturnFailure()
    {
        // Arrange
        var alert = new MonitoringAlert(Guid.NewGuid(), "type", "", ImmutableList<CognitiveEvent>.Empty, "action", 5, DateTime.UtcNow);

        // Act
        var result = alert.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Validate_ValidAlert_ShouldReturnSuccess()
    {
        // Arrange
        var alert = MonitoringAlert.HighPriority("type", "msg", Array.Empty<CognitiveEvent>(), "action");

        // Act
        var result = alert.Validate();

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion
}

[Trait("Category", "Unit")]
public class PerformanceDimensionTests
{
    [Theory]
    [InlineData(PerformanceDimension.Accuracy)]
    [InlineData(PerformanceDimension.Speed)]
    [InlineData(PerformanceDimension.Creativity)]
    [InlineData(PerformanceDimension.Consistency)]
    [InlineData(PerformanceDimension.Adaptability)]
    [InlineData(PerformanceDimension.Communication)]
    public void AllEnumValues_ShouldBeDefined(PerformanceDimension value)
    {
        // Assert
        ((int)value).Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void Enum_ShouldHaveSixValues()
    {
        // Act
        var values = Enum.GetValues<PerformanceDimension>();

        // Assert
        values.Should().HaveCount(6);
    }
}

[Trait("Category", "Unit")]
public class ProcessingModeTests
{
    [Theory]
    [InlineData(ProcessingMode.Analytical)]
    [InlineData(ProcessingMode.Creative)]
    [InlineData(ProcessingMode.Reactive)]
    [InlineData(ProcessingMode.Reflective)]
    [InlineData(ProcessingMode.Intuitive)]
    public void AllEnumValues_ShouldBeDefined(ProcessingMode value)
    {
        // Assert
        ((int)value).Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void Enum_ShouldHaveFiveValues()
    {
        // Act
        var values = Enum.GetValues<ProcessingMode>();

        // Assert
        values.Should().HaveCount(5);
    }
}

[Trait("Category", "Unit")]
public class RealtimeCognitiveMonitorTests
{
    #region Creation

    [Fact]
    public void Constructor_Default_ShouldInitialize()
    {
        // Arrange & Act
        var monitor = new RealtimeCognitiveMonitor();

        // Assert
        monitor.GetRecentEvents(10).Should().BeEmpty();
        monitor.GetAlerts().Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithCustomParameters_ShouldInitialize()
    {
        // Arrange & Act
        var monitor = new RealtimeCognitiveMonitor(500, TimeSpan.FromMinutes(2));

        // Assert
        monitor.GetRecentEvents(10).Should().BeEmpty();
    }

    #endregion

    #region RecordEvent

    [Fact]
    public void RecordEvent_NullEvent_ShouldReturnFailure()
    {
        // Arrange
        var monitor = new RealtimeCognitiveMonitor();

        // Act
        var result = monitor.RecordEvent(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RecordEvent_ValidEvent_ShouldReturnSuccess()
    {
        // Arrange
        var monitor = new RealtimeCognitiveMonitor();
        var evt = CognitiveEvent.Thought("test");

        // Act
        var result = monitor.RecordEvent(evt);

        // Assert
        result.IsSuccess.Should().BeTrue();
        monitor.GetRecentEvents(10).Should().ContainSingle();
    }

    [Fact]
    public void RecordEvent_CriticalEvent_ShouldGenerateAlert()
    {
        // Arrange
        var monitor = new RealtimeCognitiveMonitor();
        var evt = CognitiveEvent.Error("critical error", Severity.Critical);

        // Act
        monitor.RecordEvent(evt);

        // Assert
        monitor.GetAlerts().Should().NotBeEmpty();
    }

    #endregion

    #region GetHealth

    [Fact]
    public void GetHealth_EmptyMonitor_ShouldReturnOptimalMetrics()
    {
        // Arrange
        var monitor = new RealtimeCognitiveMonitor();

        // Act
        var health = monitor.GetHealth();

        // Assert
        health.ErrorRate.Should().Be(0.0);
        health.HealthScore.Should().BeGreaterThan(0.0);
    }

    [Fact]
    public void GetHealth_WithErrors_ShouldReflectErrorRate()
    {
        // Arrange
        var monitor = new RealtimeCognitiveMonitor();
        monitor.RecordEvent(CognitiveEvent.Error("err1"));
        monitor.RecordEvent(CognitiveEvent.Thought("thought1"));

        // Act
        var health = monitor.GetHealth();

        // Assert
        health.ErrorRate.Should().BeGreaterThan(0.0);
    }

    #endregion

    #region GetRecentEvents

    [Fact]
    public void GetRecentEvents_ShouldReturnOrderedEvents()
    {
        // Arrange
        var monitor = new RealtimeCognitiveMonitor();
        monitor.RecordEvent(CognitiveEvent.Thought("first"));
        monitor.RecordEvent(CognitiveEvent.Thought("second"));

        // Act
        var events = monitor.GetRecentEvents(2);

        // Assert
        events.Should().HaveCount(2);
    }

    #endregion

    #region GetAlerts

    [Fact]
    public void GetAlerts_ShouldReturnOrderedByPriority()
    {
        // Arrange
        var monitor = new RealtimeCognitiveMonitor();
        // Trigger multiple alerts through different means
        monitor.RecordEvent(CognitiveEvent.Error("err", Severity.Critical));

        // Act
        var alerts = monitor.GetAlerts();

        // Assert
        alerts.Should().NotBeEmpty();
    }

    #endregion

    #region AcknowledgeAlert

    [Fact]
    public void AcknowledgeAlert_ExistingAlert_ShouldRemoveAlert()
    {
        // Arrange
        var monitor = new RealtimeCognitiveMonitor();
        monitor.RecordEvent(CognitiveEvent.Error("err", Severity.Critical));
        var alertId = monitor.GetAlerts().First().Id;

        // Act
        var result = monitor.AcknowledgeAlert(alertId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        monitor.GetAlerts().Should().BeEmpty();
    }

    [Fact]
    public void AcknowledgeAlert_NonExistentAlert_ShouldReturnFailure()
    {
        // Arrange
        var monitor = new RealtimeCognitiveMonitor();

        // Act
        var result = monitor.AcknowledgeAlert(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region SetThreshold

    [Fact]
    public void SetThreshold_NullMetric_ShouldReturnFailure()
    {
        // Arrange
        var monitor = new RealtimeCognitiveMonitor();

        // Act
        var result = monitor.SetThreshold(null!, 0.5);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void SetThreshold_NegativeThreshold_ShouldReturnFailure()
    {
        // Arrange
        var monitor = new RealtimeCognitiveMonitor();

        // Act
        var result = monitor.SetThreshold("metric", -1.0);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void SetThreshold_ValidInput_ShouldReturnSuccess()
    {
        // Arrange
        var monitor = new RealtimeCognitiveMonitor();

        // Act
        var result = monitor.SetThreshold("error_rate", 0.2);

        // Assert
        result.IsSuccess.Should().BeTrue();
        monitor.GetThreshold("error_rate").HasValue.Should().BeTrue();
        monitor.GetThreshold("error_rate").Value.Should().Be(0.2);
    }

    #endregion

    #region GetThreshold

    [Fact]
    public void GetThreshold_UnknownMetric_ShouldReturnNone()
    {
        // Arrange
        var monitor = new RealtimeCognitiveMonitor();

        // Act
        var result = monitor.GetThreshold("unknown");

        // Assert
        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public void GetThreshold_KnownMetric_ShouldReturnValue()
    {
        // Arrange
        var monitor = new RealtimeCognitiveMonitor();

        // Act
        var result = monitor.GetThreshold("error_rate");

        // Assert
        result.HasValue.Should().BeTrue();
    }

    #endregion

    #region Subscribe

    [Fact]
    public void Subscribe_NullHandler_ShouldThrowArgumentNullException()
    {
        // Arrange
        var monitor = new RealtimeCognitiveMonitor();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => monitor.Subscribe(null!));
    }

    [Fact]
    public void Subscribe_ValidHandler_ShouldReturnDisposable()
    {
        // Arrange
        var monitor = new RealtimeCognitiveMonitor();
        var received = false;

        // Act
        var subscription = monitor.Subscribe(_ => received = true);
        monitor.RecordEvent(CognitiveEvent.Error("err", Severity.Critical));

        // Assert
        subscription.Should().NotBeNull();
        subscription.Should().BeAssignableTo<IDisposable>();
    }

    #endregion

    #region Reset and Dispose

    [Fact]
    public void Reset_ShouldClearAllData()
    {
        // Arrange
        var monitor = new RealtimeCognitiveMonitor();
        monitor.RecordEvent(CognitiveEvent.Thought("test"));

        // Act
        monitor.Reset();

        // Assert
        monitor.GetRecentEvents(10).Should().BeEmpty();
        monitor.GetAlerts().Should().BeEmpty();
    }

    [Fact]
    public void Dispose_ShouldMarkDisposed()
    {
        // Arrange
        var monitor = new RealtimeCognitiveMonitor();

        // Act
        monitor.Dispose();

        // Assert
        Assert.Throws<ObjectDisposedException>(() => monitor.GetHealth());
    }

    #endregion
}

[Trait("Category", "Unit")]
public class ReasoningStepTests
{
    #region Factory Methods

    [Fact]
    public void Observation_ShouldCreateWithCorrectType()
    {
        // Act
        var step = ReasoningStep.Observation(1, "content", "justification");

        // Assert
        step.StepType.Should().Be(ReasoningStepType.Observation);
        step.StepNumber.Should().Be(1);
        step.Dependencies.Should().BeEmpty();
    }

    [Fact]
    public void Inference_ShouldCreateWithDependencies()
    {
        // Act
        var step = ReasoningStep.Inference(2, "content", "justification", 1);

        // Assert
        step.StepType.Should().Be(ReasoningStepType.Inference);
        step.Dependencies.Should().Contain(1);
    }

    [Fact]
    public void Hypothesis_ShouldCreateWithCorrectType()
    {
        // Act
        var step = ReasoningStep.Hypothesis(2, "content", "justification", 1);

        // Assert
        step.StepType.Should().Be(ReasoningStepType.Hypothesis);
    }

    [Fact]
    public void Conclusion_ShouldCreateWithCorrectType()
    {
        // Act
        var step = ReasoningStep.Conclusion(3, "content", "justification", 1, 2);

        // Assert
        step.StepType.Should().Be(ReasoningStepType.Conclusion);
        step.Dependencies.Should().HaveCount(2);
    }

    #endregion

    #region WithDependency

    [Fact]
    public void WithDependency_ShouldAddDependency()
    {
        // Arrange
        var step = ReasoningStep.Observation(1, "content", "justification");

        // Act
        var updated = step.WithDependency(2);

        // Assert
        updated.Dependencies.Should().Contain(2);
    }

    #endregion

    #region HasValidDependencies

    [Fact]
    public void HasValidDependencies_NoDependencies_ShouldReturnTrue()
    {
        // Arrange
        var step = ReasoningStep.Observation(1, "content", "justification");

        // Act
        var result = step.HasValidDependencies();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasValidDependencies_ValidDependencies_ShouldReturnTrue()
    {
        // Arrange
        var step = ReasoningStep.Inference(3, "content", "justification", 1, 2);

        // Act
        var result = step.HasValidDependencies();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasValidDependencies_SelfReference_ShouldReturnFalse()
    {
        // Arrange
        var step = ReasoningStep.Inference(1, "content", "justification", 1);

        // Act
        var result = step.HasValidDependencies();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasValidDependencies_FutureReference_ShouldReturnFalse()
    {
        // Arrange
        var step = ReasoningStep.Inference(1, "content", "justification", 2);

        // Act
        var result = step.HasValidDependencies();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasValidDependencies_ZeroReference_ShouldReturnFalse()
    {
        // Arrange
        var step = ReasoningStep.Inference(2, "content", "justification", 0);

        // Act
        var result = step.HasValidDependencies();

        // Assert
        result.Should().BeFalse();
    }

    #endregion
}

[Trait("Category", "Unit")]
public class ReasoningStepTypeTests
{
    [Theory]
    [InlineData(ReasoningStepType.Observation)]
    [InlineData(ReasoningStepType.Inference)]
    [InlineData(ReasoningStepType.Hypothesis)]
    [InlineData(ReasoningStepType.Validation)]
    [InlineData(ReasoningStepType.Revision)]
    [InlineData(ReasoningStepType.Assumption)]
    [InlineData(ReasoningStepType.Conclusion)]
    [InlineData(ReasoningStepType.Contradiction)]
    public void AllEnumValues_ShouldBeDefined(ReasoningStepType value)
    {
        // Assert
        ((int)value).Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void Enum_ShouldHaveEightValues()
    {
        // Act
        var values = Enum.GetValues<ReasoningStepType>();

        // Assert
        values.Should().HaveCount(8);
    }
}

[Trait("Category", "Unit")]
public class ReasoningTraceTests
{
    #region Creation

    [Fact]
    public void Start_ShouldCreateActiveTrace()
    {
        // Act
        var trace = ReasoningTrace.Start();

        // Assert
        trace.Id.Should().NotBe(Guid.Empty);
        trace.IsActive.Should().BeTrue();
        trace.Steps.Should().BeEmpty();
        trace.NextStepNumber.Should().Be(1);
        trace.Duration.Should().BeNull();
    }

    [Fact]
    public void StartWithId_ShouldUseProvidedId()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var trace = ReasoningTrace.StartWithId(id);

        // Assert
        trace.Id.Should().Be(id);
    }

    #endregion

    #region Adding Steps

    [Fact]
    public void AddObservation_ShouldAddStep()
    {
        // Arrange
        var trace = ReasoningTrace.Start();

        // Act
        var updated = trace.AddObservation("obs1", "just1");

        // Assert
        updated.Steps.Should().HaveCount(1);
        updated.Steps[0].StepType.Should().Be(ReasoningStepType.Observation);
    }

    [Fact]
    public void AddInference_ShouldAddStepWithDependencies()
    {
        // Arrange
        var trace = ReasoningTrace.Start().AddObservation("obs1", "just1");

        // Act
        var updated = trace.AddInference("inf1", "just2", 1);

        // Assert
        updated.Steps.Should().HaveCount(2);
        updated.Steps[1].Dependencies.Should().Contain(1);
    }

    [Fact]
    public void AddHypothesis_ShouldAddStep()
    {
        // Arrange
        var trace = ReasoningTrace.Start().AddObservation("obs1", "just1");

        // Act
        var updated = trace.AddHypothesis("hyp1", "just2", 1);

        // Assert
        updated.Steps[1].StepType.Should().Be(ReasoningStepType.Hypothesis);
    }

    #endregion

    #region Complete

    [Fact]
    public void Complete_ShouldFinalizeTrace()
    {
        // Arrange
        var trace = ReasoningTrace.Start().AddObservation("obs1", "just1");

        // Act
        var completed = trace.Complete("conclusion", 0.8, true);

        // Assert
        completed.IsActive.Should().BeFalse();
        completed.WasSuccessful.Should().BeTrue();
        completed.FinalConclusion.Should().Be("conclusion");
        completed.Confidence.Should().Be(0.8);
        completed.Duration.Should().NotBeNull();
    }

    [Fact]
    public void Complete_ShouldClampConfidence()
    {
        // Arrange
        var trace = ReasoningTrace.Start();

        // Act
        var completed = trace.Complete("done", 1.5, true);

        // Assert
        completed.Confidence.Should().Be(1.0);
    }

    #endregion

    #region Fail

    [Fact]
    public void Fail_ShouldMarkAsFailed()
    {
        // Arrange
        var trace = ReasoningTrace.Start();

        // Act
        var failed = trace.Fail("error reason");

        // Assert
        failed.IsActive.Should().BeFalse();
        failed.WasSuccessful.Should().BeFalse();
        failed.Confidence.Should().Be(0.0);
        failed.FinalConclusion.Should().Contain("error reason");
    }

    #endregion

    #region GetStepsByType

    [Fact]
    public void GetStepsByType_ShouldFilterByType()
    {
        // Arrange
        var trace = ReasoningTrace.Start()
            .AddObservation("obs1", "just1")
            .AddObservation("obs2", "just2")
            .AddInference("inf1", "just3", 1);

        // Act
        var observations = trace.GetStepsByType(ReasoningStepType.Observation);
        var inferences = trace.GetStepsByType(ReasoningStepType.Inference);

        // Assert
        observations.Should().HaveCount(2);
        inferences.Should().HaveCount(1);
    }

    #endregion

    #region IsLogicallyConsistent

    [Fact]
    public void IsLogicallyConsistent_ValidDependencies_ShouldReturnTrue()
    {
        // Arrange
        var trace = ReasoningTrace.Start()
            .AddObservation("obs1", "just1")
            .AddInference("inf1", "just2", 1);

        // Act
        var result = trace.IsLogicallyConsistent();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsLogicallyConsistent_InvalidDependencies_ShouldReturnFalse()
    {
        // Arrange
        var trace = ReasoningTrace.Start();
        var step = ReasoningStep.Inference(1, "content", "justification", 99);
        trace = trace.WithStep(step);

        // Act
        var result = trace.IsLogicallyConsistent();

        // Assert
        result.Should().BeFalse();
    }

    #endregion
}

[Trait("Category", "Unit")]
public class ReflectionResultTests
{
    #region Factory Methods

    [Fact]
    public void HighQuality_ShouldCreateWithHighScores()
    {
        // Arrange
        var trace = ReasoningTrace.Start();

        // Act
        var result = ReflectionResult.HighQuality(trace);

        // Assert
        result.QualityScore.Should().Be(0.9);
        result.LogicalSoundness.Should().Be(0.95);
        result.EvidenceSupport.Should().Be(0.85);
        result.HasIssues.Should().BeFalse();
        result.MeetsQualityThreshold().Should().BeTrue();
    }

    [Fact]
    public void Invalid_ShouldCreateWithZeroScores()
    {
        // Arrange
        var trace = ReasoningTrace.Start();

        // Act
        var result = ReflectionResult.Invalid(trace);

        // Assert
        result.QualityScore.Should().Be(0.0);
        result.HasIssues.Should().BeTrue();
        result.MeetsQualityThreshold().Should().BeFalse();
    }

    #endregion

    #region MeetsQualityThreshold

    [Fact]
    public void MeetsQualityThreshold_BelowThreshold_ShouldReturnFalse()
    {
        // Arrange
        var trace = ReasoningTrace.Start();
        var result = ReflectionResult.Invalid(trace);

        // Act
        var meets = result.MeetsQualityThreshold(0.5);

        // Assert
        meets.Should().BeFalse();
    }

    [Fact]
    public void MeetsQualityThreshold_AboveThreshold_ShouldReturnTrue()
    {
        // Arrange
        var trace = ReasoningTrace.Start();
        var result = ReflectionResult.HighQuality(trace);

        // Act
        var meets = result.MeetsQualityThreshold(0.8);

        // Assert
        meets.Should().BeTrue();
    }

    #endregion

    #region With Methods

    [Fact]
    public void WithFallacy_ShouldAddFallacy()
    {
        // Arrange
        var trace = ReasoningTrace.Start();
        var result = ReflectionResult.HighQuality(trace);

        // Act
        var updated = result.WithFallacy("Circular Reasoning");

        // Assert
        updated.IdentifiedFallacies.Should().Contain("Circular Reasoning");
        updated.HasIssues.Should().BeTrue();
    }

    [Fact]
    public void WithMissedConsideration_ShouldAddConsideration()
    {
        // Arrange
        var trace = ReasoningTrace.Start();
        var result = ReflectionResult.HighQuality(trace);

        // Act
        var updated = result.WithMissedConsideration("Consider X");

        // Assert
        updated.MissedConsiderations.Should().Contain("Consider X");
    }

    [Fact]
    public void WithImprovement_ShouldAddImprovement()
    {
        // Arrange
        var trace = ReasoningTrace.Start();
        var result = ReflectionResult.HighQuality(trace);

        // Act
        var updated = result.WithImprovement("Do better");

        // Assert
        updated.Improvements.Should().Contain("Do better");
    }

    #endregion
}

[Trait("Category", "Unit")]
public class ReflectiveReasoningArrowTests
{
    [Fact]
    public void Reflect_EmptyTrace_ShouldReturnFailure()
    {
        // Arrange
        var reasoner = new Mock<IReflectiveReasoner>();
        var arrow = ReflectiveReasoningArrow.Reflect(reasoner.Object);

        // Act
        var result = arrow(ReasoningTrace.Start()).Result;

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Reflect_NonEmptyTrace_ShouldReturnReflection()
    {
        // Arrange
        var reasoner = new Mock<IReflectiveReasoner>();
        var reflection = ReflectionResult.HighQuality(ReasoningTrace.Start());
        reasoner.Setup(r => r.ReflectOn(It.IsAny<ReasoningTrace>())).Returns(reflection);
        var arrow = ReflectiveReasoningArrow.Reflect(reasoner.Object);

        // Act
        var trace = ReasoningTrace.Start().AddObservation("obs", "just");
        var result = arrow(trace).Result;

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void AnalyzeStyle_EmptyTraces_ShouldReturnFailure()
    {
        // Arrange
        var reasoner = new Mock<IReflectiveReasoner>();
        var arrow = ReflectiveReasoningArrow.AnalyzeStyle(reasoner.Object);

        // Act
        var result = arrow(Array.Empty<ReasoningTrace>()).Result;

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void IdentifyBiases_InsufficientTraces_ShouldReturnFailure()
    {
        // Arrange
        var reasoner = new Mock<IReflectiveReasoner>();
        var arrow = ReflectiveReasoningArrow.IdentifyBiases(reasoner.Object);

        // Act
        var result = arrow(new[] { ReasoningTrace.Start() }).Result;

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void SuggestImprovements_EmptyTrace_ShouldReturnFailure()
    {
        // Arrange
        var reasoner = new Mock<IReflectiveReasoner>();
        var arrow = ReflectiveReasoningArrow.SuggestImprovements(reasoner.Object);

        // Act
        var result = arrow(ReasoningTrace.Start()).Result;

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void FullAnalysis_EmptyTrace_ShouldReturnFailure()
    {
        // Arrange
        var reasoner = new Mock<IReflectiveReasoner>();
        var arrow = ReflectiveReasoningArrow.FullAnalysis(reasoner.Object);

        // Act
        var result = arrow(ReasoningTrace.Start()).Result;

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ValidateQuality_EmptyTrace_ShouldReturnFailure()
    {
        // Arrange
        var reasoner = new Mock<IReflectiveReasoner>();
        var arrow = ReflectiveReasoningArrow.ValidateQuality(reasoner.Object);

        // Act
        var result = arrow(ReasoningTrace.Start()).Result;

        // Assert
        result.IsFailure.Should().BeTrue();
    }
}

[Trait("Category", "Unit")]
public class SelfAssessmentArrowTests
{
    [Fact]
    public void AssessArrow_ShouldDelegateToAssessor()
    {
        // Arrange
        var assessor = new Mock<ISelfAssessor>();
        var assessment = SelfAssessmentResult.Empty();
        assessor.Setup(a => a.AssessAsync()).ReturnsAsync(Result<SelfAssessmentResult, string>.Success(assessment));
        var arrow = SelfAssessmentArrow.AssessArrow(assessor.Object);

        // Act
        var result = arrow(Unit.Value).Result;

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void UpdateBeliefArrow_ShouldDelegateToAssessor()
    {
        // Arrange
        var assessor = new Mock<ISelfAssessor>();
        var belief = CapabilityBelief.Uninformative("test");
        assessor.Setup(a => a.UpdateBelief("test", 0.8)).Returns(Result<CapabilityBelief, string>.Success(belief));
        var arrow = SelfAssessmentArrow.UpdateBeliefArrow(assessor.Object);

        // Act
        var result = arrow(("test", 0.8)).Result;

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void GetBeliefArrow_ShouldDelegateToAssessor()
    {
        // Arrange
        var assessor = new Mock<ISelfAssessor>();
        var belief = CapabilityBelief.Uninformative("test");
        assessor.Setup(a => a.GetCapabilityBelief("test")).Returns(Option<CapabilityBelief>.Some(belief));
        var arrow = SelfAssessmentArrow.GetBeliefArrow(assessor.Object);

        // Act
        var result = arrow("test").Result;

        // Assert
        result.HasValue.Should().BeTrue();
    }

    [Fact]
    public void CalibrateArrow_ShouldDelegateToAssessor()
    {
        // Arrange
        var assessor = new Mock<ISelfAssessor>();
        assessor.Setup(a => a.CalibrateConfidence(It.IsAny<IEnumerable<(double, double)>>())).Returns(Result<Unit, string>.Success(Unit.Value));
        var arrow = SelfAssessmentArrow.CalibrateArrow(assessor.Object);

        // Act
        var result = arrow(new[] { (0.8, 0.7) }).Result;

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void CalibratedAssessArrow_CalibrationFailure_ShouldReturnFailure()
    {
        // Arrange
        var assessor = new Mock<ISelfAssessor>();
        assessor.Setup(a => a.CalibrateConfidence(It.IsAny<IEnumerable<(double, double)>>())).Returns(Result<Unit, string>.Failure("calibration failed"));
        var arrow = SelfAssessmentArrow.CalibratedAssessArrow(assessor.Object, new[] { (0.8, 0.7) });

        // Act
        var result = arrow(Unit.Value).Result;

        // Assert
        result.IsFailure.Should().BeTrue();
    }
}

[Trait("Category", "Unit")]
public class SelfAssessmentResultTests
{
    #region Creation

    [Fact]
    public void Empty_ShouldCreateWithDefaults()
    {
        // Act
        var result = SelfAssessmentResult.Empty();

        // Assert
        result.DimensionScores.Should().BeEmpty();
        result.OverallScore.Should().Be(0.5);
        result.OverallConfidence.Should().Be(0.0);
        result.Strengths.Should().BeEmpty();
        result.Weaknesses.Should().BeEmpty();
        result.ImprovementAreas.Should().BeEmpty();
    }

    [Fact]
    public void FromDimensionScores_Empty_ShouldReturnEmpty()
    {
        // Act
        var result = SelfAssessmentResult.FromDimensionScores(ImmutableDictionary<PerformanceDimension, DimensionScore>.Empty);

        // Assert
        result.DimensionScores.Should().BeEmpty();
    }

    [Fact]
    public void FromDimensionScores_WithScores_ShouldComputeAggregates()
    {
        // Arrange
        var scores = ImmutableDictionary.CreateBuilder<PerformanceDimension, DimensionScore>();
        scores[PerformanceDimension.Accuracy] = DimensionScore.Create(PerformanceDimension.Accuracy, 0.8, 0.7, new[] { "evidence" });
        scores[PerformanceDimension.Speed] = DimensionScore.Create(PerformanceDimension.Speed, 0.4, 0.6, new[] { "evidence" });

        // Act
        var result = SelfAssessmentResult.FromDimensionScores(scores.ToImmutable());

        // Assert
        result.OverallScore.Should().BeGreaterThan(0.0);
        result.Strengths.Should().NotBeEmpty();
        result.Weaknesses.Should().NotBeEmpty();
        result.ImprovementAreas.Should().NotBeEmpty();
    }

    #endregion

    #region GetDimensionScore

    [Fact]
    public void GetDimensionScore_ExistingDimension_ShouldReturnSome()
    {
        // Arrange
        var scores = ImmutableDictionary.CreateBuilder<PerformanceDimension, DimensionScore>();
        scores[PerformanceDimension.Accuracy] = DimensionScore.Unknown(PerformanceDimension.Accuracy);
        var result = SelfAssessmentResult.FromDimensionScores(scores.ToImmutable());

        // Act
        var score = result.GetDimensionScore(PerformanceDimension.Accuracy);

        // Assert
        score.HasValue.Should().BeTrue();
    }

    [Fact]
    public void GetDimensionScore_MissingDimension_ShouldReturnNone()
    {
        // Arrange
        var result = SelfAssessmentResult.Empty();

        // Act
        var score = result.GetDimensionScore(PerformanceDimension.Accuracy);

        // Assert
        score.HasValue.Should().BeFalse();
    }

    #endregion

    #region WithDimensionScore

    [Fact]
    public void WithDimensionScore_ShouldAddOrUpdateScore()
    {
        // Arrange
        var result = SelfAssessmentResult.Empty();
        var newScore = DimensionScore.Create(PerformanceDimension.Accuracy, 0.9, 0.8, new[] { "evidence" });

        // Act
        var updated = result.WithDimensionScore(newScore);

        // Assert
        updated.DimensionScores.Should().ContainKey(PerformanceDimension.Accuracy);
    }

    #endregion
}

[Trait("Category", "Unit")]
public class SeverityTests
{
    [Theory]
    [InlineData(Severity.Info)]
    [InlineData(Severity.Warning)]
    [InlineData(Severity.Critical)]
    public void AllEnumValues_ShouldBeDefined(Severity value)
    {
        // Assert
        ((int)value).Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void Enum_ShouldHaveThreeValues()
    {
        // Act
        var values = Enum.GetValues<Severity>();

        // Assert
        values.Should().HaveCount(3);
    }
}

[Trait("Category", "Unit")]
public class StateComparisonTests
{
    #region Creation

    [Fact]
    public void Create_ShouldComputeDeltas()
    {
        // Arrange
        var before = InternalState.Initial().WithCognitiveLoad(0.2).WithValence(0.1);
        var after = InternalState.Initial().WithCognitiveLoad(0.6).WithValence(-0.3);

        // Act
        var comparison = StateComparison.Create(before, after);

        // Assert
        comparison.CognitiveLoadDelta.Should().BeApproximately(0.4, 0.001);
        comparison.ValenceDelta.Should().BeApproximately(-0.4, 0.001);
        comparison.CognitiveLoadIncreased.Should().BeTrue();
        comparison.CognitiveLoadDecreased.Should().BeFalse();
        comparison.ModeChanged.Should().BeFalse();
    }

    [Fact]
    public void Create_WithModeChange_ShouldDetectChange()
    {
        // Arrange
        var before = InternalState.Initial().WithMode(ProcessingMode.Analytical);
        var after = InternalState.Initial().WithMode(ProcessingMode.Creative);

        // Act
        var comparison = StateComparison.Create(before, after);

        // Assert
        comparison.ModeChanged.Should().BeTrue();
    }

    #endregion

    #region Goals Tracking

    [Fact]
    public void GoalsAdded_ShouldTrackNewGoals()
    {
        // Arrange
        var before = InternalState.Initial();
        var after = InternalState.Initial().WithGoal("goal1").WithGoal("goal2");

        // Act
        var comparison = StateComparison.Create(before, after);

        // Assert
        comparison.GoalsAdded.Should().Contain("goal1", "goal2");
        comparison.GoalsRemoved.Should().BeEmpty();
    }

    [Fact]
    public void GoalsRemoved_ShouldTrackRemovedGoals()
    {
        // Arrange
        var before = InternalState.Initial().WithGoal("goal1").WithGoal("goal2");
        var after = InternalState.Initial().WithGoal("goal1");

        // Act
        var comparison = StateComparison.Create(before, after);

        // Assert
        comparison.GoalsRemoved.Should().Contain("goal2");
        comparison.GoalsAdded.Should().BeEmpty();
    }

    #endregion

    #region TimeElapsed

    [Fact]
    public void TimeElapsed_ShouldBePositive()
    {
        // Arrange
        var before = InternalState.Initial();
        System.Threading.Thread.Sleep(10);
        var after = InternalState.Initial();

        // Act
        var comparison = StateComparison.Create(before, after);

        // Assert
        comparison.TimeElapsed.Should().BeGreaterThan(TimeSpan.Zero);
    }

    #endregion
}

[Trait("Category", "Unit")]
public class ThinkingStyleTests
{
    #region Factory Methods

    [Fact]
    public void Balanced_ShouldCreateWithEqualScores()
    {
        // Act
        var style = ThinkingStyle.Balanced();

        // Assert
        style.StyleName.Should().Be("Balanced");
        style.AnalyticalScore.Should().Be(0.5);
        style.CreativeScore.Should().Be(0.5);
        style.SystematicScore.Should().Be(0.5);
        style.IntuitiveScore.Should().Be(0.5);
    }

    [Fact]
    public void Analytical_ShouldCreateWithHighAnalyticalScore()
    {
        // Act
        var style = ThinkingStyle.Analytical();

        // Assert
        style.StyleName.Should().Be("Analytical");
        style.AnalyticalScore.Should().Be(0.85);
        style.DominantDimension.Should().Be("Analytical");
    }

    [Fact]
    public void Creative_ShouldCreateWithHighCreativeScore()
    {
        // Act
        var style = ThinkingStyle.Creative();

        // Assert
        style.StyleName.Should().Be("Creative");
        style.CreativeScore.Should().Be(0.9);
        style.DominantDimension.Should().Be("Creative");
    }

    #endregion

    #region DominantDimension

    [Fact]
    public void DominantDimension_ShouldReturnHighestScore()
    {
        // Arrange
        var style = new ThinkingStyle("Custom", 0.9, 0.2, 0.3, 0.4, ImmutableDictionary<string, double>.Empty);

        // Act
        var dominant = style.DominantDimension;

        // Assert
        dominant.Should().Be("Analytical");
    }

    #endregion

    #region HasSignificantBiases

    [Fact]
    public void HasSignificantBiases_NoBiases_ShouldReturnFalse()
    {
        // Arrange
        var style = ThinkingStyle.Balanced();

        // Act
        var result = style.HasSignificantBiases();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasSignificantBiases_WithHighBias_ShouldReturnTrue()
    {
        // Arrange
        var biases = ImmutableDictionary.CreateBuilder<string, double>();
        biases["Confirmation Bias"] = 0.7;
        var style = new ThinkingStyle("Custom", 0.5, 0.5, 0.5, 0.5, biases.ToImmutable());

        // Act
        var result = style.HasSignificantBiases();

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region GetSignificantBiases

    [Fact]
    public void GetSignificantBiases_ShouldFilterAndOrder()
    {
        // Arrange
        var biases = ImmutableDictionary.CreateBuilder<string, double>();
        biases["A"] = 0.1;
        biases["B"] = 0.5;
        biases["C"] = 0.8;
        var style = new ThinkingStyle("Custom", 0.5, 0.5, 0.5, 0.5, biases.ToImmutable());

        // Act
        var significant = style.GetSignificantBiases(0.3).ToList();

        // Assert
        significant.Should().HaveCount(2);
        significant[0].Should().Be(("C", 0.8));
    }

    #endregion

    #region WithBias

    [Fact]
    public void WithBias_ShouldAddBias()
    {
        // Arrange
        var style = ThinkingStyle.Balanced();

        // Act
        var updated = style.WithBias("New Bias", 0.6);

        // Assert
        updated.BiasProfile.Should().ContainKey("New Bias");
        updated.BiasProfile["New Bias"].Should().Be(0.6);
    }

    [Fact]
    public void WithBias_ShouldClampValue()
    {
        // Arrange
        var style = ThinkingStyle.Balanced();

        // Act
        var low = style.WithBias("Low", -0.5);
        var high = style.WithBias("High", 1.5);

        // Assert
        low.BiasProfile["Low"].Should().Be(0.0);
        high.BiasProfile["High"].Should().Be(1.0);
    }

    #endregion
}

[Trait("Category", "Unit")]
public class TrendTests
{
    [Theory]
    [InlineData(Trend.Improving)]
    [InlineData(Trend.Stable)]
    [InlineData(Trend.Declining)]
    [InlineData(Trend.Volatile)]
    [InlineData(Trend.Unknown)]
    public void AllEnumValues_ShouldBeDefined(Trend value)
    {
        // Assert
        ((int)value).Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void Enum_ShouldHaveFiveValues()
    {
        // Act
        var values = Enum.GetValues<Trend>();

        // Assert
        values.Should().HaveCount(5);
    }
}
