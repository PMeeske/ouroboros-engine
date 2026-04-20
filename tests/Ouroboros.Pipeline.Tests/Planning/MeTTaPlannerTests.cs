using FluentAssertions;
using NSubstitute;
using Ouroboros.Abstractions;
using Ouroboros.Pipeline.Planning;
using Ouroboros.Tools.MeTTa;

namespace Ouroboros.Tests.Planning;

[Trait("Category", "Unit")]
public sealed class MeTTaPlannerTests
{
    private static IMeTTaEngine CreateMockEngine(string queryResult = "summarize_tool")
    {
        var engine = Substitute.For<IMeTTaEngine>();
        engine.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<string, string>.Success(queryResult)));
        engine.AddFactAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<string, string>.Success("OK")));
        return engine;
    }

    [Fact]
    public void Constructor_NullEngine_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new MeTTaPlanner(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #region PlanAsync

    [Fact]
    public async Task PlanAsync_NullStartType_ThrowsArgumentNullException()
    {
        // Arrange
        var engine = CreateMockEngine();
        var planner = new MeTTaPlanner(engine);

        // Act
        var act = () => planner.PlanAsync(null!, MeTTaType.Summary);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task PlanAsync_NullEndType_ThrowsArgumentNullException()
    {
        // Arrange
        var engine = CreateMockEngine();
        var planner = new MeTTaPlanner(engine);

        // Act
        var act = () => planner.PlanAsync(MeTTaType.Text, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task PlanAsync_EngineReturnsToolNames_ReturnsToolChain()
    {
        // Arrange
        var engine = CreateMockEngine("(chain summarize_tool generate_code_tool)");
        var planner = new MeTTaPlanner(engine);

        // Act
        var result = await planner.PlanAsync(MeTTaType.Text, MeTTaType.Code);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Tools.Should().Contain("summarize_tool");
        result.Value.Tools.Should().Contain("generate_code_tool");
    }

    [Fact]
    public async Task PlanAsync_EngineReturnsSingleTool_ReturnsSingleToolChain()
    {
        // Arrange
        var engine = CreateMockEngine("summarize_tool");
        var planner = new MeTTaPlanner(engine);

        // Act
        var result = await planner.PlanAsync(MeTTaType.Text, MeTTaType.Summary);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Tools.Should().Contain("summarize_tool");
    }

    [Fact]
    public async Task PlanAsync_EngineReturnsEmpty_ReturnsFailure()
    {
        // Arrange
        var engine = CreateMockEngine("[]");
        var planner = new MeTTaPlanner(engine);

        // Act
        var result = await planner.PlanAsync(MeTTaType.Text, MeTTaType.Answer);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task PlanAsync_EngineReturnsError_ReturnsFailure()
    {
        // Arrange
        var engine = Substitute.For<IMeTTaEngine>();
        engine.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<string, string>.Failure("No path found")));
        var planner = new MeTTaPlanner(engine);

        // Act
        var result = await planner.PlanAsync(MeTTaType.Text, MeTTaType.Answer);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Planning failed");
    }

    [Fact]
    public async Task PlanAsync_EngineThrows_ReturnsFailure()
    {
        // Arrange
        var engine = Substitute.For<IMeTTaEngine>();
        engine.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Result<string, string>>(_ => throw new InvalidOperationException("Engine crashed"));
        var planner = new MeTTaPlanner(engine);

        // Act
        var result = await planner.PlanAsync(MeTTaType.Text, MeTTaType.Answer);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Planning exception");
    }

    #endregion

    #region GetToolsAcceptingAsync

    [Fact]
    public async Task GetToolsAcceptingAsync_ValidType_ReturnsToolNames()
    {
        // Arrange
        var engine = CreateMockEngine("summarize_tool answer_question_tool");
        var planner = new MeTTaPlanner(engine);

        // Act
        var result = await planner.GetToolsAcceptingAsync(MeTTaType.Text);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("summarize_tool");
        result.Value.Should().Contain("answer_question_tool");
    }

    [Fact]
    public async Task GetToolsAcceptingAsync_EngineError_ReturnsFailure()
    {
        // Arrange
        var engine = Substitute.For<IMeTTaEngine>();
        engine.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<string, string>.Failure("error")));
        var planner = new MeTTaPlanner(engine);

        // Act
        var result = await planner.GetToolsAcceptingAsync(MeTTaType.Text);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region GetToolsProducingAsync

    [Fact]
    public async Task GetToolsProducingAsync_ValidType_ReturnsToolNames()
    {
        // Arrange
        var engine = CreateMockEngine("generate_code_tool");
        var planner = new MeTTaPlanner(engine);

        // Act
        var result = await planner.GetToolsProducingAsync(MeTTaType.Code);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("generate_code_tool");
    }

    #endregion

    #region RegisterToolSignatureAsync

    [Fact]
    public async Task RegisterToolSignatureAsync_EngineAccepts_ReturnsSuccess()
    {
        // Arrange
        var engine = CreateMockEngine();
        var planner = new MeTTaPlanner(engine);

        // Act
        var result = await planner.RegisterToolSignatureAsync(
            "my_tool", MeTTaType.Text, MeTTaType.Summary);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task RegisterToolSignatureAsync_EngineRejects_ReturnsFailure()
    {
        // Arrange
        var engine = Substitute.For<IMeTTaEngine>();
        engine.AddFactAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<string, string>.Failure("Parse error")));
        var planner = new MeTTaPlanner(engine);

        // Act
        var result = await planner.RegisterToolSignatureAsync(
            "bad_tool", MeTTaType.Text, MeTTaType.Code);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region InitializeAsync

    [Fact]
    public async Task InitializeAsync_AllFactsAccepted_ReturnsSuccess()
    {
        // Arrange
        var engine = CreateMockEngine();
        var planner = new MeTTaPlanner(engine);

        // Act
        var result = await planner.InitializeAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_AFactRejected_ReturnsFailure()
    {
        // Arrange
        var engine = Substitute.For<IMeTTaEngine>();
        engine.AddFactAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<string, string>.Failure("Bad fact")));
        var planner = new MeTTaPlanner(engine);

        // Act
        var result = await planner.InitializeAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion
}
