// <copyright file="WalCompactorTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.IO;
using FluentAssertions;
using Ouroboros.Domain.States;
using Ouroboros.Network;
using Ouroboros.Network.Persistence;
using Xunit;

namespace Ouroboros.Tests.Network.Persistence;

[Trait("Category", "Unit")]
public sealed class WalCompactorTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    #region Input Validation Tests

    [Fact]
    public async Task CompactAsync_NullPath_ReturnsFailure()
    {
        // Act
        var result = await WalCompactor.CompactAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("null or empty");
    }

    [Fact]
    public async Task CompactAsync_EmptyPath_ReturnsFailure()
    {
        // Act
        var result = await WalCompactor.CompactAsync("");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("null or empty");
    }

    [Fact]
    public async Task CompactAsync_NonexistentFile_ReturnsFailure()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.wal");

        // Act
        var result = await WalCompactor.CompactAsync(path);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    #endregion

    #region Compaction Tests

    [Fact]
    public async Task CompactAsync_ValidWalWithNodes_ReturnsSuccess()
    {
        // Arrange
        var walPath = GetTempWalPath();
        await WriteTestWalAsync(walPath);

        // Act
        var result = await WalCompactor.CompactAsync(walPath);

        // Assert
        result.IsSuccess.Should().BeTrue();
        File.Exists(walPath).Should().BeTrue("compacted file should exist at original path");
    }

    [Fact]
    public async Task CompactAsync_ValidWal_BackupIsDeleted()
    {
        // Arrange
        var walPath = GetTempWalPath();
        await WriteTestWalAsync(walPath);
        var backupPath = walPath + ".backup";

        // Act
        var result = await WalCompactor.CompactAsync(walPath);

        // Assert
        result.IsSuccess.Should().BeTrue();
        File.Exists(backupPath).Should().BeFalse("backup should be deleted after successful compaction");
    }

    [Fact]
    public async Task CompactAsync_ValidWal_TempFileIsCleanedUp()
    {
        // Arrange
        var walPath = GetTempWalPath();
        await WriteTestWalAsync(walPath);
        var tempPath = walPath + ".compact.tmp";

        // Act
        var result = await WalCompactor.CompactAsync(walPath);

        // Assert
        result.IsSuccess.Should().BeTrue();
        File.Exists(tempPath).Should().BeFalse("temp file should be cleaned up");
    }

    [Fact]
    public async Task CompactAsync_WithExistingBackup_OverwritesBackup()
    {
        // Arrange
        var walPath = GetTempWalPath();
        await WriteTestWalAsync(walPath);
        var backupPath = walPath + ".backup";
        File.WriteAllText(backupPath, "old backup");
        _tempFiles.Add(backupPath);

        // Act
        var result = await WalCompactor.CompactAsync(walPath);

        // Assert
        result.IsSuccess.Should().BeTrue();
        File.Exists(backupPath).Should().BeFalse();
    }

    [Fact]
    public async Task CompactAsync_PreservesNodeData()
    {
        // Arrange
        var walPath = GetTempWalPath();
        var node = MonadNode.FromReasoningState(new Draft("test content"));
        await using (var persistence = new FileWalPersistence(walPath))
        {
            await persistence.AppendNodeAsync(node);
            await persistence.FlushAsync();
        }

        // Act
        var result = await WalCompactor.CompactAsync(walPath);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify the compacted WAL can be replayed and contains the same node
        await using var verifyPersistence = new FileWalPersistence(walPath);
        var entries = new List<WalEntry>();
        await foreach (var entry in verifyPersistence.ReplayAsync())
        {
            entries.Add(entry);
        }

        entries.Should().ContainSingle(e => e.EntryType == WalEntryType.Node);
    }

    [Fact]
    public async Task CompactAsync_PreservesEdgeData()
    {
        // Arrange
        var walPath = GetTempWalPath();
        var node1 = MonadNode.FromReasoningState(new Draft("input"));
        var node2 = MonadNode.FromReasoningState(new Draft("output"));
        var edge = TransitionEdge.CreateSimple(
            node1.Id, node2.Id, "Transform", new { Config = "test" });

        await using (var persistence = new FileWalPersistence(walPath))
        {
            await persistence.AppendNodeAsync(node1);
            await persistence.AppendNodeAsync(node2);
            await persistence.AppendEdgeAsync(edge);
            await persistence.FlushAsync();
        }

        // Act
        var result = await WalCompactor.CompactAsync(walPath);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await using var verifyPersistence = new FileWalPersistence(walPath);
        var entries = new List<WalEntry>();
        await foreach (var entry in verifyPersistence.ReplayAsync())
        {
            entries.Add(entry);
        }

        entries.Should().Contain(e => e.EntryType == WalEntryType.Node);
        entries.Should().Contain(e => e.EntryType == WalEntryType.Edge);
    }

    [Fact]
    public async Task CompactAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var walPath = GetTempWalPath();
        await WriteTestWalAsync(walPath);
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        await FluentActions.Invoking(async () =>
                await WalCompactor.CompactAsync(walPath, cts.Token))
            .Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region Helper Methods

    private string GetTempWalPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"test_wal_{Guid.NewGuid()}.wal");
        _tempFiles.Add(path);
        _tempFiles.Add(path + ".compact.tmp");
        _tempFiles.Add(path + ".backup");
        return path;
    }

    private static async Task WriteTestWalAsync(string walPath)
    {
        var node1 = MonadNode.FromReasoningState(new Draft("content 1"));
        var node2 = MonadNode.FromReasoningState(new Draft("content 2"));

        await using var persistence = new FileWalPersistence(walPath);
        await persistence.AppendNodeAsync(node1);
        await persistence.AppendNodeAsync(node2);
        await persistence.FlushAsync();
    }

    public void Dispose()
    {
        foreach (var path in _tempFiles)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
                // Best-effort cleanup
            }
        }
    }

    #endregion
}
