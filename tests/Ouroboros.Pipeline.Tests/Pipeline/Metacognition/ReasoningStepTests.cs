namespace Ouroboros.Tests.Pipeline.Metacognition;

using Ouroboros.Pipeline.Metacognition;

[Trait("Category", "Unit")]
public class ReasoningStepTests
{
    [Fact]
    public void Observation_CreatesStepWithNoDependencies()
    {
        var step = ReasoningStep.Observation(1, "Observed X", "Relevant because Y");

        step.StepNumber.Should().Be(1);
        step.StepType.Should().Be(ReasoningStepType.Observation);
        step.Content.Should().Be("Observed X");
        step.Justification.Should().Be("Relevant because Y");
        step.Dependencies.Should().BeEmpty();
    }

    [Fact]
    public void Inference_CreatesStepWithDependencies()
    {
        var step = ReasoningStep.Inference(3, "Therefore Z", "From X and Y", 1, 2);

        step.StepNumber.Should().Be(3);
        step.StepType.Should().Be(ReasoningStepType.Inference);
        step.Dependencies.Should().Contain(1);
        step.Dependencies.Should().Contain(2);
    }

    [Fact]
    public void Hypothesis_CreatesHypothesisStep()
    {
        var step = ReasoningStep.Hypothesis(2, "Maybe W", "Worth exploring", 1);

        step.StepType.Should().Be(ReasoningStepType.Hypothesis);
        step.Dependencies.Should().Contain(1);
    }

    [Fact]
    public void Conclusion_CreatesConclusionStep()
    {
        var step = ReasoningStep.Conclusion(4, "Final answer", "Based on all evidence", 1, 2, 3);

        step.StepType.Should().Be(ReasoningStepType.Conclusion);
        step.Dependencies.Should().HaveCount(3);
    }

    [Fact]
    public void WithDependency_AddsNewDependency()
    {
        var step = ReasoningStep.Observation(1, "X", "Y");
        var updated = step.WithDependency(0);

        updated.Dependencies.Should().Contain(0);
    }

    [Fact]
    public void HasValidDependencies_ReturnsTrueWhenAllDependenciesAreEarlier()
    {
        var step = ReasoningStep.Inference(3, "Z", "reason", 1, 2);
        step.HasValidDependencies().Should().BeTrue();
    }

    [Fact]
    public void HasValidDependencies_ReturnsFalseWhenDependencyIsLater()
    {
        var step = ReasoningStep.Inference(2, "Z", "reason", 3);
        step.HasValidDependencies().Should().BeFalse();
    }

    [Fact]
    public void HasValidDependencies_ReturnsTrueForObservationWithNoDeps()
    {
        var step = ReasoningStep.Observation(1, "X", "Y");
        step.HasValidDependencies().Should().BeTrue();
    }
}
