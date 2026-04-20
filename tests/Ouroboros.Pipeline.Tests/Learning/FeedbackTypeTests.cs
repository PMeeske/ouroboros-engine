using Ouroboros.Pipeline.Learning;

namespace Ouroboros.Tests.Learning;

public class FeedbackTypeTests
{
    [Theory]
    [InlineData(FeedbackType.Explicit, 0)]
    [InlineData(FeedbackType.Implicit, 1)]
    [InlineData(FeedbackType.Corrective, 2)]
    [InlineData(FeedbackType.Comparative, 3)]
    public void FeedbackType_HasExpectedValues(FeedbackType value, int expectedInt)
    {
        // Assert
        ((int)value).Should().Be(expectedInt);
    }

    [Fact]
    public void FeedbackType_HasExactlyFourValues()
    {
        // Act
        var values = Enum.GetValues<FeedbackType>();

        // Assert
        values.Should().HaveCount(4);
    }

    [Fact]
    public void FeedbackType_AllValuesAreDefined()
    {
        // Assert
        Enum.IsDefined(FeedbackType.Explicit).Should().BeTrue();
        Enum.IsDefined(FeedbackType.Implicit).Should().BeTrue();
        Enum.IsDefined(FeedbackType.Corrective).Should().BeTrue();
        Enum.IsDefined(FeedbackType.Comparative).Should().BeTrue();
    }
}
