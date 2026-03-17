using FluentAssertions;
using Ouroboros.Pipeline.Metacognition;

namespace Ouroboros.Tests.Metacognition;

[Trait("Category", "Unit")]
public sealed class MetacognitiveReasonerTests
{
    [Fact]
    public void StartTrace_ReturnsNonEmptyGuid()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();

        // Act
        var traceId = reasoner.StartTrace();

        // Assert
        traceId.Should().NotBeEmpty();
    }

    [Fact]
    public void StartTrace_SetsActiveTrace()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();

        // Act
        reasoner.StartTrace();

        // Assert
        var activeTrace = reasoner.GetActiveTrace();
        activeTrace.IsSome.Should().BeTrue();
    }

    [Fact]
    public void GetActiveTrace_WithNoTrace_ReturnsNone()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();

        // Act
        var activeTrace = reasoner.GetActiveTrace();

        // Assert
        activeTrace.IsSome.Should().BeFalse();
    }

    [Fact]
    public void AddStep_WithActiveTrace_ReturnsSuccess()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();
        reasoner.StartTrace();

        // Act
        var result = reasoner.AddStep(
            ReasoningStepType.Observation, "Observed X", "Relevant data");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1);
    }

    [Fact]
    public void AddStep_WithNoActiveTrace_ReturnsFailure()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();

        // Act
        var result = reasoner.AddStep(
            ReasoningStepType.Observation, "data", "reason");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void AddStep_WithInvalidDependencies_ReturnsFailure()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();
        reasoner.StartTrace();

        // Act - reference step 5 which doesn't exist
        var result = reasoner.AddStep(
            ReasoningStepType.Inference, "inf", "just", 5);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void AddStep_WithValidDependencies_ReturnsSuccess()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();
        reasoner.StartTrace();
        reasoner.AddStep(ReasoningStepType.Observation, "obs1", "reason1");
        reasoner.AddStep(ReasoningStepType.Observation, "obs2", "reason2");

        // Act
        var result = reasoner.AddStep(
            ReasoningStepType.Inference, "inf", "based on observations", 1, 2);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(3);
    }

    [Fact]
    public void AddStep_IncrementsStepNumber()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();
        reasoner.StartTrace();

        // Act
        var result1 = reasoner.AddStep(ReasoningStepType.Observation, "obs1", "r1");
        var result2 = reasoner.AddStep(ReasoningStepType.Observation, "obs2", "r2");

        // Assert
        result1.Value.Should().Be(1);
        result2.Value.Should().Be(2);
    }

    [Fact]
    public void EndTrace_WithActiveTrace_ReturnsCompletedTrace()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();
        reasoner.StartTrace();
        reasoner.AddStep(ReasoningStepType.Observation, "obs", "reason");

        // Act
        var result = reasoner.EndTrace("Final conclusion", true);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.WasSuccessful.Should().BeTrue();
        result.Value.FinalConclusion.Should().Be("Final conclusion");
        result.Value.IsActive.Should().BeFalse();
    }

    [Fact]
    public void EndTrace_WithNoActiveTrace_ReturnsFailure()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();

        // Act
        var result = reasoner.EndTrace("conclusion", true);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void EndTrace_AddsToHistory()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();
        reasoner.StartTrace();
        reasoner.AddStep(ReasoningStepType.Observation, "obs", "reason");
        reasoner.EndTrace("conclusion", true);

        // Act
        var history = reasoner.GetHistory().ToList();

        // Assert
        history.Should().HaveCount(1);
    }

    [Fact]
    public void EndTrace_ClearsActiveTrace()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();
        reasoner.StartTrace();
        reasoner.EndTrace("conclusion", true);

        // Act
        var activeTrace = reasoner.GetActiveTrace();

        // Assert
        activeTrace.IsSome.Should().BeFalse();
    }

    [Fact]
    public void EndTrace_CalculatesConfidence_ForSuccessfulTrace()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();
        reasoner.StartTrace();
        reasoner.AddStep(ReasoningStepType.Observation, "obs1", "r1");
        reasoner.AddStep(ReasoningStepType.Observation, "obs2", "r2");
        reasoner.AddStep(ReasoningStepType.Inference, "inf", "just", 1, 2);

        // Act
        var result = reasoner.EndTrace("conclusion", true);

        // Assert
        result.Value.Confidence.Should().BeGreaterThan(0.0);
        result.Value.Confidence.Should().BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public void EndTrace_WithFailure_SetsLowConfidence()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();
        reasoner.StartTrace();
        reasoner.AddStep(ReasoningStepType.Observation, "obs", "r");

        // Act
        var result = reasoner.EndTrace("no conclusion", false);

        // Assert
        result.Value.Confidence.Should().Be(0.1);
        result.Value.WasSuccessful.Should().BeFalse();
    }

    [Fact]
    public void ReflectOn_WithEmptyTrace_ReturnsInvalidResult()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();
        var trace = ReasoningTrace.Start(); // no steps

        // Act
        var result = reasoner.ReflectOn(trace);

        // Assert
        result.QualityScore.Should().Be(0.0);
        result.IdentifiedFallacies.Should().Contain("Invalid or empty reasoning trace");
    }

    [Fact]
    public void ReflectOn_WithGoodTrace_ReturnsReasonableQuality()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();
        var trace = ReasoningTrace.Start()
            .AddObservation("Data point 1", "Primary source")
            .AddObservation("Data point 2", "Secondary source")
            .AddInference("Pattern found", "Based on observations", 1, 2)
            .AddHypothesis("Explanation", "Plausible mechanism", 3)
            .Complete("Confirmed pattern", 0.8);

        // Act
        var result = reasoner.ReflectOn(trace);

        // Assert
        result.QualityScore.Should().BeGreaterThan(0.3);
        result.LogicalSoundness.Should().BeGreaterThan(0.0);
        result.EvidenceSupport.Should().BeGreaterThan(0.0);
    }

    [Fact]
    public void ReflectOn_WithUnsupportedConclusion_DetectsFallacy()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();
        // Conclusion with no dependencies
        var conclusionStep = new ReasoningStep(1, ReasoningStepType.Conclusion,
            "Just a guess", "No reason", DateTime.UtcNow, ImmutableList<int>.Empty);
        var trace = ReasoningTrace.Start().WithStep(conclusionStep);

        // Act
        var result = reasoner.ReflectOn(trace);

        // Assert
        result.IdentifiedFallacies.Should().Contain("Unsupported Conclusion");
    }

    [Fact]
    public void ReflectOn_WithNoObservationsButInferences_DetectsMissingEvidence()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();
        var infStep = new ReasoningStep(1, ReasoningStepType.Inference,
            "Inference without data", "No basis", DateTime.UtcNow, ImmutableList<int>.Empty);
        var trace = ReasoningTrace.Start().WithStep(infStep);

        // Act
        var result = reasoner.ReflectOn(trace);

        // Assert
        result.IdentifiedFallacies.Should().Contain("Missing Evidence");
    }

    [Fact]
    public void ReflectOn_WithHypothesesButNoValidation_DetectsConfirmationBias()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();
        var obsStep = new ReasoningStep(1, ReasoningStepType.Observation,
            "obs", "just", DateTime.UtcNow, ImmutableList<int>.Empty);
        var hypStep = new ReasoningStep(2, ReasoningStepType.Hypothesis,
            "hyp", "worth exploring", DateTime.UtcNow, ImmutableList.Create(1));
        var trace = ReasoningTrace.Start().WithStep(obsStep).WithStep(hypStep);

        // Act
        var result = reasoner.ReflectOn(trace);

        // Assert
        result.IdentifiedFallacies.Should().Contain("Confirmation Bias Pattern");
    }

    [Fact]
    public void ReflectOn_WithSingleObsAndConclusion_DetectsHastyGeneralization()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();
        var trace = ReasoningTrace.Start()
            .AddObservation("one data point", "limited data");
        var conclusionStep = new ReasoningStep(2, ReasoningStepType.Conclusion,
            "broad conclusion", "just one point", DateTime.UtcNow, ImmutableList.Create(1));
        trace = trace.WithStep(conclusionStep);

        // Act
        var result = reasoner.ReflectOn(trace);

        // Assert
        result.IdentifiedFallacies.Should().Contain("Hasty Generalization");
    }

    [Fact]
    public void GetThinkingStyle_WithNoHistory_ReturnsBalanced()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();

        // Act
        var style = reasoner.GetThinkingStyle();

        // Assert
        style.StyleName.Should().Be("Balanced");
        style.AnalyticalScore.Should().Be(0.5);
    }

    [Fact]
    public void GetThinkingStyle_WithHistory_ReturnsAnalyzedStyle()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();

        // Create a trace heavy on inferences (analytical style)
        reasoner.StartTrace();
        reasoner.AddStep(ReasoningStepType.Observation, "obs1", "r1");
        reasoner.AddStep(ReasoningStepType.Inference, "inf1", "just", 1);
        reasoner.AddStep(ReasoningStepType.Inference, "inf2", "just", 1);
        reasoner.EndTrace("conclusion", true);

        // Act
        var style = reasoner.GetThinkingStyle();

        // Assert
        style.AnalyticalScore.Should().BeGreaterThan(0.0);
    }

    [Fact]
    public void IdentifyBiases_WithEmptyHistory_ReturnsEmpty()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();

        // Act
        var biases = reasoner.IdentifyBiases(Array.Empty<ReasoningTrace>());

        // Assert
        biases.Should().BeEmpty();
    }

    [Fact]
    public void IdentifyBiases_WithBiasedTraces_DetectsBiases()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();

        // Create traces showing hasty generalization
        var traces = Enumerable.Range(0, 5).Select(_ =>
        {
            var trace = ReasoningTrace.Start()
                .AddObservation("single obs", "limited");
            var conclusion = new ReasoningStep(2, ReasoningStepType.Conclusion,
                "broad claim", "from one obs", DateTime.UtcNow, ImmutableList.Create(1));
            return trace.WithStep(conclusion).Complete("rushed conclusion", 0.5);
        }).ToList();

        // Act
        var biases = reasoner.IdentifyBiases(traces);

        // Assert
        biases.Should().NotBeEmpty();
    }

    [Fact]
    public void SuggestImprovement_WithFewObservations_SuggestsMoreObservations()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();
        var trace = ReasoningTrace.Start()
            .AddObservation("single obs", "limited");

        // Act
        var suggestions = reasoner.SuggestImprovement(trace);

        // Assert
        suggestions.Should().Contain(s => s.Contains("observations"));
    }

    [Fact]
    public void SuggestImprovement_WithUnvalidatedHypotheses_SuggestsValidation()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();
        var trace = ReasoningTrace.Start()
            .AddObservation("obs", "r")
            .AddHypothesis("maybe this", "exploring", 1);

        // Act
        var suggestions = reasoner.SuggestImprovement(trace);

        // Assert
        suggestions.Should().Contain(s => s.Contains("Validate") || s.Contains("hypothes"));
    }

    [Fact]
    public void SuggestImprovement_WithLongTraceAndNoRevisions_SuggestsRevisions()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();
        var trace = ReasoningTrace.Start()
            .AddObservation("obs1", "r1")
            .AddObservation("obs2", "r2")
            .AddInference("inf1", "j1", 1)
            .AddInference("inf2", "j2", 2)
            .AddInference("inf3", "j3", 1, 2)
            .AddHypothesis("hyp", "worth testing", 3);

        // Act
        var suggestions = reasoner.SuggestImprovement(trace);

        // Assert
        suggestions.Should().Contain(s => s.Contains("revis"));
    }

    [Fact]
    public void SuggestImprovement_WithShortTrace_SuggestsDeeperAnalysis()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();
        var trace = ReasoningTrace.Start()
            .AddObservation("obs", "r");

        // Act
        var suggestions = reasoner.SuggestImprovement(trace);

        // Assert
        suggestions.Should().Contain(s => s.Contains("more thoroughly"));
    }

    [Fact]
    public void SuggestImprovement_WithContradictions_SuggestsResolution()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();
        var contradictionStep = new ReasoningStep(1, ReasoningStepType.Contradiction,
            "conflicting data", "inconsistency", DateTime.UtcNow, ImmutableList<int>.Empty);
        var trace = ReasoningTrace.Start().WithStep(contradictionStep);

        // Act
        var suggestions = reasoner.SuggestImprovement(trace);

        // Assert
        suggestions.Should().Contain(s => s.Contains("contradict"));
    }

    [Fact]
    public void GetHistory_InitiallyEmpty()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();

        // Act
        var history = reasoner.GetHistory().ToList();

        // Assert
        history.Should().BeEmpty();
    }

    [Fact]
    public void GetHistory_ContainsCompletedTraces()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();

        reasoner.StartTrace();
        reasoner.AddStep(ReasoningStepType.Observation, "obs", "r");
        reasoner.EndTrace("conclusion 1", true);

        reasoner.StartTrace();
        reasoner.AddStep(ReasoningStepType.Observation, "obs2", "r2");
        reasoner.EndTrace("conclusion 2", true);

        // Act
        var history = reasoner.GetHistory().ToList();

        // Assert
        history.Should().HaveCount(2);
    }

    [Fact]
    public void MultipleTracesInSequence_WorkCorrectly()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();

        // First trace
        var id1 = reasoner.StartTrace();
        reasoner.AddStep(ReasoningStepType.Observation, "obs1", "r1");
        var result1 = reasoner.EndTrace("conclusion1", true);

        // Second trace
        var id2 = reasoner.StartTrace();
        reasoner.AddStep(ReasoningStepType.Observation, "obs2", "r2");
        var result2 = reasoner.EndTrace("conclusion2", true);

        // Assert
        id1.Should().NotBe(id2);
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();
        reasoner.GetHistory().Count().Should().Be(2);
    }
}
