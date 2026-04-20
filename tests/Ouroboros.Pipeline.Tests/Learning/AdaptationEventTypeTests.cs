using Ouroboros.Pipeline.Learning;

namespace Ouroboros.Tests.Learning;

public class AdaptationEventTypeTests
{
    [Theory]
    [InlineData(AdaptationEventType.StrategyChange, 0)]
    [InlineData(AdaptationEventType.ParameterTune, 1)]
    [InlineData(AdaptationEventType.ModelUpdate, 2)]
    [InlineData(AdaptationEventType.SkillAcquisition, 3)]
    [InlineData(AdaptationEventType.Rollback, 4)]
    public void AdaptationEventType_HasExpectedValues(AdaptationEventType value, int expectedInt)
    {
        // Assert
        ((int)value).Should().Be(expectedInt);
    }

    [Fact]
    public void AdaptationEventType_HasExactlyFiveValues()
    {
        // Act
        var values = Enum.GetValues<AdaptationEventType>();

        // Assert
        values.Should().HaveCount(5);
    }

    [Fact]
    public void AdaptationEventType_AllValuesAreDefined()
    {
        // Assert
        Enum.IsDefined(AdaptationEventType.StrategyChange).Should().BeTrue();
        Enum.IsDefined(AdaptationEventType.ParameterTune).Should().BeTrue();
        Enum.IsDefined(AdaptationEventType.ModelUpdate).Should().BeTrue();
        Enum.IsDefined(AdaptationEventType.SkillAcquisition).Should().BeTrue();
        Enum.IsDefined(AdaptationEventType.Rollback).Should().BeTrue();
    }
}
