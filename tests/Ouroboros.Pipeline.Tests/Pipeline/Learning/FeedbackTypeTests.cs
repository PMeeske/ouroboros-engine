namespace Ouroboros.Tests.Pipeline.Learning;

using Ouroboros.Pipeline.Learning;

[Trait("Category", "Unit")]
public class FeedbackTypeTests
{
    [Theory]
    [InlineData(FeedbackType.Explicit, 0)]
    [InlineData(FeedbackType.Implicit, 1)]
    [InlineData(FeedbackType.Corrective, 2)]
    [InlineData(FeedbackType.Comparative, 3)]
    public void EnumValues_AreDefinedCorrectly(FeedbackType value, int expectedInt)
    {
        ((int)value).Should().Be(expectedInt);
    }
}
