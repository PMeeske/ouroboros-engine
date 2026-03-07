namespace Ouroboros.Tests.Pipeline.Metacognition;

using Ouroboros.Pipeline.Metacognition;

[Trait("Category", "Unit")]
public class ProcessingModeTests
{
    [Theory]
    [InlineData(ProcessingMode.Analytical)]
    [InlineData(ProcessingMode.Creative)]
    [InlineData(ProcessingMode.Reactive)]
    [InlineData(ProcessingMode.Reflective)]
    [InlineData(ProcessingMode.Intuitive)]
    public void AllValues_AreDefined(ProcessingMode mode)
    {
        Enum.IsDefined(mode).Should().BeTrue();
    }

    [Fact]
    public void EnumHasExpectedCount()
    {
        Enum.GetValues<ProcessingMode>().Should().HaveCount(5);
    }
}
