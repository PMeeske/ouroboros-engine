namespace Ouroboros.Tests.Pipeline.Metacognition;

using Ouroboros.Pipeline.Metacognition;

[Trait("Category", "Unit")]
public class CognitiveEventTypeTests
{
    [Fact]
    public void EnumHasExpectedCount()
    {
        Enum.GetValues<CognitiveEventType>().Should().HaveCount(10);
    }

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
    public void AllValues_AreDefined(CognitiveEventType value)
    {
        Enum.IsDefined(value).Should().BeTrue();
    }
}
