namespace Ouroboros.Tests.Pipeline.MultiAgent;

using TaskStatus = Ouroboros.Pipeline.MultiAgent.TaskStatus;

[Trait("Category", "Unit")]
public class TaskStatusTests
{
    [Theory]
    [InlineData(TaskStatus.Pending, 0)]
    [InlineData(TaskStatus.Assigned, 1)]
    [InlineData(TaskStatus.InProgress, 2)]
    [InlineData(TaskStatus.Completed, 3)]
    [InlineData(TaskStatus.Failed, 4)]
    [InlineData(TaskStatus.Cancelled, 5)]
    public void EnumValues_AreDefinedCorrectly(TaskStatus value, int expectedInt)
    {
        ((int)value).Should().Be(expectedInt);
    }
}
