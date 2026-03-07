namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class SubGoalResultTests
{
    [Fact]
    public void Ctor_SetsAllProperties()
    {
        var response = new ThinkingResponse("thinking...", "the answer");
        var result = new SubGoalResult(
            GoalId: "goal_1",
            PathwayUsed: "claude",
            Response: response,
            Duration: TimeSpan.FromSeconds(2),
            Success: true);

        result.GoalId.Should().Be("goal_1");
        result.PathwayUsed.Should().Be("claude");
        result.Response.Content.Should().Be("the answer");
        result.Duration.Should().Be(TimeSpan.FromSeconds(2));
        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Ctor_WithError_SetsErrorMessage()
    {
        var response = new ThinkingResponse(null, "");
        var result = new SubGoalResult(
            GoalId: "goal_2",
            PathwayUsed: "gpt-4",
            Response: response,
            Duration: TimeSpan.FromMilliseconds(500),
            Success: false,
            ErrorMessage: "Timeout occurred");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Timeout occurred");
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var response = new ThinkingResponse(null, "content");
        var r1 = new SubGoalResult("g1", "p1", response, TimeSpan.FromSeconds(1), true);
        var r2 = new SubGoalResult("g1", "p1", response, TimeSpan.FromSeconds(1), true);

        r1.Should().Be(r2);
    }
}
