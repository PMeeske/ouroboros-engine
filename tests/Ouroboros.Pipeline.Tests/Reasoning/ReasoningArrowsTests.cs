using Ouroboros.Abstractions.Core;
using Ouroboros.Pipeline.Reasoning;
using NSubstitute;

namespace Ouroboros.Tests.Pipeline.Reasoning;

[Trait("Category", "Unit")]
public sealed class ReasoningArrowsTests
{
    private readonly ToolAwareChatModel _llm;
    private readonly ToolRegistry _tools;
    private readonly IEmbeddingModel _embed;

    public ReasoningArrowsTests()
    {
        var chatModel = new MockChatModel("Generated response text");
        _tools = new ToolRegistry();
        _llm = new ToolAwareChatModel(chatModel, _tools);

        _embed = Substitute.For<IEmbeddingModel>();
        _embed.CreateEmbeddingsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new float[] { 0.1f, 0.2f, 0.3f }));
    }

    private static PipelineBranch CreateBranch(string name = "test-branch")
    {
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test");
        return new PipelineBranch(name, store, source);
    }

    #region ThinkingArrow Tests

    [Fact]
    public async Task ThinkingArrow_WithValidInputs_ReturnsUpdatedBranch()
    {
        // Arrange
        var branch = CreateBranch();
        var arrow = ReasoningArrows.ThinkingArrow(_llm, _tools, _embed, "AI", "What is AI?");

        // Act
        var result = await arrow(branch);

        // Assert
        result.Should().NotBeNull();
        result.Events.Should().HaveCount(1);
        result.Events[0].Should().BeOfType<Ouroboros.Domain.Events.ReasoningStep>();
    }

    [Fact]
    public async Task ThinkingArrow_AddsThinkingState()
    {
        // Arrange
        var branch = CreateBranch();
        var arrow = ReasoningArrows.ThinkingArrow(_llm, _tools, _embed, "topic", "query");

        // Act
        var result = await arrow(branch);

        // Assert
        var step = result.Events[0] as Ouroboros.Domain.Events.ReasoningStep;
        step.Should().NotBeNull();
        step!.State.Should().BeOfType<Thinking>();
    }

    #endregion

    #region SafeThinkingArrow Tests

    [Fact]
    public async Task SafeThinkingArrow_WithValidInputs_ReturnsSuccess()
    {
        // Arrange
        var branch = CreateBranch();
        var arrow = ReasoningArrows.SafeThinkingArrow(_llm, _tools, _embed, "topic", "query");

        // Act
        var result = await arrow(branch);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Events.Should().HaveCount(1);
    }

    [Fact]
    public async Task SafeThinkingArrow_WhenLlmThrows_ReturnsFailure()
    {
        // Arrange
        var failingChat = Substitute.For<IChatCompletionModel>();
        failingChat.GenerateTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<string>(x => throw new InvalidOperationException("LLM failed"));
        var failingLlm = new ToolAwareChatModel(failingChat, _tools);

        var branch = CreateBranch();
        var arrow = ReasoningArrows.SafeThinkingArrow(failingLlm, _tools, _embed, "topic", "query");

        // Act
        var result = await arrow(branch);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Thinking generation failed");
    }

    #endregion

    #region DraftArrow Tests

    [Fact]
    public async Task DraftArrow_WithValidInputs_ReturnsUpdatedBranch()
    {
        // Arrange
        var branch = CreateBranch();
        var arrow = ReasoningArrows.DraftArrow(_llm, _tools, _embed, "topic", "query");

        // Act
        var result = await arrow(branch);

        // Assert
        result.Events.Should().HaveCount(1);
        var step = result.Events[0] as Ouroboros.Domain.Events.ReasoningStep;
        step!.State.Should().BeOfType<Draft>();
    }

    [Fact]
    public async Task SafeDraftArrow_WithValidInputs_ReturnsSuccess()
    {
        // Arrange
        var branch = CreateBranch();
        var arrow = ReasoningArrows.SafeDraftArrow(_llm, _tools, _embed, "topic", "query");

        // Act
        var result = await arrow(branch);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task SafeDraftArrow_WhenLlmThrows_ReturnsFailure()
    {
        // Arrange
        var failingChat = Substitute.For<IChatCompletionModel>();
        failingChat.GenerateTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<string>(x => throw new InvalidOperationException("LLM down"));
        var failingLlm = new ToolAwareChatModel(failingChat, _tools);

        var branch = CreateBranch();
        var arrow = ReasoningArrows.SafeDraftArrow(failingLlm, _tools, _embed, "topic", "query");

        // Act
        var result = await arrow(branch);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Draft generation failed");
    }

    #endregion

    #region CritiqueArrow Tests

    [Fact]
    public async Task CritiqueArrow_WithNoDraft_ReturnsBranchUnchanged()
    {
        // Arrange
        var branch = CreateBranch();
        var arrow = ReasoningArrows.CritiqueArrow(_llm, _tools, _embed, "topic", "query");

        // Act
        var result = await arrow(branch);

        // Assert
        result.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task CritiqueArrow_WithDraft_AddsCritiqueEvent()
    {
        // Arrange
        var branch = CreateBranch();
        branch = branch.WithReasoning(new Draft("Initial draft text"), "prompt");
        var arrow = ReasoningArrows.CritiqueArrow(_llm, _tools, _embed, "topic", "query");

        // Act
        var result = await arrow(branch);

        // Assert
        result.Events.Should().HaveCount(2); // Draft + Critique
        var lastStep = result.Events[^1] as Ouroboros.Domain.Events.ReasoningStep;
        lastStep!.State.Should().BeOfType<Critique>();
    }

    [Fact]
    public async Task SafeCritiqueArrow_WithNoDraft_ReturnsFailure()
    {
        // Arrange
        var branch = CreateBranch();
        var arrow = ReasoningArrows.SafeCritiqueArrow(_llm, _tools, _embed, "topic", "query");

        // Act
        var result = await arrow(branch);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("No draft or previous improvement found");
    }

    [Fact]
    public async Task SafeCritiqueArrow_WithDraft_ReturnsSuccess()
    {
        // Arrange
        var branch = CreateBranch();
        branch = branch.WithReasoning(new Draft("Draft content"), "prompt");
        var arrow = ReasoningArrows.SafeCritiqueArrow(_llm, _tools, _embed, "topic", "query");

        // Act
        var result = await arrow(branch);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CritiqueArrow_WithFinalSpec_CritiquesFinalSpec()
    {
        // Arrange - FinalSpec should also be critiqueable for iterative refinement
        var branch = CreateBranch();
        branch = branch.WithReasoning(new FinalSpec("Improved text"), "prompt");
        var arrow = ReasoningArrows.CritiqueArrow(_llm, _tools, _embed, "topic", "query");

        // Act
        var result = await arrow(branch);

        // Assert
        result.Events.Should().HaveCount(2);
    }

    #endregion

    #region ImproveArrow Tests

    [Fact]
    public async Task ImproveArrow_WithNoDraftOrCritique_ReturnsBranchUnchanged()
    {
        // Arrange
        var branch = CreateBranch();
        var arrow = ReasoningArrows.ImproveArrow(_llm, _tools, _embed, "topic", "query");

        // Act
        var result = await arrow(branch);

        // Assert
        result.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task ImproveArrow_WithDraftAndCritique_AddsFinalSpecEvent()
    {
        // Arrange
        var branch = CreateBranch();
        branch = branch.WithReasoning(new Draft("Draft text"), "prompt");
        branch = branch.WithReasoning(new Critique("Critique text"), "prompt");
        var arrow = ReasoningArrows.ImproveArrow(_llm, _tools, _embed, "topic", "query");

        // Act
        var result = await arrow(branch);

        // Assert
        result.Events.Should().HaveCount(3);
        var lastStep = result.Events[^1] as Ouroboros.Domain.Events.ReasoningStep;
        lastStep!.State.Should().BeOfType<FinalSpec>();
    }

    [Fact]
    public async Task SafeImproveArrow_WithNoDraft_ReturnsFailure()
    {
        // Arrange
        var branch = CreateBranch();
        var arrow = ReasoningArrows.SafeImproveArrow(_llm, _tools, _embed, "topic", "query");

        // Act
        var result = await arrow(branch);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("No draft or previous improvement found");
    }

    [Fact]
    public async Task SafeImproveArrow_WithDraftButNoCritique_ReturnsFailure()
    {
        // Arrange
        var branch = CreateBranch();
        branch = branch.WithReasoning(new Draft("Draft text"), "prompt");
        var arrow = ReasoningArrows.SafeImproveArrow(_llm, _tools, _embed, "topic", "query");

        // Act
        var result = await arrow(branch);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("No critique found");
    }

    [Fact]
    public async Task SafeImproveArrow_WithDraftAndCritique_ReturnsSuccess()
    {
        // Arrange
        var branch = CreateBranch();
        branch = branch.WithReasoning(new Draft("Draft text"), "prompt");
        branch = branch.WithReasoning(new Critique("Critique text"), "prompt");
        var arrow = ReasoningArrows.SafeImproveArrow(_llm, _tools, _embed, "topic", "query");

        // Act
        var result = await arrow(branch);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region SafeReasoningPipeline Tests

    [Fact]
    public async Task SafeReasoningPipeline_WithValidInputs_ExecutesAllStages()
    {
        // Arrange
        var branch = CreateBranch();
        var pipeline = ReasoningArrows.SafeReasoningPipeline(_llm, _tools, _embed, "topic", "query");

        // Act
        var result = await pipeline(branch);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // Should have Thinking + Draft + Critique + Improve = 4 events
        result.Value.Events.Should().HaveCount(4);
    }

    #endregion

    #region Immutability Tests

    [Fact]
    public async Task ThinkingArrow_DoesNotMutateOriginalBranch()
    {
        // Arrange
        var branch = CreateBranch();
        var arrow = ReasoningArrows.ThinkingArrow(_llm, _tools, _embed, "topic", "query");

        // Act
        var result = await arrow(branch);

        // Assert
        branch.Events.Should().BeEmpty();
        result.Events.Should().HaveCount(1);
    }

    [Fact]
    public async Task DraftArrow_PreservesBranchName()
    {
        // Arrange
        var branch = CreateBranch("my-branch");
        var arrow = ReasoningArrows.DraftArrow(_llm, _tools, _embed, "topic", "query");

        // Act
        var result = await arrow(branch);

        // Assert
        result.Name.Should().Be("my-branch");
    }

    #endregion
}
