namespace Ouroboros.Pipeline.Tests;

using Ouroboros.Pipeline.Replay;

[Trait("Category", "Unit")]
public class ReplayEngineTests
{
    #region Construction

    [Fact]
    public void Constructor_ShouldInitialize()
    {
        var llm = new Mock<ToolAwareChatModel>();
        var embed = new Mock<IEmbeddingModel>().Object;
        var engine = new ReplayEngine(llm.Object, embed);
        engine.Should().NotBeNull();
    }

    #endregion

    #region ReplayAsync

    [Fact]
    public async Task ReplayAsync_ShouldReplayBranch()
    {
        // Arrange
        var llm = new Mock<ToolAwareChatModel>();
        var embed = new Mock<IEmbeddingModel>();
        var engine = new ReplayEngine(llm.Object, embed.Object);
        var branch = new PipelineBranch("test", new TrackedVectorStore(), DataSource.FromPath(Environment.CurrentDirectory));
        var tools = new ToolRegistry();

        // Act
        var replayed = await engine.ReplayAsync(branch, "topic", "query", tools);

        // Assert
        replayed.Should().NotBeNull();
        replayed.Name.Should().Contain("replay");
    }

    [Fact]
    public async Task ReplayAsync_NullBranch_ShouldThrowArgumentNullException()
    {
        var llm = new Mock<ToolAwareChatModel>();
        var embed = new Mock<IEmbeddingModel>().Object;
        var engine = new ReplayEngine(llm.Object, embed);

        await Assert.ThrowsAsync<ArgumentNullException>(() => engine.ReplayAsync(null!, "topic", "query", new ToolRegistry()));
    }

    #endregion
}
