namespace Ouroboros.Tests.Pipeline.Metacognition;

using Ouroboros.Pipeline.Metacognition;

[Trait("Category", "Unit")]
public class ReasoningStepTypeTests
{
    [Fact]
    public void EnumHasExpectedCount()
    {
        Enum.GetValues<ReasoningStepType>().Should().HaveCount(8);
    }

    [Theory]
    [InlineData(ReasoningStepType.Observation)]
    [InlineData(ReasoningStepType.Inference)]
    [InlineData(ReasoningStepType.Hypothesis)]
    [InlineData(ReasoningStepType.Validation)]
    [InlineData(ReasoningStepType.Revision)]
    [InlineData(ReasoningStepType.Assumption)]
    [InlineData(ReasoningStepType.Conclusion)]
    [InlineData(ReasoningStepType.Contradiction)]
    public void AllValues_AreDefined(ReasoningStepType value)
    {
        Enum.IsDefined(value).Should().BeTrue();
    }
}
