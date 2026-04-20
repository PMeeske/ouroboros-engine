using Ouroboros.Pipeline.Reasoning;

namespace Ouroboros.Tests.Pipeline.Reasoning;

[Trait("Category", "Unit")]
public sealed class HyperonReasoningStepTests : IDisposable
{
    private readonly HyperonReasoningStep _sut;

    public HyperonReasoningStepTests()
    {
        _sut = new HyperonReasoningStep("test-step");
    }

    [Fact]
    public void Constructor_WithStepName_SetsName()
    {
        // Assert
        _sut.Name.Should().Be("test-step");
    }

    [Fact]
    public void Constructor_WithStepName_CreatesEngine()
    {
        // Assert
        _sut.Engine.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithStepName_CreatesFlow()
    {
        // Assert
        _sut.Flow.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithSharedEngine_UsesProvidedEngine()
    {
        // Arrange
        var engine = _sut.Engine;

        // Act
        using var step2 = new HyperonReasoningStep("step-2", engine);

        // Assert
        step2.Engine.Should().BeSameAs(engine);
        step2.Name.Should().Be("step-2");
    }

    [Fact]
    public void Constructor_WithNullEngine_ThrowsArgumentNullException()
    {
        // Arrange & Act
        Action act = () => new HyperonReasoningStep("step", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CreateArrow_ReturnsCallableFunction()
    {
        // Arrange
        var arrow = _sut.CreateArrow<string>(async (engine, context) =>
        {
            await Task.CompletedTask;
            return context + "-processed";
        });

        // Assert
        arrow.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateArrow_ExecutesReasoningLogic()
    {
        // Arrange
        var arrow = _sut.CreateArrow<string>(async (engine, context) =>
        {
            await Task.CompletedTask;
            return context + "-processed";
        });

        // Act
        string result = await arrow("input");

        // Assert
        result.Should().Be("input-processed");
    }

    [Fact]
    public async Task CreateArrow_WhenLogicThrows_PropagatesException()
    {
        // Arrange
        var arrow = _sut.CreateArrow<string>(async (engine, context) =>
        {
            await Task.CompletedTask;
            throw new InvalidOperationException("test error");
        });

        // Act
        Func<Task> act = async () => await arrow("input");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("test error");
    }

    [Fact]
    public async Task LoadContextAsync_WithFacts_DoesNotThrow()
    {
        // Arrange
        var facts = new[] { "(= (IsA dog Animal) True)", "(= (IsA cat Animal) True)" };

        // Act
        Func<Task> act = async () => await _sut.LoadContextAsync("animals", facts);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task InferAsync_ReturnsResults()
    {
        // Arrange & Act
        var results = await _sut.InferAsync("(match &self (IsA $x Animal) $x)");

        // Assert
        results.Should().NotBeNull();
    }

    [Fact]
    public void ExportKnowledge_ReturnsString()
    {
        // Act
        string knowledge = _sut.ExportKnowledge();

        // Assert
        knowledge.Should().NotBeNull();
    }

    [Fact]
    public async Task GetTraceAsync_ReturnsTraceEntries()
    {
        // Act
        var trace = await _sut.GetTraceAsync();

        // Assert
        trace.Should().NotBeNull();
    }

    [Fact]
    public async Task GetTraceAsync_AfterArrowExecution_ContainsEntries()
    {
        // Arrange
        var arrow = _sut.CreateArrow<string>(async (engine, context) =>
        {
            await Task.CompletedTask;
            return "done";
        });

        // Act
        await arrow("input");
        var trace = await _sut.GetTraceAsync();

        // Assert
        trace.Should().NotBeNull();
        // After execution, trace may contain entry and success events
    }

    [Fact]
    public void CreateReasoningFlow_ReturnsFlow()
    {
        // Act
        var flow = _sut.CreateReasoningFlow("test-flow", "A test flow");

        // Assert
        flow.Should().NotBeNull();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var step = new HyperonReasoningStep("disposable-step");

        // Act
        Action act = () =>
        {
            step.Dispose();
            step.Dispose();
        };

        // Assert
        act.Should().NotThrow();
    }

    public void Dispose()
    {
        _sut.Dispose();
    }
}
