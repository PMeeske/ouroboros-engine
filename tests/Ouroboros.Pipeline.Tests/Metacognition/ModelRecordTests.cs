using FluentAssertions;
using Ouroboros.Pipeline.Metacognition;

namespace Ouroboros.Tests.Metacognition;

[Trait("Category", "Unit")]
public sealed class IntrospectionReportTests
{
    private static InternalState CreateTestState() => InternalState.Initial();

    [Fact]
    public void Empty_CreatesReportWithNoObservationsOrAnomalies()
    {
        // Act
        var report = IntrospectionReport.Empty(CreateTestState());

        // Assert
        report.Observations.Should().BeEmpty();
        report.Anomalies.Should().BeEmpty();
        report.Recommendations.Should().BeEmpty();
        report.SelfAssessmentScore.Should().Be(0.5);
        report.HasAnomalies.Should().BeFalse();
        report.HasRecommendations.Should().BeFalse();
    }

    [Fact]
    public void WithObservation_AddsObservation()
    {
        // Arrange
        var report = IntrospectionReport.Empty(CreateTestState());

        // Act
        var updated = report.WithObservation("High load detected");

        // Assert
        updated.Observations.Should().HaveCount(1);
        updated.Observations.Should().Contain("High load detected");
    }

    [Fact]
    public void WithAnomaly_AddsAnomalyAndSetsFlag()
    {
        // Arrange
        var report = IntrospectionReport.Empty(CreateTestState());

        // Act
        var updated = report.WithAnomaly("Memory overflow risk");

        // Assert
        updated.Anomalies.Should().HaveCount(1);
        updated.HasAnomalies.Should().BeTrue();
    }

    [Fact]
    public void WithRecommendation_AddsRecommendationAndSetsFlag()
    {
        // Arrange
        var report = IntrospectionReport.Empty(CreateTestState());

        // Act
        var updated = report.WithRecommendation("Reduce task complexity");

        // Assert
        updated.Recommendations.Should().HaveCount(1);
        updated.HasRecommendations.Should().BeTrue();
    }
}

