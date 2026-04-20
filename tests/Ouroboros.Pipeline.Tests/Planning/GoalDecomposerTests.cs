using FluentAssertions;
using NSubstitute;
using LangChain.Databases;
using Ouroboros.Pipeline.Branches;
using Ouroboros.Pipeline.Planning;
using Ouroboros.Providers;

namespace Ouroboros.Tests.Planning;

[Trait("Category", "Unit")]
public sealed class GoalDecomposerTests
{
    private static PipelineBranch CreateBranch()
    {
        var store = Substitute.For<IVectorStore>();
        return new PipelineBranch("test", store, new DataSource("test", "test"));
    }

    [Fact]
    public void DecomposeArrow_NullLlm_ThrowsArgumentNullException()
    {
        // Act
        var act = () => GoalDecomposer.DecomposeArrow(null!, Goal.Atomic("Test"));

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void DecomposeArrow_NullGoal_ThrowsArgumentNullException()
    {
        // Arrange
        var llm = Substitute.For<ToolAwareChatModel>();

        // Act
        var act = () => GoalDecomposer.DecomposeArrow(llm, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task DecomposeArrow_MaxDepthZero_ReturnsOriginalGoal()
    {
        // Arrange
        var llm = Substitute.For<ToolAwareChatModel>();
        var goal = Goal.Atomic("Test goal");
        var branch = CreateBranch();

        // Act
        var step = GoalDecomposer.DecomposeArrow(llm, goal, maxDepth: 0);
        var result = await step(branch);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Description.Should().Be("Test goal");
    }

    [Fact]
    public async Task DecomposeArrow_EmptyDescription_ReturnsFailure()
    {
        // Arrange
        var llm = Substitute.For<ToolAwareChatModel>();
        var goal = new Goal(Guid.NewGuid(), "   ", [], _ => false);
        var branch = CreateBranch();

        // Act
        var step = GoalDecomposer.DecomposeArrow(llm, goal, maxDepth: 3);
        var result = await step(branch);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public async Task DecomposeArrow_LlmReturnsValidJson_CreatesSubGoals()
    {
        // Arrange
        var llm = Substitute.For<ToolAwareChatModel>();
        llm.GenerateWithToolsAsync(Arg.Any<string>())
            .Returns(Task.FromResult(
                (Response: "[\"Sub-goal 1\", \"Sub-goal 2\"]",
                 ToolExecutions: new List<ToolExecution>())));
        var goal = Goal.Atomic("Main goal");
        var branch = CreateBranch();

        // Act
        var step = GoalDecomposer.DecomposeArrow(llm, goal, maxDepth: 1);
        var result = await step(branch);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SubGoals.Should().HaveCount(2);
        result.Value.SubGoals[0].Description.Should().Be("Sub-goal 1");
        result.Value.SubGoals[1].Description.Should().Be("Sub-goal 2");
    }

    [Fact]
    public async Task DecomposeArrow_LlmReturnsJsonInCodeBlock_ParsesCorrectly()
    {
        // Arrange
        var llm = Substitute.For<ToolAwareChatModel>();
        llm.GenerateWithToolsAsync(Arg.Any<string>())
            .Returns(Task.FromResult(
                (Response: "```json\n[\"Goal A\", \"Goal B\"]\n```",
                 ToolExecutions: new List<ToolExecution>())));
        var goal = Goal.Atomic("Main goal");
        var branch = CreateBranch();

        // Act
        var step = GoalDecomposer.DecomposeArrow(llm, goal, maxDepth: 1);
        var result = await step(branch);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SubGoals.Should().HaveCount(2);
    }

    [Fact]
    public async Task DecomposeArrow_LlmReturnsInvalidJson_ReturnsFailure()
    {
        // Arrange
        var llm = Substitute.For<ToolAwareChatModel>();
        llm.GenerateWithToolsAsync(Arg.Any<string>())
            .Returns(Task.FromResult(
                (Response: "not valid json at all",
                 ToolExecutions: new List<ToolExecution>())));
        var goal = Goal.Atomic("Main goal");
        var branch = CreateBranch();

        // Act
        var step = GoalDecomposer.DecomposeArrow(llm, goal, maxDepth: 1);
        var result = await step(branch);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task DecomposeArrow_LlmThrowsException_ReturnsFailure()
    {
        // Arrange
        var llm = Substitute.For<ToolAwareChatModel>();
        llm.GenerateWithToolsAsync(Arg.Any<string>())
            .Returns<(string, List<ToolExecution>)>(_ => throw new InvalidOperationException("API error"));
        var goal = Goal.Atomic("Main goal");
        var branch = CreateBranch();

        // Act
        var step = GoalDecomposer.DecomposeArrow(llm, goal, maxDepth: 1);
        var result = await step(branch);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Goal decomposition failed");
    }

    [Fact]
    public async Task DecomposeArrow_LlmReturnsEmptyArray_ReturnsFailure()
    {
        // Arrange
        var llm = Substitute.For<ToolAwareChatModel>();
        llm.GenerateWithToolsAsync(Arg.Any<string>())
            .Returns(Task.FromResult(
                (Response: "[]",
                 ToolExecutions: new List<ToolExecution>())));
        var goal = Goal.Atomic("Main goal");
        var branch = CreateBranch();

        // Act
        var step = GoalDecomposer.DecomposeArrow(llm, goal, maxDepth: 1);
        var result = await step(branch);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void DecomposeRecursiveArrow_NullLlm_ThrowsArgumentNullException()
    {
        // Act
        var act = () => GoalDecomposer.DecomposeRecursiveArrow(null!, Goal.Atomic("Test"));

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void DecomposeRecursiveArrow_NullGoal_ThrowsArgumentNullException()
    {
        // Arrange
        var llm = Substitute.For<ToolAwareChatModel>();

        // Act
        var act = () => GoalDecomposer.DecomposeRecursiveArrow(llm, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task DecomposeRecursiveArrow_MaxDepthOne_DoesNotRecurse()
    {
        // Arrange
        var llm = Substitute.For<ToolAwareChatModel>();
        llm.GenerateWithToolsAsync(Arg.Any<string>())
            .Returns(Task.FromResult(
                (Response: "[\"Sub A\", \"Sub B\"]",
                 ToolExecutions: new List<ToolExecution>())));
        var goal = Goal.Atomic("Root");
        var branch = CreateBranch();

        // Act
        var step = GoalDecomposer.DecomposeRecursiveArrow(llm, goal, maxDepth: 1);
        var result = await step(branch);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SubGoals.Should().HaveCount(2);
        // Sub-goals should not have their own sub-goals at depth 1
        result.Value.SubGoals[0].SubGoals.Should().BeEmpty();
    }

    [Fact]
    public async Task DecomposeArrow_LlmReturnsJsonWithPreamble_ExtractsArray()
    {
        // Arrange
        var llm = Substitute.For<ToolAwareChatModel>();
        llm.GenerateWithToolsAsync(Arg.Any<string>())
            .Returns(Task.FromResult(
                (Response: "Here are the sub-goals: [\"Alpha\", \"Beta\"]",
                 ToolExecutions: new List<ToolExecution>())));
        var goal = Goal.Atomic("Main");
        var branch = CreateBranch();

        // Act
        var step = GoalDecomposer.DecomposeArrow(llm, goal, maxDepth: 1);
        var result = await step(branch);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SubGoals.Should().HaveCount(2);
    }
}
