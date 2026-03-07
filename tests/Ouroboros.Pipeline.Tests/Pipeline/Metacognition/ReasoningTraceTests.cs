namespace Ouroboros.Tests.Pipeline.Metacognition;

using Ouroboros.Pipeline.Metacognition;

[Trait("Category", "Unit")]
public class ReasoningTraceTests
{
    [Fact]
    public void Start_CreatesActiveEmptyTrace()
    {
        var trace = ReasoningTrace.Start();

        trace.Steps.Should().BeEmpty();
        trace.IsActive.Should().BeTrue();
        trace.FinalConclusion.Should().BeNull();
        trace.WasSuccessful.Should().BeFalse();
        trace.Confidence.Should().Be(0.0);
        trace.Duration.Should().BeNull();
        trace.NextStepNumber.Should().Be(1);
    }

    [Fact]
    public void StartWithId_UsesProvidedId()
    {
        var id = Guid.NewGuid();
        var trace = ReasoningTrace.StartWithId(id);

        trace.Id.Should().Be(id);
    }

    [Fact]
    public void AddObservation_AddsObservationStep()
    {
        var trace = ReasoningTrace.Start()
            .AddObservation("Saw X", "It matters");

        trace.Steps.Should().HaveCount(1);
        trace.Steps[0].StepType.Should().Be(ReasoningStepType.Observation);
        trace.NextStepNumber.Should().Be(2);
    }

    [Fact]
    public void AddInference_AddsInferenceStep()
    {
        var trace = ReasoningTrace.Start()
            .AddObservation("X", "reason")
            .AddInference("Therefore Y", "From X", 1);

        trace.Steps.Should().HaveCount(2);
        trace.Steps[1].StepType.Should().Be(ReasoningStepType.Inference);
    }

    [Fact]
    public void AddHypothesis_AddsHypothesisStep()
    {
        var trace = ReasoningTrace.Start()
            .AddObservation("X", "reason")
            .AddHypothesis("Maybe Z", "Worth considering", 1);

        trace.Steps.Should().HaveCount(2);
        trace.Steps[1].StepType.Should().Be(ReasoningStepType.Hypothesis);
    }

    [Fact]
    public void Complete_SetsEndTimeAndConclusion()
    {
        var trace = ReasoningTrace.Start()
            .AddObservation("X", "reason")
            .Complete("Final answer", 0.9);

        trace.IsActive.Should().BeFalse();
        trace.WasSuccessful.Should().BeTrue();
        trace.FinalConclusion.Should().Be("Final answer");
        trace.Confidence.Should().Be(0.9);
        trace.Duration.Should().NotBeNull();
    }

    [Fact]
    public void Fail_MarksTraceFailed()
    {
        var trace = ReasoningTrace.Start()
            .AddObservation("X", "reason")
            .Fail("Insufficient data");

        trace.IsActive.Should().BeFalse();
        trace.WasSuccessful.Should().BeFalse();
        trace.Confidence.Should().Be(0.0);
        trace.FinalConclusion.Should().Contain("Failed");
    }

    [Fact]
    public void GetStepsByType_FiltersCorrectly()
    {
        var trace = ReasoningTrace.Start()
            .AddObservation("X", "reason")
            .AddObservation("Y", "reason")
            .AddInference("Z", "from X and Y", 1, 2);

        trace.GetStepsByType(ReasoningStepType.Observation).Should().HaveCount(2);
        trace.GetStepsByType(ReasoningStepType.Inference).Should().HaveCount(1);
    }

    [Fact]
    public void IsLogicallyConsistent_ReturnsTrueForValidTrace()
    {
        var trace = ReasoningTrace.Start()
            .AddObservation("X", "reason")
            .AddInference("Y", "from X", 1);

        trace.IsLogicallyConsistent().Should().BeTrue();
    }
}
