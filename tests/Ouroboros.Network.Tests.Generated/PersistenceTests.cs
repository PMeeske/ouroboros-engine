namespace Ouroboros.Network.Tests.Persistence;

using System.Collections.Immutable;
using System.Text.Json;

[Trait("Category", "Unit")]
public sealed class FileWalPersistenceTests : IDisposable
{
    private readonly string _testFilePath;

    public FileWalPersistenceTests()
    {
        _testFilePath = Path.Combine(Path.GetTempPath(), $"test_wal_{Guid.NewGuid()}.ndjson");
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_testFilePath))
            {
                File.Delete(_testFilePath);
            }
        }
        catch { }
    }

    #region Construction

    [Fact]
    public void Constructor_NullPath_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new FileWalPersistence(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("walFilePath");
    }

    [Fact]
    public void Constructor_CreatesDirectory()
    {
        // Arrange
        var dir = Path.Combine(Path.GetTempPath(), $"testdir_{Guid.NewGuid()}");
        var path = Path.Combine(dir, "wal.ndjson");

        try
        {
            // Act
            using var persistence = new FileWalPersistence(path);

            // Assert
            Directory.Exists(dir).Should().BeTrue();
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    #endregion

    #region AppendNodeAsync

    [Fact]
    public async Task AppendNodeAsync_NullNode_ThrowsArgumentNullException()
    {
        // Arrange
        await using var persistence = new FileWalPersistence(_testFilePath);

        // Act
        Func<Task> act = async () => await persistence.AppendNodeAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("node");
    }

    [Fact]
    public async Task AppendNodeAsync_WritesToFile()
    {
        // Arrange
        await using var persistence = new FileWalPersistence(_testFilePath);
        var node = CreateNode("Test");

        // Act
        await persistence.AppendNodeAsync(node);
        await persistence.FlushAsync();

        // Assert
        var content = await File.ReadAllTextAsync(_testFilePath);
        content.Should().Contain("AddNode");
        content.Should().Contain(node.Id.ToString());
    }

    #endregion

    #region AppendEdgeAsync

    [Fact]
    public async Task AppendEdgeAsync_NullEdge_ThrowsArgumentNullException()
    {
        // Arrange
        await using var persistence = new FileWalPersistence(_testFilePath);

        // Act
        Func<Task> act = async () => await persistence.AppendEdgeAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("edge");
    }

    [Fact]
    public async Task AppendEdgeAsync_WritesToFile()
    {
        // Arrange
        await using var persistence = new FileWalPersistence(_testFilePath);
        var edge = TransitionEdge.CreateSimple(Guid.NewGuid(), Guid.NewGuid(), "Op", new { });

        // Act
        await persistence.AppendEdgeAsync(edge);
        await persistence.FlushAsync();

        // Assert
        var content = await File.ReadAllTextAsync(_testFilePath);
        content.Should().Contain("AddEdge");
    }

    #endregion

    #region ReplayAsync

    [Fact]
    public async Task ReplayAsync_EmptyFile_ReturnsEmpty()
    {
        // Arrange
        await using var persistence = new FileWalPersistence(_testFilePath);

        // Act
        var entries = new List<WalEntry>();
        await foreach (var entry in persistence.ReplayAsync())
        {
            entries.Add(entry);
        }

        // Assert
        entries.Should().BeEmpty();
    }

    [Fact]
    public async Task ReplayAsync_ReturnsWrittenEntries()
    {
        // Arrange
        await using var persistence = new FileWalPersistence(_testFilePath);
        var node = CreateNode("Test");
        await persistence.AppendNodeAsync(node);
        await persistence.FlushAsync();

        // Act
        var entries = new List<WalEntry>();
        await foreach (var entry in persistence.ReplayAsync())
        {
            entries.Add(entry);
        }

        // Assert
        entries.Should().ContainSingle();
        entries[0].Type.Should().Be(WalEntryType.AddNode);
    }

    #endregion

    #region FlushAsync

    [Fact]
    public async Task FlushAsync_EnsuresDataWritten()
    {
        // Arrange
        await using var persistence = new FileWalPersistence(_testFilePath);
        var node = CreateNode("Test");
        await persistence.AppendNodeAsync(node);

        // Act
        await persistence.FlushAsync();

        // Assert
        var info = new FileInfo(_testFilePath);
        info.Length.Should().BeGreaterThan(0);
    }

    #endregion

    #region DisposeAsync

    [Fact]
    public async Task DisposeAsync_DoesNotThrow()
    {
        // Arrange
        var persistence = new FileWalPersistence(_testFilePath);

        // Act
        Func<Task> act = async () => await persistence.DisposeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DisposeAsync_MultipleCalls_DoesNotThrow()
    {
        // Arrange
        var persistence = new FileWalPersistence(_testFilePath);
        await persistence.DisposeAsync();

        // Act
        Func<Task> act = async () => await persistence.DisposeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    private static MonadNode CreateNode(string typeName)
    {
        return new MonadNode(Guid.NewGuid(), typeName, "{}", DateTimeOffset.UtcNow, ImmutableArray<Guid>.Empty);
    }
}

[Trait("Category", "Unit")]
public sealed class WalCompactorTests : IDisposable
{
    private readonly string _testWalPath;

    public WalCompactorTests()
    {
        _testWalPath = Path.Combine(Path.GetTempPath(), $"compact_wal_{Guid.NewGuid()}.ndjson");
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_testWalPath))
                File.Delete(_testWalPath);
            if (File.Exists(_testWalPath + ".backup"))
                File.Delete(_testWalPath + ".backup");
            if (File.Exists(_testWalPath + ".compact.tmp"))
                File.Delete(_testWalPath + ".compact.tmp");
        }
        catch { }
    }

    #region CompactAsync

    [Fact]
    public async Task CompactAsync_NullPath_ReturnsFailure()
    {
        // Act
        var result = await WalCompactor.CompactAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task CompactAsync_EmptyPath_ReturnsFailure()
    {
        // Act
        var result = await WalCompactor.CompactAsync("");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task CompactAsync_MissingFile_ReturnsFailure()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), $"missing_{Guid.NewGuid()}.ndjson");

        // Act
        var result = await WalCompactor.CompactAsync(path);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task CompactAsync_ValidFile_ReturnsSuccess()
    {
        // Arrange
        await using var persistence = new FileWalPersistence(_testWalPath);
        var node = CreateNode("Test");
        await persistence.AppendNodeAsync(node);
        await persistence.FlushAsync();
        await persistence.DisposeAsync();

        // Act
        var result = await WalCompactor.CompactAsync(_testWalPath);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    private static MonadNode CreateNode(string typeName)
    {
        return new MonadNode(Guid.NewGuid(), typeName, "{}", DateTimeOffset.UtcNow, ImmutableArray<Guid>.Empty);
    }
}

[Trait("Category", "Unit")]
public sealed class PersistentMerkleDagTests : IDisposable
{
    private readonly string _testWalPath;

    public PersistentMerkleDagTests()
    {
        _testWalPath = Path.Combine(Path.GetTempPath(), $"persistent_wal_{Guid.NewGuid()}.ndjson");
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_testWalPath))
                File.Delete(_testWalPath);
        }
        catch { }
    }

    #region Create

    [Fact]
    public void Create_NullPersistence_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => PersistentMerkleDag.Create(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("persistence");
    }

    [Fact]
    public void Create_ReturnsInstance()
    {
        // Arrange
        var persistence = new FileWalPersistence(_testWalPath);

        try
        {
            // Act
            var dag = PersistentMerkleDag.Create(persistence);

            // Assert
            dag.Should().NotBeNull();
            dag.NodeCount.Should().Be(0);
            dag.EdgeCount.Should().Be(0);
        }
        finally
        {
            persistence.DisposeAsync().GetAwaiter().GetResult();
        }
    }

    #endregion

    #region RestoreAsync

    [Fact]
    public async Task RestoreAsync_NullPersistence_ReturnsFailure()
    {
        // Act
        var result = await PersistentMerkleDag.RestoreAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be null");
    }

    [Fact]
    public async Task RestoreAsync_EmptyWAL_ReturnsSuccess()
    {
        // Arrange
        var persistence = new FileWalPersistence(_testWalPath);
        await persistence.DisposeAsync();

        // Act
        var result = await PersistentMerkleDag.RestoreAsync(persistence);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.NodeCount.Should().Be(0);
    }

    #endregion

    #region AddNodeAsync / AddEdgeAsync

    [Fact]
    public async Task AddNodeAsync_ValidNode_ReturnsSuccess()
    {
        // Arrange
        var persistence = new FileWalPersistence(_testWalPath);
        var dag = PersistentMerkleDag.Create(persistence);
        var node = CreateNode("Test");

        try
        {
            // Act
            var result = await dag.AddNodeAsync(node);

            // Assert
            result.IsSuccess.Should().BeTrue();
            dag.NodeCount.Should().Be(1);
        }
        finally
        {
            await dag.DisposeAsync();
        }
    }

    [Fact]
    public async Task AddEdgeAsync_ValidEdge_ReturnsSuccess()
    {
        // Arrange
        var persistence = new FileWalPersistence(_testWalPath);
        var dag = PersistentMerkleDag.Create(persistence);
        var input = CreateNode("Input");
        var output = CreateNode("Output");
        await dag.AddNodeAsync(input);
        await dag.AddNodeAsync(output);
        var edge = TransitionEdge.CreateSimple(input.Id, output.Id, "Op", new { });

        try
        {
            // Act
            var result = await dag.AddEdgeAsync(edge);

            // Assert
            result.IsSuccess.Should().BeTrue();
            dag.EdgeCount.Should().Be(1);
        }
        finally
        {
            await dag.DisposeAsync();
        }
    }

    #endregion

    #region Pass-through Methods

    [Fact]
    public async Task GetNode_ExistingNode_ReturnsSome()
    {
        // Arrange
        var persistence = new FileWalPersistence(_testWalPath);
        var dag = PersistentMerkleDag.Create(persistence);
        var node = CreateNode("Test");
        await dag.AddNodeAsync(node);

        try
        {
            // Act
            var result = dag.GetNode(node.Id);

            // Assert
            result.HasValue.Should().BeTrue();
        }
        finally
        {
            await dag.DisposeAsync();
        }
    }

    [Fact]
    public async Task TopologicalSort_ReturnsSuccess()
    {
        // Arrange
        var persistence = new FileWalPersistence(_testWalPath);
        var dag = PersistentMerkleDag.Create(persistence);
        var node = CreateNode("Test");
        await dag.AddNodeAsync(node);

        try
        {
            // Act
            var result = dag.TopologicalSort();

            // Assert
            result.IsSuccess.Should().BeTrue();
        }
        finally
        {
            await dag.DisposeAsync();
        }
    }

    #endregion

    #region DisposeAsync

    [Fact]
    public async Task DisposeAsync_DoesNotThrow()
    {
        // Arrange
        var persistence = new FileWalPersistence(_testWalPath);
        var dag = PersistentMerkleDag.Create(persistence);

        // Act
        Func<Task> act = async () => await dag.DisposeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DisposeAsync_MultipleCalls_DoesNotThrow()
    {
        // Arrange
        var persistence = new FileWalPersistence(_testWalPath);
        var dag = PersistentMerkleDag.Create(persistence);
        await dag.DisposeAsync();

        // Act
        Func<Task> act = async () => await dag.DisposeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    private static MonadNode CreateNode(string typeName)
    {
        return new MonadNode(Guid.NewGuid(), typeName, "{}", DateTimeOffset.UtcNow, ImmutableArray<Guid>.Empty);
    }
}
