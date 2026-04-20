using FluentAssertions;
using Ouroboros.Pipeline.Metacognition;

namespace Ouroboros.Tests.Metacognition;

public sealed class HealthStatusTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void HealthStatus_HasExpectedValues()
    {
        Enum.GetValues<HealthStatus>().Should().HaveCount(4);
    }

    [Theory]
    [InlineData(HealthStatus.Healthy, 0)]
    [InlineData(HealthStatus.Degraded, 1)]
    [InlineData(HealthStatus.Impaired, 2)]
    [InlineData(HealthStatus.Critical, 3)]
    public void HealthStatus_Value_MatchesExpectedOrdinal(HealthStatus status, int expected)
    {
        ((int)status).Should().Be(expected);
    }
}

public sealed class SeverityTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Severity_HasExpectedValues()
    {
        Enum.GetValues<Severity>().Should().HaveCount(3);
    }

    [Theory]
    [InlineData(Severity.Info, 0)]
    [InlineData(Severity.Warning, 1)]
    [InlineData(Severity.Critical, 2)]
    public void Severity_Value_MatchesExpectedOrdinal(Severity severity, int expected)
    {
        ((int)severity).Should().Be(expected);
    }
}

public sealed class ProcessingModeTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void ProcessingMode_HasExpectedValues()
    {
        Enum.GetValues<ProcessingMode>().Should().HaveCount(5);
    }

    [Theory]
    [InlineData(ProcessingMode.Analytical)]
    [InlineData(ProcessingMode.Creative)]
    [InlineData(ProcessingMode.Reactive)]
    [InlineData(ProcessingMode.Reflective)]
    [InlineData(ProcessingMode.Intuitive)]
    public void ProcessingMode_Value_IsDefined(ProcessingMode mode)
    {
        Enum.IsDefined(mode).Should().BeTrue();
    }
}

public sealed class TrendTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Trend_HasExpectedValues()
    {
        Enum.GetValues<Trend>().Should().HaveCount(5);
    }

    [Theory]
    [InlineData(Trend.Improving)]
    [InlineData(Trend.Stable)]
    [InlineData(Trend.Declining)]
    [InlineData(Trend.Volatile)]
    [InlineData(Trend.Unknown)]
    public void Trend_Value_IsDefined(Trend trend)
    {
        Enum.IsDefined(trend).Should().BeTrue();
    }
}

public sealed class PerformanceDimensionTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void PerformanceDimension_HasExpectedValues()
    {
        Enum.GetValues<PerformanceDimension>().Should().HaveCount(6);
    }

    [Theory]
    [InlineData(PerformanceDimension.Accuracy)]
    [InlineData(PerformanceDimension.Speed)]
    [InlineData(PerformanceDimension.Creativity)]
    [InlineData(PerformanceDimension.Consistency)]
    [InlineData(PerformanceDimension.Adaptability)]
    [InlineData(PerformanceDimension.Communication)]
    public void PerformanceDimension_Value_IsDefined(PerformanceDimension dimension)
    {
        Enum.IsDefined(dimension).Should().BeTrue();
    }
}

public sealed class CognitiveEventTypeTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void CognitiveEventType_HasExpectedValues()
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
    public void CognitiveEventType_Value_IsDefined(CognitiveEventType eventType)
    {
        Enum.IsDefined(eventType).Should().BeTrue();
    }
}

public sealed class ReasoningStepTypeTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void ReasoningStepType_HasExpectedValues()
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
    public void ReasoningStepType_Value_IsDefined(ReasoningStepType stepType)
    {
        Enum.IsDefined(stepType).Should().BeTrue();
    }
}
