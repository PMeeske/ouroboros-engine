namespace Ouroboros.Tests.Routing;

[Trait("Category", "Unit")]
public sealed class TaskTypeTests
{
    [Fact]
    public void Enum_HasFiveMembers()
    {
        Enum.GetValues<TaskType>().Should().HaveCount(5);
    }

    [Theory]
    [InlineData(TaskType.Simple, 0)]
    [InlineData(TaskType.Reasoning, 1)]
    [InlineData(TaskType.Planning, 2)]
    [InlineData(TaskType.Coding, 3)]
    [InlineData(TaskType.Unknown, 4)]
    public void Enum_HasExpectedValues(TaskType type, int expected)
    {
        ((int)type).Should().Be(expected);
    }
}