[Trait("Category", "Unit")]
public sealed class MonitoringAlertTests
{
    [Fact]
    public void HighPriority_CreatesPriority8Alert()
    {
        // Act
        var alert = MonitoringAlert.HighPriority(
            "ErrorRate", "High errors", Array.Empty<CognitiveEvent>(), "Investigate");

        // Assert
        alert.Priority.Should().Be(8);
        alert.AlertType.Should().Be("ErrorRate");
        alert.Message.Should().Be("High errors");
        alert.RecommendedAction.Should().Be("Investigate");
        alert.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void MediumPriority_CreatesPriority5Alert()
    {
        // Act
        var alert = MonitoringAlert.MediumPriority(
            "ConfusionRate", "Moderate confusion", Array.Empty<CognitiveEvent>(), "Provide context");

        // Assert
        alert.Priority.Should().Be(5);
    }

    [Fact]
    public void LowPriority_CreatesPriority2Alert()
    {
        // Act
        var alert = MonitoringAlert.LowPriority(
            "Info", "Minor issue", Array.Empty<CognitiveEvent>(), "Monitor");

        // Assert
        alert.Priority.Should().Be(2);
    }

    [Fact]
    public void Validate_WithValidValues_ReturnsSuccess()
    {
        // Arrange
        var alert = MonitoringAlert.HighPriority(
            "Test", "Valid", Array.Empty<CognitiveEvent>(), "Action");

        // Act
        var result = alert.Validate();

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithPriorityOutOfRange_ReturnsFailure()
    {
        // Arrange
        var alert = new MonitoringAlert(
            Guid.NewGuid(), "Test", "Msg",
            ImmutableList<CognitiveEvent>.Empty, "Action", 11, DateTime.UtcNow);

        // Act
        var result = alert.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyAlertType_ReturnsFailure()
    {
        // Arrange
        var alert = new MonitoringAlert(
            Guid.NewGuid(), "", "Msg",
            ImmutableList<CognitiveEvent>.Empty, "Action", 5, DateTime.UtcNow);

        // Act
        var result = alert.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyMessage_ReturnsFailure()
    {
        // Arrange
        var alert = new MonitoringAlert(
            Guid.NewGuid(), "Type", "",
            ImmutableList<CognitiveEvent>.Empty, "Action", 5, DateTime.UtcNow);

        // Act
        var result = alert.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
    }
}

[Trait("Category", "Unit")]
public sealed class ReasoningStepTests
{
    [Fact]
    public void Observation_CreatesObservationWithNoDependencies()
    {
        // Act
        var step = ReasoningStep.Observation(1, "Observed data", "Relevant to task");

        // Assert
        step.StepNumber.Should().Be(1);
        step.StepType.Should().Be(ReasoningStepType.Observation);
        step.Content.Should().Be("Observed data");
        step.Justification.Should().Be("Relevant to task");
        step.Dependencies.Should().BeEmpty();
    }

    [Fact]
    public void Inference_CreatesInferenceWithDependencies()
    {
        // Act
        var step = ReasoningStep.Inference(3, "Inferred result", "Based on obs", 1, 2);

        // Assert
        step.StepType.Should().Be(ReasoningStepType.Inference);
        step.Dependencies.Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public void Hypothesis_CreatesHypothesisStep()
    {
        // Act
        var step = ReasoningStep.Hypothesis(2, "Maybe X", "Worth exploring", 1);

        // Assert
        step.StepType.Should().Be(ReasoningStepType.Hypothesis);
        step.Dependencies.Should().Contain(1);
    }

    [Fact]
    public void Conclusion_CreatesConclusionStep()
    {
        // Act
        var step = ReasoningStep.Conclusion(4, "Therefore Y", "Follows from all", 1, 2, 3);

        // Assert
        step.StepType.Should().Be(ReasoningStepType.Conclusion);
        step.Dependencies.Should().HaveCount(3);
    }

    [Fact]
    public void WithDependency_AddsDependency()
    {
        // Arrange
        var step = ReasoningStep.Observation(1, "obs", "just");

        // Act
        var updated = step.WithDependency(0); // unusual but valid call

        // Assert
        updated.Dependencies.Should().Contain(0);
    }

    [Fact]
    public void HasValidDependencies_WithValidDependencies_ReturnsTrue()
    {
        // Arrange - step 3 depends on 1 and 2
        var step = ReasoningStep.Inference(3, "inf", "just", 1, 2);

        // Act & Assert
        step.HasValidDependencies().Should().BeTrue();
    }

    [Fact]
    public void HasValidDependencies_WithSelfReference_ReturnsFalse()
    {
        // Arrange - step depends on itself
        var step = new ReasoningStep(2, ReasoningStepType.Inference, "inf", "just",
            DateTime.UtcNow, ImmutableList.Create(2));

        // Act & Assert
        step.HasValidDependencies().Should().BeFalse();
    }

    [Fact]
    public void HasValidDependencies_WithForwardReference_ReturnsFalse()
    {
        // Arrange - step 2 depends on step 5 (which doesn't exist yet)
        var step = new ReasoningStep(2, ReasoningStepType.Inference, "inf", "just",
            DateTime.UtcNow, ImmutableList.Create(5));

        // Act & Assert
        step.HasValidDependencies().Should().BeFalse();
    }

    [Fact]
    public void HasValidDependencies_WithNoDependencies_ReturnsTrue()
    {
        // Arrange
        var step = ReasoningStep.Observation(1, "obs", "just");

        // Act & Assert
        step.HasValidDependencies().Should().BeTrue();
    }
}

[Trait("Category", "Unit")]
public sealed class ReasoningTraceTests
{
    [Fact]
    public void Start_CreatesEmptyActiveTrace()
    {
        // Act
        var trace = ReasoningTrace.Start();

        // Assert
        trace.Id.Should().NotBeEmpty();
        trace.Steps.Should().BeEmpty();
        trace.FinalConclusion.Should().BeNull();
        trace.Confidence.Should().Be(0.0);
        trace.WasSuccessful.Should().BeFalse();
        trace.IsActive.Should().BeTrue();
        trace.EndTime.Should().BeNull();
        trace.Duration.Should().BeNull();
        trace.NextStepNumber.Should().Be(1);
    }

    [Fact]
    public void StartWithId_UsesProvidedId()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var trace = ReasoningTrace.StartWithId(id);

        // Assert
        trace.Id.Should().Be(id);
    }

    [Fact]
    public void WithStep_AddsStepToTrace()
    {
        // Arrange
        var trace = ReasoningTrace.Start();
        var step = ReasoningStep.Observation(1, "data", "reason");

        // Act
        var updated = trace.WithStep(step);

        // Assert
        updated.Steps.Should().HaveCount(1);
        updated.NextStepNumber.Should().Be(2);
        trace.Steps.Should().BeEmpty(); // immutable
    }

    [Fact]
    public void AddObservation_AddsObservationStep()
    {
        // Act
        var trace = ReasoningTrace.Start().AddObservation("data", "reason");

        // Assert
        trace.Steps.Should().HaveCount(1);
        trace.Steps[0].StepType.Should().Be(ReasoningStepType.Observation);
        trace.Steps[0].StepNumber.Should().Be(1);
    }

    [Fact]
    public void AddInference_AddsInferenceStep()
    {
        // Act
        var trace = ReasoningTrace.Start()
            .AddObservation("obs", "reason")
            .AddInference("inf", "based on obs", 1);

        // Assert
        trace.Steps.Should().HaveCount(2);
        trace.Steps[1].StepType.Should().Be(ReasoningStepType.Inference);
    }

    [Fact]
    public void AddHypothesis_AddsHypothesisStep()
    {
        // Act
        var trace = ReasoningTrace.Start()
            .AddObservation("obs", "reason")
            .AddHypothesis("hyp", "exploring", 1);

        // Assert
        trace.Steps[1].StepType.Should().Be(ReasoningStepType.Hypothesis);
    }

    [Fact]
    public void Complete_SetsEndTimeAndConclusion()
    {
        // Arrange
        var trace = ReasoningTrace.Start()
            .AddObservation("data", "reason");

        // Act
        var completed = trace.Complete("Final answer", 0.85);

        // Assert
        completed.IsActive.Should().BeFalse();
        completed.EndTime.Should().NotBeNull();
        completed.FinalConclusion.Should().Be("Final answer");
        completed.Confidence.Should().Be(0.85);
        completed.WasSuccessful.Should().BeTrue();
        completed.Duration.Should().NotBeNull();
        // Conclusion step added
        completed.Steps.Should().HaveCount(2);
        completed.Steps[^1].StepType.Should().Be(ReasoningStepType.Conclusion);
    }

    [Fact]
    public void Complete_ClampsConfidence()
    {
        // Act
        var completed = ReasoningTrace.Start().Complete("answer", 1.5);

        // Assert
        completed.Confidence.Should().Be(1.0);
    }

    [Fact]
    public void Fail_SetsFailedState()
    {
        // Arrange
        var trace = ReasoningTrace.Start().AddObservation("obs", "just");

        // Act
        var failed = trace.Fail("Could not determine");

        // Assert
        failed.IsActive.Should().BeFalse();
        failed.WasSuccessful.Should().BeFalse();
        failed.Confidence.Should().Be(0.0);
        failed.FinalConclusion.Should().StartWith("Failed:");
    }

    [Fact]
    public void GetStepsByType_FiltersCorrectly()
    {
        // Arrange
        var trace = ReasoningTrace.Start()
            .AddObservation("obs1", "r1")
            .AddObservation("obs2", "r2")
            .AddInference("inf1", "just", 1, 2);

        // Act
        var observations = trace.GetStepsByType(ReasoningStepType.Observation).ToList();

        // Assert
        observations.Should().HaveCount(2);
    }

    [Fact]
    public void IsLogicallyConsistent_WithValidTrace_ReturnsTrue()
    {
        // Arrange
        var trace = ReasoningTrace.Start()
            .AddObservation("obs", "r")
            .AddInference("inf", "just", 1);

        // Act & Assert
        trace.IsLogicallyConsistent().Should().BeTrue();
    }

    [Fact]
    public void IsLogicallyConsistent_WithInvalidDependencies_ReturnsFalse()
    {
        // Arrange - step 1 with forward dependency to step 5
        var badStep = new ReasoningStep(1, ReasoningStepType.Inference, "inf", "just",
            DateTime.UtcNow, ImmutableList.Create(5));
        var trace = ReasoningTrace.Start().WithStep(badStep);

        // Act & Assert
        trace.IsLogicallyConsistent().Should().BeFalse();
    }
}

[Trait("Category", "Unit")]
public sealed class ReflectionResultTests
{
    private static ReasoningTrace CreateTestTrace() =>
        ReasoningTrace.Start()
            .AddObservation("obs", "reason")
            .AddInference("inf", "just", 1)
            .Complete("conclusion", 0.8);

    [Fact]
    public void HighQuality_ReturnsHighScores()
    {
        // Arrange
        var trace = CreateTestTrace();

        // Act
        var result = ReflectionResult.HighQuality(trace);

        // Assert
        result.QualityScore.Should().Be(0.9);
        result.LogicalSoundness.Should().Be(0.95);
        result.EvidenceSupport.Should().Be(0.85);
        result.IdentifiedFallacies.Should().BeEmpty();
        result.MissedConsiderations.Should().BeEmpty();
        result.HasIssues.Should().BeFalse();
    }

    [Fact]
    public void Invalid_ReturnsZeroScores()
    {
        // Arrange
        var trace = ReasoningTrace.Start();

        // Act
        var result = ReflectionResult.Invalid(trace);

        // Assert
        result.QualityScore.Should().Be(0.0);
        result.IdentifiedFallacies.Should().NotBeEmpty();
        result.HasIssues.Should().BeTrue();
    }

    [Fact]
    public void MeetsQualityThreshold_WithHighScore_ReturnsTrue()
    {
        // Arrange
        var result = ReflectionResult.HighQuality(CreateTestTrace());

        // Act & Assert
        result.MeetsQualityThreshold(0.7).Should().BeTrue();
    }

    [Fact]
    public void MeetsQualityThreshold_WithLowScore_ReturnsFalse()
    {
        // Arrange
        var result = ReflectionResult.Invalid(ReasoningTrace.Start());

        // Act & Assert
        result.MeetsQualityThreshold(0.7).Should().BeFalse();
    }

    [Fact]
    public void WithFallacy_AddsFallacy()
    {
        // Arrange
        var result = ReflectionResult.HighQuality(CreateTestTrace());

        // Act
        var updated = result.WithFallacy("Circular reasoning");

        // Assert
        updated.IdentifiedFallacies.Should().Contain("Circular reasoning");
        updated.HasIssues.Should().BeTrue();
    }

    [Fact]
    public void WithMissedConsideration_AddsConsideration()
    {
        // Arrange
        var result = ReflectionResult.HighQuality(CreateTestTrace());

        // Act
        var updated = result.WithMissedConsideration("Alternative explanation");

        // Assert
        updated.MissedConsiderations.Should().Contain("Alternative explanation");
    }

    [Fact]
    public void WithImprovement_AddsImprovement()
    {
        // Arrange
        var result = ReflectionResult.HighQuality(CreateTestTrace());

        // Act
        var updated = result.WithImprovement("Add more observations");

        // Assert
        updated.Improvements.Should().Contain("Add more observations");
    }
}

[Trait("Category", "Unit")]
public sealed class MetacognitiveAnalysisTests
{
    private static ReasoningTrace CreateTestTrace() =>
        ReasoningTrace.Start()
            .AddObservation("obs", "reason")
            .Complete("conclusion", 0.8);

    [Theory]
    [InlineData(0.95, "Excellent reasoning quality")]
    [InlineData(0.75, "Good reasoning quality")]
    [InlineData(0.55, "Moderate reasoning quality - improvements recommended")]
    [InlineData(0.35, "Poor reasoning quality - significant improvements needed")]
    [InlineData(0.15, "Very poor reasoning quality - fundamental issues detected")]
    public void QualitySummary_ReturnsCorrectDescription(double qualityScore, string expectedSummary)
    {
        // Arrange
        var trace = CreateTestTrace();
        var reflection = new ReflectionResult(
            trace, qualityScore, 0.5, 0.5,
            ImmutableList<string>.Empty, ImmutableList<string>.Empty,
            ImmutableList<string>.Empty, ImmutableList<string>.Empty);
        var analysis = new MetacognitiveAnalysis(
            trace, reflection, ThinkingStyle.Balanced(),
            ImmutableList<string>.Empty, DateTime.UtcNow);

        // Act & Assert
        analysis.QualitySummary.Should().Be(expectedSummary);
    }

    [Fact]
    public void IsAcceptable_WithHighQuality_ReturnsTrue()
    {
        // Arrange
        var trace = CreateTestTrace();
        var reflection = ReflectionResult.HighQuality(trace);
        var analysis = new MetacognitiveAnalysis(
            trace, reflection, ThinkingStyle.Balanced(),
            ImmutableList<string>.Empty, DateTime.UtcNow);

        // Act & Assert
        analysis.IsAcceptable.Should().BeTrue();
    }

    [Fact]
    public void PriorityImprovements_ReturnsAtMostThree()
    {
        // Arrange
        var trace = CreateTestTrace();
        var reflection = ReflectionResult.HighQuality(trace);
        var improvements = ImmutableList.Create("A", "B", "C", "D", "E");
        var analysis = new MetacognitiveAnalysis(
            trace, reflection, ThinkingStyle.Balanced(), improvements, DateTime.UtcNow);

        // Act
        var priority = analysis.PriorityImprovements.ToList();

        // Assert
        priority.Should().HaveCount(3);
        priority.Should().BeEquivalentTo(new[] { "A", "B", "C" });
    }
}

[Trait("Category", "Unit")]
public sealed class StateComparisonTests
{
    [Fact]
    public void Create_ComputesCognitiveLoadDelta()
    {
        // Arrange
        var before = InternalState.Initial().WithCognitiveLoad(0.3);
        var after = InternalState.Initial().WithCognitiveLoad(0.8);

        // Act
        var comparison = StateComparison.Create(before, after);

        // Assert
        comparison.CognitiveLoadDelta.Should().BeApproximately(0.5, 0.001);
        comparison.CognitiveLoadIncreased.Should().BeTrue();
        comparison.CognitiveLoadDecreased.Should().BeFalse();
    }

    [Fact]
    public void Create_ComputesValenceDelta()
    {
        // Arrange
        var before = InternalState.Initial().WithValence(0.5);
        var after = InternalState.Initial().WithValence(-0.3);

        // Act
        var comparison = StateComparison.Create(before, after);

        // Assert
        comparison.ValenceDelta.Should().BeApproximately(-0.8, 0.001);
    }

    [Fact]
    public void ModeChanged_WhenModesAreDifferent_ReturnsTrue()
    {
        // Arrange
        var before = InternalState.Initial().WithMode(ProcessingMode.Reactive);
        var after = InternalState.Initial().WithMode(ProcessingMode.Analytical);

        // Act
        var comparison = StateComparison.Create(before, after);

        // Assert
        comparison.ModeChanged.Should().BeTrue();
    }

    [Fact]
    public void ModeChanged_WhenModesSame_ReturnsFalse()
    {
        // Arrange
        var before = InternalState.Initial();
        var after = InternalState.Initial();

        // Act
        var comparison = StateComparison.Create(before, after);

        // Assert
        comparison.ModeChanged.Should().BeFalse();
    }

    [Fact]
    public void GoalsAdded_ReturnsNewGoals()
    {
        // Arrange
        var before = InternalState.Initial().WithGoal("A");
        var after = InternalState.Initial().WithGoal("A").WithGoal("B");

        // Act
        var comparison = StateComparison.Create(before, after);

        // Assert
        comparison.GoalsAdded.Should().Contain("B");
        comparison.GoalsRemoved.Should().BeEmpty();
    }

    [Fact]
    public void GoalsRemoved_ReturnsRemovedGoals()
    {
        // Arrange
        var before = InternalState.Initial().WithGoal("A").WithGoal("B");
        var after = InternalState.Initial().WithGoal("A");

        // Act
        var comparison = StateComparison.Create(before, after);

        // Assert
        comparison.GoalsRemoved.Should().Contain("B");
        comparison.GoalsAdded.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithSignificantLoadIncrease_InterpretationMentionsLoad()
    {
        // Arrange
        var before = InternalState.Initial().WithCognitiveLoad(0.1);
        var after = InternalState.Initial().WithCognitiveLoad(0.8);

        // Act
        var comparison = StateComparison.Create(before, after);

        // Assert
        comparison.Interpretation.Should().Contain("cognitive load");
    }

    [Fact]
    public void Create_WithNoChanges_InterpretationSaysNoChanges()
    {
        // Arrange
        var state = InternalState.Initial();

        // Act
        var comparison = StateComparison.Create(state, state);

        // Assert
        comparison.Interpretation.Should().Contain("No significant state changes");
    }
}

[Trait("Category", "Unit")]
public sealed class CapabilityBeliefTests
{
    [Fact]
    public void Uninformative_CreatesMaxUncertaintyBelief()
    {
        // Act
        var belief = CapabilityBelief.Uninformative("coding");

        // Assert
        belief.CapabilityName.Should().Be("coding");
        belief.Proficiency.Should().Be(0.5);
        belief.Uncertainty.Should().Be(1.0);
        belief.ValidationCount.Should().Be(0);
        belief.LastValidated.Should().Be(DateTime.MinValue);
    }

    [Fact]
    public void Create_ClampsValues()
    {
        // Act
        var belief = CapabilityBelief.Create("test", 1.5, -0.5);

        // Assert
        belief.Proficiency.Should().Be(1.0);
        belief.Uncertainty.Should().Be(0.0);
        belief.ValidationCount.Should().Be(1);
    }

    [Fact]
    public void WithBayesianUpdate_UpdatesProficiencyTowardEvidence()
    {
        // Arrange
        var belief = CapabilityBelief.Uninformative("coding");

        // Act
        var updated = belief.WithBayesianUpdate(0.9, 5);

        // Assert
        updated.Proficiency.Should().BeGreaterThan(0.5);
        updated.ValidationCount.Should().Be(5);
        updated.LastValidated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void WithBayesianUpdate_MultipleUpdates_ConvergesOnEvidence()
    {
        // Arrange
        var belief = CapabilityBelief.Uninformative("coding");

        // Act - repeatedly observe high success
        for (var i = 0; i < 10; i++)
        {
            belief = belief.WithBayesianUpdate(0.9, 5);
        }

        // Assert
        belief.Proficiency.Should().BeGreaterThan(0.8);
    }

    [Fact]
    public void GetCredibleInterval_ReturnsValidBounds()
    {
        // Arrange
        var belief = CapabilityBelief.Create("test", 0.7, 0.3);

        // Act
        var (lower, expected, upper) = belief.GetCredibleInterval(0.95);

        // Assert
        lower.Should().BeLessThanOrEqualTo(expected);
        upper.Should().BeGreaterThanOrEqualTo(expected);
        expected.Should().Be(0.7);
        lower.Should().BeGreaterThanOrEqualTo(0.0);
        upper.Should().BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public void GetCredibleInterval_HigherLevel_WiderInterval()
    {
        // Arrange
        var belief = CapabilityBelief.Create("test", 0.5, 0.5);

        // Act
        var interval90 = belief.GetCredibleInterval(0.90);
        var interval99 = belief.GetCredibleInterval(0.99);

        // Assert
        var width90 = interval90.Upper - interval90.Lower;
        var width99 = interval99.Upper - interval99.Lower;
        width99.Should().BeGreaterThanOrEqualTo(width90);
    }

    [Fact]
    public void Validate_WithValidValues_ReturnsSuccess()
    {
        // Act
        var result = CapabilityBelief.Create("test", 0.5, 0.5).Validate();

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyName_ReturnsFailure()
    {
        // Arrange
        var belief = new CapabilityBelief("", 0.5, 0.5, DateTime.UtcNow, 1);

        // Act
        var result = belief.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithNegativeValidationCount_ReturnsFailure()
    {
        // Arrange
        var belief = new CapabilityBelief("test", 0.5, 0.5, DateTime.UtcNow, -1);

        // Act
        var result = belief.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithProficiencyOutOfRange_ReturnsFailure()
    {
        // Arrange
        var belief = new CapabilityBelief("test", 1.5, 0.5, DateTime.UtcNow, 1);

        // Act
        var result = belief.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
    }
}

[Trait("Category", "Unit")]
public sealed class SelfAssessmentResultTests
{
    [Fact]
    public void Empty_ReturnsDefaultAssessment()
    {
        // Act
        var result = SelfAssessmentResult.Empty();

        // Assert
        result.OverallScore.Should().Be(0.5);
        result.OverallConfidence.Should().Be(0.0);
        result.DimensionScores.Should().BeEmpty();
        result.Strengths.Should().BeEmpty();
        result.Weaknesses.Should().BeEmpty();
    }

    [Fact]
    public void FromDimensionScores_WithEmptyScores_ReturnsEmpty()
    {
        // Act
        var result = SelfAssessmentResult.FromDimensionScores(
            ImmutableDictionary<PerformanceDimension, DimensionScore>.Empty);

        // Assert
        result.OverallScore.Should().Be(0.5);
    }

    [Fact]
    public void FromDimensionScores_ComputesWeightedOverallScore()
    {
        // Arrange
        var scores = ImmutableDictionary<PerformanceDimension, DimensionScore>.Empty
            .Add(PerformanceDimension.Accuracy, DimensionScore.Create(
                PerformanceDimension.Accuracy, 0.9, 0.8, new[] { "high accuracy" }))
            .Add(PerformanceDimension.Speed, DimensionScore.Create(
                PerformanceDimension.Speed, 0.5, 0.8, new[] { "moderate speed" }));

        // Act
        var result = SelfAssessmentResult.FromDimensionScores(scores);

        // Assert
        result.OverallScore.Should().BeApproximately(0.7, 0.01);
        result.DimensionScores.Should().HaveCount(2);
    }

    [Fact]
    public void FromDimensionScores_IdentifiesStrengths()
    {
        // Arrange
        var scores = ImmutableDictionary<PerformanceDimension, DimensionScore>.Empty
            .Add(PerformanceDimension.Accuracy, DimensionScore.Create(
                PerformanceDimension.Accuracy, 0.9, 0.5, new[] { "high" }));

        // Act
        var result = SelfAssessmentResult.FromDimensionScores(scores);

        // Assert
        result.Strengths.Should().NotBeEmpty();
    }

    [Fact]
    public void FromDimensionScores_IdentifiesWeaknesses()
    {
        // Arrange
        var scores = ImmutableDictionary<PerformanceDimension, DimensionScore>.Empty
            .Add(PerformanceDimension.Speed, DimensionScore.Create(
                PerformanceDimension.Speed, 0.3, 0.5, new[] { "slow" }));

        // Act
        var result = SelfAssessmentResult.FromDimensionScores(scores);

        // Assert
        result.Weaknesses.Should().NotBeEmpty();
    }

    [Fact]
    public void GetDimensionScore_WithExistingDimension_ReturnsSome()
    {
        // Arrange
        var scores = ImmutableDictionary<PerformanceDimension, DimensionScore>.Empty
            .Add(PerformanceDimension.Accuracy, DimensionScore.Create(
                PerformanceDimension.Accuracy, 0.8, 0.5, Array.Empty<string>()));
        var result = SelfAssessmentResult.FromDimensionScores(scores);

        // Act
        var score = result.GetDimensionScore(PerformanceDimension.Accuracy);

        // Assert
        score.IsSome.Should().BeTrue();
    }

    [Fact]
    public void GetDimensionScore_WithMissingDimension_ReturnsNone()
    {
        // Arrange
        var result = SelfAssessmentResult.Empty();

        // Act
        var score = result.GetDimensionScore(PerformanceDimension.Creativity);

        // Assert
        score.IsSome.Should().BeFalse();
    }

    [Fact]
    public void WithDimensionScore_RecomputesAssessment()
    {
        // Arrange
        var result = SelfAssessmentResult.Empty();
        var newScore = DimensionScore.Create(
            PerformanceDimension.Accuracy, 0.9, 0.7, new[] { "test" });

        // Act
        var updated = result.WithDimensionScore(newScore);

        // Assert
        updated.DimensionScores.Should().ContainKey(PerformanceDimension.Accuracy);
    }
}
