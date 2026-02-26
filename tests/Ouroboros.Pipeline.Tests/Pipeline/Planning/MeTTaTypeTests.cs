namespace Ouroboros.Tests.Pipeline.Planning;

using Ouroboros.Pipeline.Planning;

[Trait("Category", "Unit")]
public class MeTTaTypeTests
{
    [Fact]
    public void StaticInstances_HaveExpectedNames()
    {
        MeTTaType.Text.Name.Should().Be("Text");
        MeTTaType.Summary.Name.Should().Be("Summary");
        MeTTaType.Code.Name.Should().Be("Code");
        MeTTaType.TestResult.Name.Should().Be("TestResult");
        MeTTaType.Query.Name.Should().Be("Query");
        MeTTaType.Answer.Name.Should().Be("Answer");
    }

    [Fact]
    public void ToString_ReturnsName()
    {
        MeTTaType.Text.ToString().Should().Be("Text");
    }

    [Fact]
    public void Record_SupportsEquality()
    {
        var t1 = new MeTTaType("Custom");
        var t2 = new MeTTaType("Custom");

        t1.Should().Be(t2);
    }
}
