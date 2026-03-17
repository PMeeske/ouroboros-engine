using FluentAssertions;
using Ouroboros.Agent;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public class DivideAndConquerOrchestratorTests
{
    private readonly Mock<Ouroboros.Abstractions.Core.IChatCompletionModel> _mockModel = new();

    private DivideAndConquerOrchestrator CreateSut(DivideAndConquerConfig? config = null)
    {
        return new DivideAndConquerOrchestrator(_mockModel.Object, config);
    }

    // === Constructor Tests ===

    [Fact]
    public void Constructor_NullModel_ThrowsArgumentNullException()
    {
        var act = () => new DivideAndConquerOrchestrator(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("model");
    }

    [Fact]
    public void Constructor_ValidArgs_DoesNotThrow()
    {
        var act = () => CreateSut();
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_NullConfig_UsesDefaults()
    {
        var act = () => CreateSut(null);
        act.Should().NotThrow();
    }

    // === ExecuteAsync Tests ===

    [Fact]
    public async Task ExecuteAsync_EmptyTask_ReturnsFailure()
    {
        var sut = CreateSut();

        var result = await sut.ExecuteAsync("", new List<string> { "chunk1" });

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public async Task ExecuteAsync_NullChunks_ReturnsFailure()
    {
        var sut = CreateSut();

        var result = await sut.ExecuteAsync("task", null!);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No chunks");
    }

    [Fact]
    public async Task ExecuteAsync_EmptyChunks_ReturnsFailure()
    {
        var sut = CreateSut();

        var result = await sut.ExecuteAsync("task", new List<string>());

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_SingleChunk_ReturnsSuccess()
    {
        _mockModel.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("processed chunk");

        var sut = CreateSut();

        var result = await sut.ExecuteAsync("summarize", new List<string> { "some text" });

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("processed chunk");
    }

    [Fact]
    public async Task ExecuteAsync_MultipleChunks_ProcessesAll()
    {
        var callIndex = 0;
        _mockModel.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => $"result-{Interlocked.Increment(ref callIndex)}");

        var sut = CreateSut(new DivideAndConquerConfig(MaxParallelism: 2));

        var result = await sut.ExecuteAsync("process", new List<string> { "chunk1", "chunk2", "chunk3" });

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ChunkFails_ReturnsFailure()
    {
        _mockModel.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("model error"));

        var sut = CreateSut();

        var result = await sut.ExecuteAsync("process", new List<string> { "chunk1" });

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Failed to process");
    }

    [Fact]
    public async Task ExecuteAsync_MergeResultsEnabled_MergesWithSeparator()
    {
        _mockModel.SetupSequence(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("result1")
            .ReturnsAsync("result2");

        var sut = CreateSut(new DivideAndConquerConfig(MaxParallelism: 1, MergeResults: true, MergeSeparator: " | "));

        var result = await sut.ExecuteAsync("process", new List<string> { "c1", "c2" });

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("result1");
        result.Value.Should().Contain("result2");
    }

    // === DivideIntoChunks Tests ===

    [Fact]
    public void DivideIntoChunks_EmptyText_ReturnsEmpty()
    {
        var sut = CreateSut();

        var chunks = sut.DivideIntoChunks("");

        chunks.Should().BeEmpty();
    }

    [Fact]
    public void DivideIntoChunks_WhitespaceText_ReturnsEmpty()
    {
        var sut = CreateSut();

        var chunks = sut.DivideIntoChunks("   ");

        chunks.Should().BeEmpty();
    }

    [Fact]
    public void DivideIntoChunks_ShortText_ReturnsSingleChunk()
    {
        var sut = CreateSut(new DivideAndConquerConfig(ChunkSize: 1000));

        var chunks = sut.DivideIntoChunks("This is a short text.");

        chunks.Should().HaveCount(1);
    }

    [Fact]
    public void DivideIntoChunks_LongText_ReturnsMultipleChunks()
    {
        var sut = CreateSut(new DivideAndConquerConfig(ChunkSize: 50));

        var text = string.Join("\n\n", Enumerable.Range(1, 10).Select(i => $"Paragraph {i}. This is some text content that fills out the paragraph."));

        var chunks = sut.DivideIntoChunks(text);

        chunks.Should().HaveCountGreaterThan(1);
    }

    // === GetMetrics Tests ===

    [Fact]
    public void GetMetrics_Initially_ReturnsEmpty()
    {
        var sut = CreateSut();

        var metrics = sut.GetMetrics();

        metrics.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMetrics_AfterExecution_HasEntries()
    {
        _mockModel.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("done");

        var sut = CreateSut();
        await sut.ExecuteAsync("task", new List<string> { "chunk" });

        var metrics = sut.GetMetrics();

        metrics.Should().NotBeEmpty();
    }
}

[Trait("Category", "Unit")]
public class DivideAndConquerConfigTests
{
    [Fact]
    public void DefaultConfig_HasExpectedValues()
    {
        var config = new DivideAndConquerConfig();

        config.MaxParallelism.Should().Be(4);
        config.ChunkSize.Should().Be(500);
        config.MergeResults.Should().BeTrue();
        config.MergeSeparator.Should().Contain("---");
    }

    [Fact]
    public void Config_CustomValues_ArePreserved()
    {
        var config = new DivideAndConquerConfig(MaxParallelism: 8, ChunkSize: 1000, MergeResults: false, MergeSeparator: "\n");

        config.MaxParallelism.Should().Be(8);
        config.ChunkSize.Should().Be(1000);
        config.MergeResults.Should().BeFalse();
        config.MergeSeparator.Should().Be("\n");
    }
}
