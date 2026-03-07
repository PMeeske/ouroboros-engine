namespace Ouroboros.Tests.Pipeline.Metacognition;

using Ouroboros.Pipeline.Metacognition;

[Trait("Category", "Unit")]
public class SeverityTests
{
    [Theory]
    [InlineData(Severity.Info, 0)]
    [InlineData(Severity.Warning, 1)]
    [InlineData(Severity.Critical, 2)]
    public void EnumValues_AreDefinedCorrectly(Severity value, int expectedInt)
    {
        ((int)value).Should().Be(expectedInt);
    }
}
