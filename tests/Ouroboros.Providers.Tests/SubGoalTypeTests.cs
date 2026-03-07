namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class SubGoalTypeTests
{
    [Fact]
    public void EnumValues_ContainAllExpected()
    {
        var values = Enum.GetValues<SubGoalType>();

        values.Should().HaveCount(7);
        values.Should().Contain(SubGoalType.Retrieval);
        values.Should().Contain(SubGoalType.Transform);
        values.Should().Contain(SubGoalType.Reasoning);
        values.Should().Contain(SubGoalType.Creative);
        values.Should().Contain(SubGoalType.Coding);
        values.Should().Contain(SubGoalType.Math);
        values.Should().Contain(SubGoalType.Synthesis);
    }
}
