namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class SubGoalComplexityTests
{
    [Fact]
    public void EnumValues_ContainAllExpected()
    {
        var values = Enum.GetValues<SubGoalComplexity>();

        values.Should().HaveCount(5);
        values.Should().Contain(SubGoalComplexity.Trivial);
        values.Should().Contain(SubGoalComplexity.Simple);
        values.Should().Contain(SubGoalComplexity.Moderate);
        values.Should().Contain(SubGoalComplexity.Complex);
        values.Should().Contain(SubGoalComplexity.Expert);
    }

    [Fact]
    public void Ordering_TrivialIsLessThanExpert()
    {
        ((int)SubGoalComplexity.Trivial).Should().BeLessThan((int)SubGoalComplexity.Expert);
    }

    [Fact]
    public void Ordering_SimpleIsLessThanComplex()
    {
        ((int)SubGoalComplexity.Simple).Should().BeLessThan((int)SubGoalComplexity.Complex);
    }
}
